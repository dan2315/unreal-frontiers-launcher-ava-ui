using System.IO;
using System.Linq;
using SharpNBT;

public class ServerListManagerTests
{
    private static ListTag List(params (string Name, string Ip)[] servers) =>
        new("servers", TagType.Compound, servers.Select(server => new CompoundTag(null)
        {
            new StringTag("name", server.Name),
            new StringTag("ip", server.Ip),
        }));

    private static (string? Name, string? Ip)[] Read(ListTag servers) => servers.Cast<CompoundTag>()
        .Select(server => (
            server.TryGetValue<StringTag>("name", out var name) ? name.Value : null,
            server.TryGetValue<StringTag>("ip", out var ip) ? ip.Value : null))
        .ToArray();

    [Fact]
    public void Update_AddsTheFrontServerToAnEmptyList()
    {
        var servers = List();
        Assert.True(ServerListManager.Update(servers, "Strata", "unrealfrontiers.com:25580", ["Strata", "Unreal Zaruba"]));
        Assert.Equal([("Strata", "unrealfrontiers.com:25580")], Read(servers));
    }

    [Fact]
    public void Update_PointsThePacksOldEntryAtTheFrontServer()
    {
        var servers = List(("Hypixel", "mc.hypixel.net"), ("Unreal Zaruba", "unrealfrontiers.com:25571"));

        Assert.True(ServerListManager.Update(servers, "Strata", "unrealfrontiers.com:25580", ["Strata", "Unreal Zaruba"]));

        Assert.Equal([("Hypixel", "mc.hypixel.net"), ("Strata", "unrealfrontiers.com:25580")], Read(servers));
    }

    [Fact]
    public void Update_LeavesOneEntryOfItsOwn()
    {
        var servers = List(("Strata", "unrealfrontiers.com:25580"), ("Unreal Zaruba", "127.0.0.1:25571"));

        Assert.True(ServerListManager.Update(servers, "Strata", "unrealfrontiers.com:25580", ["Strata", "Unreal Zaruba"]));

        Assert.Equal([("Strata", "unrealfrontiers.com:25580")], Read(servers));
    }

    [Fact]
    public void Update_AnUpToDateListIsNotRewritten()
    {
        var servers = List(("Strata", "unrealfrontiers.com:25580"));
        Assert.False(ServerListManager.Update(servers, "Strata", "unrealfrontiers.com:25580", ["Strata", "Unreal Zaruba"]));
    }

    [Fact]
    public void Update_ThePlayersOwnEntryForTheAddressIsEnough()
    {
        var servers = List(("My server", "unrealfrontiers.com:25580"));
        Assert.False(ServerListManager.Update(servers, "Strata", "unrealfrontiers.com:25580", ["Strata", "Unreal Zaruba"]));
        Assert.Equal([("My server", "unrealfrontiers.com:25580")], Read(servers));
    }

    [Fact]
    public void Update_DropsTheHiddenEntryAQuickJoinLeft()
    {
        var servers = List(("Minecraft Server", "unrealfrontiers.com:25580"));
        ((CompoundTag)servers[0]).Add(new ByteTag("hidden", 1));

        Assert.True(ServerListManager.Update(servers, "Strata", "unrealfrontiers.com:25580", ["Strata", "Unreal Zaruba"]));

        Assert.Equal([("Strata", "unrealfrontiers.com:25580")], Read(servers));
    }

    [Fact]
    public void Write_GivesTheGameANamedRootCompound()
    {
        // Read back from a file the game wrote: a root compound with an empty name.
        var original = new MemoryStream();
        ServerListManager.Write(original, List(("Strata", "unrealfrontiers.com:25580")));
        original.Position = 0;
        var servers = new TagReader(original, FormatOptions.Java).ReadTag<CompoundTag>().Get<ListTag>("servers");
        ServerListManager.Update(servers, "Strata", "unrealfrontiers.com:25581", ["Strata"]);

        var written = new MemoryStream();
        ServerListManager.Write(written, servers);
        var bytes = written.ToArray();

        // TAG_Compound with a name, as NbtIo.read in the game expects (the name itself does not matter to it).
        Assert.Equal(new byte[] { 10, 0, 7 }, bytes[..3]);
        written.Position = 0;
        var again = new TagReader(written, FormatOptions.Java).ReadTag<CompoundTag>().Get<ListTag>("servers");
        Assert.Equal([("Strata", "unrealfrontiers.com:25581")], Read(again));
    }
}
