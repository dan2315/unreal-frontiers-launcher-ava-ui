using System;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnrealFrontiersLauncher;

// Opt-in: talks to the real dev backend. Run with UF_RUN_DEV_TESTS=1 while the dev stack is up.
public class DevBackendTests
{
    [Fact]
    public async Task ModpackList_MatchesTheContractOnTheDevBackend()
    {
        if (Environment.GetEnvironmentVariable("UF_RUN_DEV_TESTS") != "1") return;

        Assert.Equal("http://localhost:3200", AppStaticData.ServerURL);

        var list = await ModpackCatalog.FetchRemoteAsync(AppStaticData.GetModpackListURL(), new HttpClient());

        Assert.NotNull(list);
        Assert.NotEmpty(list!);
        var zaruba = list!.Single(info => info.modpackId == "zaruba");
        Assert.Equal(ModLoader.NeoForge, ModLoader.Resolve(zaruba.loader, zaruba.minecraftVersion, zaruba.forgeVersion));
        Assert.Equal("unrealfrontiers.com:25571", zaruba.serverAddress);
    }
}

public class DevPackInstallTests : IDisposable
{
    private readonly string _folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "uf-dev-pack-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_folder))
            System.IO.Directory.Delete(_folder, recursive: true);
    }

    // Opt-in like the test above: installs the real zaruba pack from the dev backend, then checks that a second run has nothing to do.
    [Fact]
    public async Task The_zaruba_pack_installs_from_the_dev_backend_and_a_second_run_downloads_nothing()
    {
        if (Environment.GetEnvironmentVariable("UF_RUN_DEV_TESTS") != "1") return;

        var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var manifest = await UnrealFrontiersLauncher.Packs.PackClient.FetchManifestAsync(http, AppStaticData.ServerURL, "zaruba", null);
        var installer = new UnrealFrontiersLauncher.Packs.PackInstaller(http, AppStaticData.ServerURL);

        var first = await installer.InstallAsync(manifest, _folder, null, default);
        var second = await installer.InstallAsync(manifest, _folder, null, default);

        Assert.Equal(manifest.ClientFiles.Count(), first.Downloaded);
        Assert.Equal(0, second.Downloaded);
        Assert.Equal(first.Downloaded, second.Verified);
        Assert.True(System.IO.Directory.GetFiles(System.IO.Path.Combine(_folder, "mods"), "*.jar").Length > 10);
    }
}
