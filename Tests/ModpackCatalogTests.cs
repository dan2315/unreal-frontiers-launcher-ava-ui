using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using UnrealFrontiersLauncher;

public class ModpackCatalogParseTests
{
    [Fact]
    public void Parse_ReadsTheBackendArray()
    {
        var json = """
            [
              {"id":"zaruba","name":"Unreal Zaruba","version":"1.0.0","game":{"minecraft":"1.21.1","loader":"neoforge","loaderVersion":"21.1.252"},"minRam":4096,"serverAddress":"unrealfrontiers.com:25570","fileCount":3,"totalSize":9,"manifestSha256":"x","updatedAt":"2026-01-01T00:00:00Z"},
              {"id":"create","name":"Create","version":"0.0.1","game":{"minecraft":"1.20.1","loader":"forge","loaderVersion":"47.4.0"},"minRam":0,"image":"https://example.com/c.png"}
            ]
            """;

        var list = ModpackCatalog.Parse(json);

        Assert.Equal(["zaruba", "create"], list.Select(info => info.modpackId));
        Assert.Equal(("neoforge", "21.1.252", "unrealfrontiers.com:25570"), (list[0].loader, list[0].forgeVersion, list[0].serverAddress));
        Assert.Equal(("", "https://example.com/c.png", 0), (list[1].serverAddress, list[1].imageURl, list[1].minRam));
    }

    [Fact]
    public void Parse_DropsEntriesWithoutAnIdOrGameAndDuplicateIds()
    {
        var game = """{"minecraft":"1.21.1","loader":"neoforge"}""";
        var json = $$"""
            [
              {"id":"a","name":"A","version":"1","game":{{game}}},
              {"name":"No id","version":"1","game":{{game}}},
              null,
              {"id":"nogame","name":"No game","version":"1"},
              {"id":"a","name":"A again","version":"2","game":{{game}}},
              {"id":"b","name":"Same name","version":"1","game":{{game}}},
              {"id":"c","name":"Same name","version":"1","game":{{game}}}
            ]
            """;

        var list = ModpackCatalog.Parse(json);

        Assert.Equal(["a", "b", "c"], list.Select(info => info.modpackId));
        Assert.Equal("A", list[0].modpackName);
    }

    [Fact]
    public void Parse_AcceptsAnEmptyList()
    {
        Assert.Empty(ModpackCatalog.Parse("[]"));
    }

    [Theory]
    [InlineData("{\"id\":\"a\"}")]
    [InlineData("<html>not json</html>")]
    [InlineData("")]
    public void Parse_RejectsAnythingButAnArray(string json)
    {
        Assert.ThrowsAny<JsonException>(() => ModpackCatalog.Parse(json));
    }
}

public class ModpackCatalogMergeTests
{
    private static VersionInfo Remote(string id, string name, string version = "1.0.0", string mc = "1.21.1", string loaderVersion = "21.1.252", string loader = "") =>
        new() { modpackId = id, modpackName = name, version = version, minecraftVersion = mc, forgeVersion = loaderVersion, loader = loader };

    private static MinecraftInstance Installed(string id, string name, string? installedVersion, string packVersion = "1.0.0")
    {
        var json = JsonSerializer.Serialize(new
        {
            Id = id,
            Name = name,
            GameVersion = "1.21.1",
            ForgeVersion = "21.1.252",
            ServerIp = "play.example.com",
            ServerPort = "25565",
            PackVersion = packVersion,
            InstalledVersion = installedVersion
        });
        return JsonSerializer.Deserialize<MinecraftInstance>(json)!;
    }

    [Fact]
    public void Merge_ListsEveryServerModpackAsNotInstalledWhenNothingIsLocal()
    {
        var entries = ModpackCatalog.Merge([Remote("zaruba", "Unreal Zaruba"), Remote("create", "Create", mc: "1.20.1", loaderVersion: "47.4.0")], []);

        Assert.Equal(["zaruba", "create"], entries.Select(e => e.Id));
        Assert.All(entries, e => Assert.Equal(ModpackStatus.NotInstalled, e.Status));
        Assert.Equal(ModLoader.NeoForge, entries[0].Loader);
        Assert.Equal(ModLoader.Forge, entries[1].Loader);
    }

    [Fact]
    public void Merge_MarksInstalledAndOutdatedModpacks()
    {
        var local = new[] { Installed("a", "A", "1.0.0"), Installed("b", "B", "1.0.0") };
        var remote = new[] { Remote("a", "A", "1.0.0"), Remote("b", "B", "1.1.0") };

        var entries = ModpackCatalog.Merge(remote, local);

        Assert.Equal(ModpackStatus.Installed, entries.Single(e => e.Id == "a").Status);
        Assert.Equal(ModpackStatus.UpdateAvailable, entries.Single(e => e.Id == "b").Status);
    }

