using System.Net;
using System.Text.Json;
using UnrealFrontiersLauncher;

public class TicketRulesTests
{
    [Theory]
    [InlineData("abc", "0123456789", null)]
    [InlineData("ab", "0123456789", "«Коротко»: от 3 до 120 символов.")]
    [InlineData("  ab  ", "0123456789", "«Коротко»: от 3 до 120 символов.")]
    [InlineData("abc", "012345678", "«Что случилось»: от 10 до 4000 символов.")]
    [InlineData("abc", "          ", "«Что случилось»: от 10 до 4000 символов.")]
    [InlineData(null, null, "«Коротко»: от 3 до 120 символов.")]
    public void Validate_FollowsTheBackendLimits(string? title, string? body, string? expected)
    {
        Assert.Equal(expected, TicketRules.Validate(title, body));
    }

    [Fact]
    public void Validate_AcceptsTheLongestAllowedTexts()
    {
        Assert.Null(TicketRules.Validate(new string('a', 120), new string('b', 4000)));
        Assert.NotNull(TicketRules.Validate(new string('a', 121), new string('b', 4000)));
        Assert.NotNull(TicketRules.Validate(new string('a', 120), new string('b', 4001)));
    }

    [Fact]
    public void TheFourKindsHaveTheSiteLabels()
    {
        Assert.Equal(["bug", "problem", "question", "idea"], TicketRules.Kinds.Select(k => k.Value));
        Assert.Equal("Ошибка в игре или на сайте", TicketRules.Kinds[0].Label);
        Assert.Equal("Проблема с лаунчером, сервером или аккаунтом", TicketRules.Kinds[1].Label);
        Assert.Equal("Вопрос", TicketRules.Kinds[2].Label);
        Assert.Equal("Идея", TicketRules.Kinds[3].Label);
    }
}

public class SupportInfoTests
{
    [Fact]
    public void BuildClient_NamesLauncherSystemBuildAndTheLastError()
    {
        var client = SupportInfo.BuildClient("1.6.4", "Windows 11", "Unreal Zaruba v1.1.2", "Could not   start\nthe game");

        Assert.Equal("лаунчер 1.6.4, Windows 11, сборка Unreal Zaruba v1.1.2, ошибка: Could not start the game", client);
    }

    [Fact]
    public void BuildClient_SkipsWhatIsUnknown()
    {
        Assert.Equal("лаунчер 1.6.4, Linux", SupportInfo.BuildClient("1.6.4", "Linux", null, " "));
    }

    [Fact]
    public void BuildClient_NeverExceedsWhatTheBackendTakes()
    {
        var client = SupportInfo.BuildClient("1.6.4", "Windows 11", "build", new string('x', 1000));

        Assert.Equal(TicketRules.ClientMax, client.Length);
        Assert.EndsWith("…", client);
    }

    [Theory]
    [InlineData(19045, "Windows 10")]
    [InlineData(22000, "Windows 11")]
    [InlineData(26200, "Windows 11")]
    public void WindowsName_TellsTenFromEleven(int build, string expected) => Assert.Equal(expected, SupportInfo.WindowsName(build));

    [Theory]
    [InlineData("https://unrealfrontiers.com", "https://unrealfrontiers.com")]
    [InlineData("https://dev.unrealfrontiers.com/", "https://dev.unrealfrontiers.com")]
    [InlineData("http://localhost:3200", "http://localhost:8180")]
    [InlineData("http://26.86.1.245:3200", "http://26.86.1.245:8180")]
    public void SiteUrl_MapsTheDevApiPortToTheSite(string server, string expected) => Assert.Equal(expected, SupportInfo.SiteUrl(server));

    [Fact]
    public void TicketUrl_PointsAtTheTicketOnTheSite()
    {
        Assert.Equal("https://unrealfrontiers.com/support/42", SupportInfo.TicketUrl("https://unrealfrontiers.com", 42));
    }
}

public class TicketServiceTests
{
    [Fact]
    public void BuildPayload_TrimsTheTextsAndOmitsWhatIsEmpty()
    {
        var json = TicketService.BuildPayload("bug", "  Crash  ", "  It crashed on start  ", " ", null);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("bug", root.GetProperty("kind").GetString());
        Assert.Equal("Crash", root.GetProperty("title").GetString());
        Assert.Equal("It crashed on start", root.GetProperty("body").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("serverId").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("client").ValueKind);
    }

    [Fact]
    public void BuildPayload_SendsTheServerAndTheClientLine()
    {
        using var document = JsonDocument.Parse(TicketService.BuildPayload("idea", "Title", "Some body text", "zaruba", "лаунчер 1.6.4, Windows 11"));

        Assert.Equal("zaruba", document.RootElement.GetProperty("serverId").GetString());
        Assert.Equal("лаунчер 1.6.4, Windows 11", document.RootElement.GetProperty("client").GetString());
    }

    [Fact]
    public void ParseId_ReadsTheTicketNumber()
    {
        Assert.Equal(17, TicketService.ParseId("""{"id":17,"author":"x","status":"open","thread":[]}"""));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"id":"x"}""")]
    [InlineData("""{"title":"no id"}""")]
    public void ParseId_IsZeroWhenTheAnswerIsNotATicket(string? json) => Assert.Equal(0, TicketService.ParseId(json));

    [Theory]
    [InlineData("too_fast", "Обращения можно отправлять не чаще раза в минуту")]
    [InlineData("too_many_open", "У вас уже 5 открытых обращений: дождитесь ответа или закройте одно")]
    public void TheTicketErrorCodesHaveRussianTexts(string code, string expected)
    {
        var status = code == "too_fast" ? HttpStatusCode.TooManyRequests : HttpStatusCode.Conflict;

        Assert.Equal(expected, AuthMessages.Describe(status, $$"""{"error":"x","code":"{{code}}"}"""));
    }

    [Fact]
    public void AnUnknownServerAndAMissingLoginAreReadable()
    {
        Assert.Equal("Сервер не найден", AuthMessages.Describe(HttpStatusCode.NotFound, """{"error":"Server not found"}"""));
        Assert.Equal("Нужно войти в аккаунт", AuthMessages.Describe(HttpStatusCode.Unauthorized, null));
    }
}
