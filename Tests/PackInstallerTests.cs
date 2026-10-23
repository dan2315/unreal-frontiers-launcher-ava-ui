using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using UnrealFrontiersLauncher.Packs;

public sealed class PackInstallerTests : IDisposable
{
    private const string Server = "https://packs.test";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "uf-pack-" + Guid.NewGuid().ToString("N"));
    private readonly FakePackServer _server = new();

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private PackInstaller Installer(TimeSpan? stall = null) =>
        new(new HttpClient(_server), Server, concurrency: 2, stall ?? TimeSpan.FromSeconds(5), attemptsPerSource: 2);

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private PackFile Serve(string path, string content, string side = "both", string overwrite = "always", List<string>? urls = null)
    {
        _server.Blobs[Sha(content)] = Encoding.UTF8.GetBytes(content);
        return new PackFile(path, Sha(content), Encoding.UTF8.GetByteCount(content), side, overwrite, urls);
    }

    private static PackManifest Manifest(string version, params PackFile[] files) =>
        new(1, "test", "Test", version, new PackGame("1.21.1", "neoforge", "21.1.0"), 4096, null, null, files.ToList());

    private string Read(string path) => File.ReadAllText(Path.Combine(_folder, path));

    [Fact]
    public async Task A_fresh_install_downloads_the_client_files_and_skips_the_server_only_ones()
    {
        var manifest = Manifest("1.0.0", Serve("mods/a.jar", "a"), Serve("config/x.toml", "x"), Serve("mods/server.jar", "s", side: "server"));

        var result = await Installer().InstallAsync(manifest, _folder, null, default);

        Assert.Equal((2, 0, 0), (result.Downloaded, result.Verified, result.Removed));
        Assert.Equal("a", Read("mods/a.jar"));
        Assert.Equal("x", Read("config/x.toml"));
        Assert.False(File.Exists(Path.Combine(_folder, "mods/server.jar")));
        Assert.Empty(Directory.GetFiles(_folder, "*.uf-part", SearchOption.AllDirectories));
        Assert.Equal("1.0.0", InstalledState.Load(_folder).Version);
    }

    [Fact]
    public async Task An_update_downloads_only_what_changed_removes_what_was_dropped_and_leaves_the_players_files()
    {
        await Installer().InstallAsync(Manifest("1.0.0", Serve("mods/keep.jar", "keep"), Serve("mods/old.jar", "old"), Serve("mods/change.jar", "v1")), _folder, null, default);
        File.WriteAllText(Path.Combine(_folder, "options.txt"), "mine");
        File.WriteAllText(Path.Combine(_folder, "mods/own.jar"), "own");
        _server.Requests.Clear();

        var result = await Installer().InstallAsync(Manifest("1.1.0", Serve("mods/keep.jar", "keep"), Serve("mods/change.jar", "v2"), Serve("mods/new.jar", "new")), _folder, null, default);

        Assert.Equal((2, 1, 1), (result.Downloaded, result.Verified, result.Removed));
        Assert.Equal(2, _server.Requests.Count);
        Assert.Equal("v2", Read("mods/change.jar"));
        Assert.False(File.Exists(Path.Combine(_folder, "mods/old.jar")));
        Assert.Equal("mine", Read("options.txt"));
        Assert.Equal("own", Read("mods/own.jar"));
    }

    [Fact]
    public async Task A_file_the_player_changed_is_put_back_unless_it_is_install_once()
    {
        var manifest = Manifest("1.0.0", Serve("mods/a.jar", "a"), Serve("config/c.toml", "default", overwrite: "once"));
        await Installer().InstallAsync(manifest, _folder, null, default);
        File.WriteAllText(Path.Combine(_folder, "mods/a.jar"), "tampered!");
        File.WriteAllText(Path.Combine(_folder, "config/c.toml"), "player's own");

        await Installer().InstallAsync(manifest, _folder, null, default);

        Assert.Equal("a", Read("mods/a.jar"));
        Assert.Equal("player's own", Read("config/c.toml"));
        File.Delete(Path.Combine(_folder, "config/c.toml"));
        await Installer().InstallAsync(manifest, _folder, null, default);
        Assert.Equal("default", Read("config/c.toml"));
    }

    [Fact]
    public async Task A_dropped_install_once_file_stays_and_a_dropped_mod_goes_with_its_emptied_folder()
    {
        await Installer().InstallAsync(Manifest("1.0.0", Serve("extra/a.jar", "a"), Serve("config/c.toml", "default", overwrite: "once")), _folder, null, default);

        var result = await Installer().InstallAsync(Manifest("1.1.0", Serve("mods/b.jar", "b")), _folder, null, default);

        Assert.Equal(1, result.Removed);
        Assert.False(Directory.Exists(Path.Combine(_folder, "extra")), "an emptied folder goes with its last file");
        Assert.Equal("default", Read("config/c.toml"));
    }

    [Fact]
    public async Task When_the_pack_server_does_not_have_a_file_or_serves_another_one_a_mirror_is_used()
    {
        var content = "from-mirror";
        var file = new PackFile("mods/m.jar", Sha(content), content.Length, Urls: ["https://mirror.test/m.jar"]);
        _server.Mirror["https://mirror.test/m.jar"] = Encoding.UTF8.GetBytes(content);

        // Missing on the pack server.
        await Installer().InstallAsync(Manifest("1.0.0", file), _folder, null, default);
        Assert.Equal(content, Read("mods/m.jar"));

        // Present, but with other content: refused, the mirror is used.
        File.Delete(Path.Combine(_folder, "mods/m.jar"));
        _server.Blobs[file.Sha256] = Encoding.UTF8.GetBytes("EVIL-CONTENT");
        await Installer().InstallAsync(Manifest("1.0.1", file), _folder, null, default);
        Assert.Equal(content, Read("mods/m.jar"));
    }

    [Fact]
    public async Task A_file_nobody_can_serve_correctly_fails_and_leaves_nothing_at_its_path()
    {
        var file = new PackFile("mods/bad.jar", Sha("expected"), 8);
        _server.Blobs[file.Sha256] = Encoding.UTF8.GetBytes("WRONG123");

        var error = await Assert.ThrowsAsync<PackException>(() => Installer().InstallAsync(Manifest("1.0.0", file), _folder, null, default));

        Assert.Contains("mods/bad.jar", error.Message);
        Assert.False(File.Exists(Path.Combine(_folder, "mods/bad.jar")));
        Assert.Empty(Directory.GetFiles(_folder, "*.uf-part", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task A_partial_download_is_resumed_with_a_range_request()
    {
        var content = new string('x', 5000) + "end";
        var file = Serve("mods/big.jar", content);
        Directory.CreateDirectory(Path.Combine(_folder, "mods"));
        File.WriteAllText(Path.Combine(_folder, $"mods/big.jar.{file.Sha256[..16]}.uf-part"), content[..3000]);

        await Installer().InstallAsync(Manifest("1.0.0", file), _folder, null, default);

        Assert.Equal(content, Read("mods/big.jar"));
        Assert.Equal(3000, _server.Requests.Single().Headers.Range?.Ranges.Single().From);
    }

    [Fact]
    public async Task Progress_counts_bytes_and_files()
    {
        var manifest = Manifest("1.0.0", Serve("mods/a.jar", new string('a', 1000)), Serve("mods/b.jar", new string('b', 500)));
        var reports = new List<PackProgress>();

        await Installer().InstallAsync(manifest, _folder, new SyncProgress<PackProgress>(reports.Add), default);

        var last = reports[^1];
        Assert.Equal((1500L, 1500L, 2, 2), (last.TotalBytes, last.DoneBytes, last.TotalFiles, last.DoneFiles));
    }

    [Theory]
    [InlineData("../evil.jar")]
    [InlineData("mods/../../evil.jar")]
    [InlineData(".uf-installed.json")]
    public async Task A_manifest_that_reaches_outside_the_folder_or_the_state_file_is_refused(string path)
    {
        var manifest = Manifest("1.0.0", Serve(path, "x"));

        await Assert.ThrowsAsync<PackException>(() => Installer().InstallAsync(manifest, _folder, null, default));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_folder)!, "evil.jar")));
    }

    [Fact]
    public async Task A_dropped_file_the_player_has_changed_or_that_was_install_once_is_not_deleted()
    {
        await Installer().InstallAsync(Manifest("1.0.0", Serve("mods/a.jar", "a"), Serve("mods/b.jar", "b"), Serve("config/c.toml", "default", overwrite: "once")), _folder, null, default);
        File.WriteAllText(Path.Combine(_folder, "mods/b.jar"), "my own build of b");
        File.WriteAllText(Path.Combine(_folder, "config/c.toml"), "my config");

        var result = await Installer().InstallAsync(Manifest("1.1.0", Serve("mods/new.jar", "n")), _folder, null, default);

        Assert.Equal(1, result.Removed);
        Assert.False(File.Exists(Path.Combine(_folder, "mods/a.jar")));
        Assert.Equal("my own build of b", Read("mods/b.jar"));
        Assert.Equal("my config", Read("config/c.toml"));
        // And once it is not the pack's, a later update does not come back for it.
        Assert.False(InstalledState.Load(_folder).Files.ContainsKey("mods/b.jar"));
    }

    [Fact]
    public async Task A_path_that_only_changes_case_keeps_its_file_on_a_case_insensitive_file_system()
    {
        if (!OperatingSystem.IsWindows())
            return;
        await Installer().InstallAsync(Manifest("1.0.0", Serve("mods/Mod.jar", "same")), _folder, null, default);

        var result = await Installer().InstallAsync(Manifest("1.1.0", Serve("mods/mod.jar", "same")), _folder, null, default);

        Assert.Equal(0, result.Removed);
        Assert.Equal("same", Read("mods/mod.jar"));
    }

    [Fact]
    public async Task A_source_that_sends_more_than_the_manifest_says_is_cut_off_and_the_next_source_is_used()
    {
        var content = "exactly-this";
        var file = new PackFile("mods/long.jar", Sha(content), content.Length, Urls: ["https://mirror.test/long.jar"]);
        _server.Blobs[file.Sha256] = Encoding.UTF8.GetBytes(content + new string('x', 100000));
        _server.Mirror["https://mirror.test/long.jar"] = Encoding.UTF8.GetBytes(content);

        await Installer().InstallAsync(Manifest("1.0.0", file), _folder, null, default);

        Assert.Equal(content, Read("mods/long.jar"));
    }

    [Fact]
    public async Task A_server_that_never_answers_is_given_up_not_waited_for_forever()
    {
        var file = Serve("mods/hang.jar", "hang");
        _server.HangHeaders = true;

        await Assert.ThrowsAsync<PackException>(() => Installer(TimeSpan.FromMilliseconds(200)).InstallAsync(Manifest("1.0.0", file), _folder, null, default));
    }

    [Fact]
    public async Task A_missing_file_is_not_retried_on_the_same_source()
    {
        var file = new PackFile("mods/gone.jar", Sha("gone"), 4);

        await Assert.ThrowsAsync<PackException>(() => Installer().InstallAsync(Manifest("1.0.0", file), _folder, null, default));

        Assert.Single(_server.Requests);
    }

    [Fact]
    public async Task A_download_that_stalls_is_given_up()
    {
        var file = Serve("mods/slow.jar", "slow");
        _server.Stall = true;

        await Assert.ThrowsAsync<PackException>(() => Installer(TimeSpan.FromMilliseconds(200)).InstallAsync(Manifest("1.0.0", file), _folder, null, default));
    }

    [Fact]
    public void A_manifest_in_a_newer_schema_asks_for_an_update_and_garbage_is_a_readable_error()
    {
        var newer = Assert.Throws<PackException>(() => PackManifest.Parse("""{"schema":2,"pack":"p","name":"P","version":"1","game":{"minecraft":"1","loader":"forge"},"minRam":1,"files":[]}"""));
        Assert.Contains("обновите лаунчер", newer.Message);
        Assert.Throws<PackException>(() => PackManifest.Parse("<html>"));
        Assert.Throws<PackException>(() => PackManifest.Parse("null"));

        var ok = PackManifest.Parse("""{"schema":1,"pack":"p","name":"P","version":"1","game":{"minecraft":"1.21.1","loader":"forge"},"minRam":4096,"future":{"x":1},"files":[{"path":"mods/a.jar","sha256":"aa","size":1,"newField":true}]}""");
        Assert.Equal(("mods/a.jar", "both", "always"), (ok.Files[0].Path, ok.Files[0].Side, ok.Files[0].Overwrite));
    }

    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value)
        {
            lock (this)
                handler(value);
        }
    }

    private sealed class FakePackServer : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Blobs { get; } = [];
        public Dictionary<string, byte[]> Mirror { get; } = [];
        public List<HttpRequestMessage> Requests { get; } = [];
        public bool Stall { get; set; }
        public bool HangHeaders { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (Requests)
                Requests.Add(request);
            var url = request.RequestUri!.ToString();
            byte[]? data = null;
            if (url.StartsWith($"{Server}/updateclient/blobs/"))
                Blobs.TryGetValue(url[(url.LastIndexOf('/') + 1)..], out data);
            else
                Mirror.TryGetValue(url, out data);
            if (HangHeaders)
                await Task.Delay(Timeout.Infinite, ct);
            if (data is null)
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (Stall)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StallingStream()) };

            var from = request.Headers.Range?.Ranges.FirstOrDefault()?.From;
            if (from is { } start)
            {
                var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(data[(int)start..]) };
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, data.Length - 1, data.Length);
                return response;
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
        }
    }

    private sealed class StallingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
