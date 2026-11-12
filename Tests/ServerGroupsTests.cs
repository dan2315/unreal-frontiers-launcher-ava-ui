using System.Linq;
using UnrealFrontiersLauncher;

public class ServerGroupsTests
{
    private const string Groups = """
        [{"id":"rubezh","name":"Рубеж","description":"Одна сборка, один вход.","color":null,"host":"unrealfrontiers.com","port":25580,
          "online":3,"maxPlayers":50,"primaryServerId":"zaruba","packId":"zaruba","servers":[
            {"id":"zaruba","name":"Unreal Zaruba","description":"PvP","kind":"PvP","color":"#34D399","locked":false,"direct":true,"up":true,"online":2,"maxPlayers":30},
            {"id":"survival","name":"Выживание в городах","description":"","kind":"Выживание","color":"#EEFF00","locked":false,"direct":true,"up":false,"online":1,"maxPlayers":30},
            {"id":"extraction","name":"Глубины","kind":"Экстракшн","color":"#5C5878","locked":true,"direct":false,"up":true,"online":0,"maxPlayers":20}]},
         {"id":"nohost","name":"Без адреса","servers":[]}]
        """;

    [Fact]
    public void Parse_ReadsTheGroupAndItsServers()
    {
        var group = ServerGroups.Parse(Groups)!.Single();

        Assert.Equal("Рубеж", group.Name);
        Assert.Equal("unrealfrontiers.com:25580", group.Address);
        Assert.Equal("zaruba", group.PackId);
        Assert.Equal("zaruba", group.PrimaryServerId);
        Assert.Null(group.Color);
        Assert.Equal(3, group.Online);
        Assert.Equal(50, group.MaxPlayers);
        Assert.Equal(["zaruba", "survival", "extraction"], group.Servers.Select(server => server.Id));

        Assert.Equal("", group.Servers[2].Description);
        Assert.Equal(20, group.Servers[2].MaxPlayers);
        Assert.False(group.Servers[1].Up);
    }

    [Fact]
    public void Parse_AFrontServerWithoutALimitHasNone()
    {
        var group = ServerGroups.Parse(Groups.Replace("\"maxPlayers\":50,", ""))!.Single();
        Assert.Equal(0, group.MaxPlayers);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html></html>")]
    [InlineData("{\"items\":[]}")]
    public void Parse_SomethingElseIsNotAList(string body) => Assert.Null(ServerGroups.Parse(body));

    [Fact]
    public void ForPack_ByPackIdThenByPrimaryServer()
    {
        var groups = ServerGroups.Parse(Groups)!;
        Assert.Equal("rubezh", ServerGroups.ForPack(groups, "zaruba")?.Id);
        Assert.Null(ServerGroups.ForPack(groups, "create"));
        Assert.Null(ServerGroups.ForPack(null, "zaruba"));

        var withoutPack = ServerGroups.Parse(Groups.Replace("\"packId\":\"zaruba\"", "\"packId\":null"))!;
        Assert.Equal("rubezh", ServerGroups.ForPack(withoutPack, "zaruba")?.Id);
    }

    [Fact]
    public void Row_ShowsTheServerWithoutAnyWayToJoinIt()
    {
        var group = ServerGroups.Parse(Groups)!.Single();
        var rows = group.Servers.Select(server => new GroupServerRow(server, null)).ToList();

        Assert.Equal("2/30", rows[0].Online);
        Assert.Equal("PvP", rows[0].Tip);
        Assert.Equal("Недоступен", rows[1].StatusText);
        Assert.Null(rows[2].Tip);
        Assert.Equal("Г", rows[2].Initial);
        Assert.False(rows[2].HasIcon);
    }
}
