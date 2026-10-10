using System;
using System.Linq;
using System.Net.Http;
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
        Assert.Equal("localhost:25571", zaruba.serverAddress);
    }
}
