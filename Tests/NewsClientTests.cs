using System;
using System.Linq;
using UnrealFrontiersLauncher;

public class NewsClientTests
{
    private const string Page = """
        {"items":[
          {"id":2,"serverId":"zaruba","title":"Pinned <b>post</b>","body":"Line 1\nLine 2 <script>alert(1)</script>","pinned":true,"author":"admin","createdAt":"2026-10-12T21:30:00Z","updatedAt":"2026-10-12T21:30:00Z"},
          {"id":1,"serverId":"zaruba","title":"Older","body":"","pinned":false,"author":"admin","createdAt":"2025-01-05T10:00:00Z"},
          {"id":3,"serverId":"zaruba","title":"","body":"no title is dropped","pinned":false}
        ],"total":3,"page":1,"pageSize":10}
        """;

    [Fact]
    public void Parse_ReadsThePostsInServerOrderAndKeepsTextAsIs()
    {
        var items = NewsClient.Parse(Page)!;

        Assert.Equal(2, items.Count);
        Assert.Equal("Pinned <b>post</b>", items[0].Title);
        Assert.Contains("<script>", items[0].Body);
        Assert.Contains("\n", items[0].Body);
        Assert.True(items[0].Pinned);
        Assert.False(items[1].Pinned);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 21, 30, 0, TimeSpan.Zero), items[0].CreatedAt);
    }

    [Fact]
    public void Parse_AnEmptyFeedIsNotAFailure()
    {
        Assert.Empty(NewsClient.Parse("""{"items":[],"total":0,"page":1,"pageSize":10}""")!);
    }

    [Theory]
    [InlineData("<!doctype html><html></html>")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("""{"error":"nope"}""")]
    [InlineData("""{"items":5}""")]
    public void Parse_IsNullForAnythingThatIsNotAFeed(string body)
    {
        Assert.Null(NewsClient.Parse(body));
    }

    [Fact]
    public void Candidates_UseTheApiPrefixBehindHttpsAndTheBareRouteOtherwise()
    {
        var site = NewsClient.Candidates("https://unrealfrontiers.com/", "zaruba", 10).ToList();
        Assert.Equal("https://unrealfrontiers.com/api/news?page=1&pageSize=10&serverId=zaruba", site[0]);
        Assert.Equal("https://unrealfrontiers.com/news?page=1&pageSize=10&serverId=zaruba", site[1]);

        var direct = NewsClient.Candidates("http://localhost:3200", null, 5).ToList();
        Assert.Equal("http://localhost:3200/news?page=1&pageSize=5", direct[0]);
        Assert.Equal("http://localhost:3200/api/news?page=1&pageSize=5", direct[1]);
    }

    [Fact]
    public void Candidates_EscapeTheServerId()
    {
        Assert.EndsWith("&serverId=a%26b%3Dc", NewsClient.Candidates("http://h", "a&b=c", 1).First());
    }

    [Fact]
    public void FormatDate_ShowsTheLocalDayAndTheYearOnlyWhenNotCurrent()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(5), "test", "test");
        var now = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

        // 21:30 UTC is already the next day at +5.
        Assert.Equal("13 окт", NewsClient.FormatDate(new DateTimeOffset(2026, 10, 12, 21, 30, 0, TimeSpan.Zero), now, zone));
        Assert.Equal("5 янв 2025", NewsClient.FormatDate(new DateTimeOffset(2025, 1, 5, 10, 0, 0, TimeSpan.Zero), now, zone));
        Assert.Equal("", NewsClient.FormatDate(null, now, zone));
    }

    [Fact]
    public void ToEntry_MarksPinnedPosts()
    {
        var item = NewsClient.Parse(Page)![0];

        var entry = NewsClient.ToEntry(item, DateTimeOffset.UtcNow, TimeZoneInfo.Utc);

        Assert.Equal("★ ", entry.Marker);
        Assert.Equal("", NewsClient.ToEntry(NewsClient.Parse(Page)![1], DateTimeOffset.UtcNow, TimeZoneInfo.Utc).Marker);
    }
}
