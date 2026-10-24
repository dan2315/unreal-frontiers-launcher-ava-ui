using System.Linq;
using Xunit;

using UnrealFrontiersLauncher;

public class SkinClientTests
{
    private const string List = """
        {"items":[
          {"id":12,"name":"a","source":"upload","model":"classic","isActive":true,"isPublic":false,"previewUrl":"/skins/12/preview","createdAt":"x"},
          {"id":7,"name":"b","source":"mojang","model":"slim","isActive":false,"isPublic":false,"previewUrl":"/skins/7/preview","createdAt":"x"}
        ],"count":2,"limit":10,"uploadsEnabled":true}
        """;

    [Fact]
    public void TheActiveSkinIsTaken()
    {
        var skin = SkinClient.ParseActive(List)!;
        Assert.Equal(12, skin.Id);
        Assert.Equal("/skins/12/preview", skin.PreviewUrl);
    }

    [Fact]
    public void NoActiveSkinMeansSteve()
    {
        Assert.Null(SkinClient.ParseActive("""{"items":[{"id":7,"isActive":false,"previewUrl":"/skins/7/preview"}]}"""));
        Assert.Null(SkinClient.ParseActive("""{"items":[],"count":0}"""));
    }

    [Fact]
    public void ActiveSkinWithoutPictureMeansSteve()
    {
        Assert.Null(SkinClient.ParseActive("""{"items":[{"id":3,"isActive":true,"previewUrl":null}]}"""));
        Assert.Null(SkinClient.ParseActive("""{"items":[{"id":3,"isActive":true}]}"""));
    }

    [Fact]
    public void NotASkinListIsIgnored()
    {
        Assert.Null(SkinClient.ParseActive("<html></html>"));
        Assert.Null(SkinClient.ParseActive("[]"));
        Assert.Null(SkinClient.ParseActive("""{"items":5}"""));
    }

    [Fact]
    public void ApiPrefixOrderFollowsTheScheme()
    {
        Assert.Equal(["https://site.com/api/skins/my", "https://site.com/skins/my"], SkinClient.Candidates("https://site.com/", "/skins/my").ToArray());
        Assert.Equal(["http://h:3200/skins/12/preview", "http://h:3200/api/skins/12/preview"], SkinClient.Candidates("http://h:3200", "skins/12/preview").ToArray());
    }

    [Fact]
    public void OnlyPngBytesAreAccepted()
    {
        Assert.True(SkinClient.IsPng([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0]));
        Assert.False(SkinClient.IsPng("<html>not a png</html>"u8.ToArray()));
    }

    [Fact]
    public void CacheFileIsNamedByTheSkinId()
    {
        Assert.EndsWith("12.png", SkinClient.CachePath("root", 12));
    }
}