    [Fact]
    public void Merge_TreatsAnUnknownInstalledVersionAsUpToDate()
    {
        var entries = ModpackCatalog.Merge([Remote("a", "A", "2.0.0")], [Installed("a", "A", "unknown")]);

        Assert.Equal(ModpackStatus.Installed, entries[0].Status);
    }

    [Fact]
    public void Merge_KeepsAnInstalledModpackThatDisappearedFromTheServer()
    {
        var entries = ModpackCatalog.Merge([Remote("a", "A")], [Installed("a", "A", "1.0.0"), Installed("gone", "Gone Pack", "1.0.0")]);

        Assert.Equal(["a", "gone"], entries.Select(e => e.Id));
        Assert.Equal(ModpackStatus.RemovedFromServer, entries[1].Status);
        Assert.Equal("Gone Pack", entries[1].Name);
    }

    [Fact]
    public void Merge_IgnoresLocalInstancesThatWereOnlySelectedNeverInstalled()
    {
        var entries = ModpackCatalog.Merge([Remote("a", "A")], [Installed("ghost", "Ghost", null)]);

        Assert.Equal(["a"], entries.Select(e => e.Id));
    }

    [Fact]
    public void Merge_UsesTheIdAsKeyWhenNamesRepeat()
    {
        var entries = ModpackCatalog.Merge([Remote("x1", "Same"), Remote("x2", "Same")], []);

        Assert.Equal(["x1", "x2"], entries.Select(e => e.Id));
    }

    [Fact]
    public void Merge_FallsBackToInstalledPlusBuiltInWhenTheServerListFailed()
    {
        var entries = ModpackCatalog.Merge(null, [Installed("zaruba", "Unreal Zaruba", "1.0.0")]);

        Assert.Equal(ModpackStatus.Installed, entries.Single(e => e.Id == "zaruba").Status);
        Assert.Contains(entries, e => e.Id == "create" && e.Status == ModpackStatus.ServerUnavailable);
        Assert.Contains(entries, e => e.Id == "monifactory");
        Assert.Equal(1, entries.Count(e => e.Id == "zaruba"));
    }

    [Fact]
    public void Merge_ReturnsAnEmptyListForAnEmptyServerWithNothingInstalled()
    {
        Assert.Empty(ModpackCatalog.Merge([], []));
    }
}

public class ModpackCatalogFetchTests
{
    private static async Task<T> WithServer<T>(int status, string body, Func<string, Task<T>> test)
    {
        var port = new Random().Next(20000, 40000);
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        _ = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = status;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        });

        var result = await test($"http://localhost:{port}/updateclient/packs");
        listener.Stop();
        return result;
    }

    [Fact]
    public async Task FetchRemote_ReturnsTheParsedList()
    {
        var list = await WithServer(200, """[{"id":"zaruba","name":"Unreal Zaruba","version":"1.0.0","game":{"minecraft":"1.21.1","loader":"neoforge"}}]""",
            url => ModpackCatalog.FetchRemoteAsync(url, new HttpClient()));

        Assert.Equal("zaruba", Assert.Single(list!).modpackId);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(500)]
    public async Task FetchRemote_ReturnsNullForAnOldBackendOrServerError(int status)
    {
        var list = await WithServer(status, "Not Found", url => ModpackCatalog.FetchRemoteAsync(url, new HttpClient()));

        Assert.Null(list);
    }

    [Fact]
    public async Task FetchRemote_ReturnsNullForGarbage()
    {
        var list = await WithServer(200, "<html>nope</html>", url => ModpackCatalog.FetchRemoteAsync(url, new HttpClient()));

        Assert.Null(list);
    }

    [Fact]
    public async Task FetchRemote_ReturnsNullWhenTheServerIsUnreachable()
    {
        var list = await ModpackCatalog.FetchRemoteAsync("http://localhost:1/updateclient/packs", new HttpClient());

        Assert.Null(list);
    }
}

public class InstallStateTests
{
    [Fact]
    public void Instance_RoundTripsPackAndInstalledVersions()
    {
        var instance = new MinecraftInstance("zaruba", "Unreal Zaruba", "1.21.1", "21.1.252", "play.example.com:25570", 4096, null, "1.0.2");
        Assert.Equal("1.0.2", instance.PackVersion);
        Assert.Null(instance.InstalledVersion);

        instance.RestoreInstalledVersion("1.0.1");
        var restored = JsonSerializer.Deserialize<MinecraftInstance>(JsonSerializer.Serialize(instance))!;

        Assert.Equal("1.0.2", restored.PackVersion);
        Assert.Equal("1.0.1", restored.InstalledVersion);
        Assert.True(restored.IsInstalled);
    }

    [Fact]
    public void Instance_WithoutAnInstalledVersionOrModsIsNotInstalled()
    {
        var instance = new MinecraftInstance("id-" + Guid.NewGuid().ToString("N"), "Pack " + Guid.NewGuid().ToString("N"), "1.21.1", "21.1.252", "x", 4096);

        Assert.False(instance.IsInstalled);
    }
}
