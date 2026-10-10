using System.Text.Json;
using UnrealFrontiersLauncher;

public class ModpackCatalogTests
{
    [Fact]
    public void Usable_KeepsModpacksTheLauncherCanOpen()
    {
        // What GET /updateclient/modpacks answers: each modpack's version.json.
        const string json = """
            [
              {"modpackName":"Create","imageURl":"none","version":"0.0.1","modpackId":"create","minecraftVersion":"1.20.1","forgeVersion":"47.4.0","serverAddress":"unrealfrontiers.xyz","minRam":4064},
              {"modpackId":"no-name"},
              {"modpackName":"Duplicate","modpackId":"create"},
              {"modpackName":"No id"}
            ]
            """;

        var modpacks = ModpackCatalog.Usable(JsonSerializer.Deserialize<List<VersionInfo>>(json));

        var create = Assert.Single(modpacks);
        Assert.Equal("create", create.modpackId);
        Assert.Equal("Create", create.modpackName);
        Assert.Equal("none", create.imageURl);
        Assert.Equal(4064, create.minRam);
    }

    [Fact]
    public void Usable_HandlesNoList() => Assert.Empty(ModpackCatalog.Usable(null));
}
