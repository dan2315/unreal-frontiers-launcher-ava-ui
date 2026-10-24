using System;
using System.Net;
using System.Threading;
using UnrealFrontiersLauncher;

public class OAuthTests
{
    [Fact]
    public void ParseProviders_ReadsTheEnabledOnes()
    {
        const string json = """{"providers":[{"name":"google","title":"Google"},{"name":"yandex","title":"Яндекс"}]}""";

        var providers = OAuth.ParseProviders(json);

        Assert.Equal([new OAuthProvider("google", "Google"), new OAuthProvider("yandex", "Яндекс")], providers);
    }

    [Theory]
    [InlineData("""{"providers":[]}""")]
    [InlineData("""{"providers":5}""")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("<html></html>")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseProviders_IsEmptyForNoProvidersOrAnotherAnswer(string? json)
    {
        Assert.Empty(OAuth.ParseProviders(json));
    }

    [Fact]
    public void ParseProviders_SkipsUnsafeNamesDiscordAndEntriesWithoutAName()
    {
        const string json = """
            {"providers":[
              {"name":"../evil","title":"x"},
              {"name":"a b","title":"x"},
              {"name":"discord","title":"Discord"},
              {"title":"no name"},
              {"name":"vk"},
              "text"
            ]}
            """;

        var providers = OAuth.ParseProviders(json);

        Assert.Equal([new OAuthProvider("vk", "vk")], providers);
    }

    [Fact]
    public void StartUrl_IsTheLauncherModeWithoutTheWebClientFlag()
    {
        Assert.Equal("https://unrealfrontiers.com/auth/oauth/google", OAuth.StartUrl("https://unrealfrontiers.com/", "google"));
        Assert.Equal("http://localhost:3200/auth/oauth/yandex", OAuth.StartUrl("http://localhost:3200", "yandex"));
    }

    [Theory]
    [InlineData("google", true)]
    [InlineData("my-provider_2", true)]
    [InlineData("Google", false)]
    [InlineData("a/b", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidName(string? name, bool expected) => Assert.Equal(expected, OAuth.IsValidName(name));
}

[Collection("LoginFlow")]
public class LoginFlowTests
{
    [Fact]
    public void RemembersWhichSignInIsWaiting()
    {
        LoginFlow.Begin("google");
        Assert.True(LoginFlow.IsOAuth);
        Assert.Equal("google", LoginFlow.Provider);

        LoginFlow.Begin(LoginFlow.Discord);
        Assert.False(LoginFlow.IsOAuth);

        LoginFlow.Finish();
        Assert.Null(LoginFlow.Provider);
        Assert.False(LoginFlow.IsOAuth);
    }

    [Fact]
    public void TimesOutWhenTheBrowserDoesNotComeBack()
    {
        using var timedOut = new ManualResetEventSlim();

        LoginFlow.Begin("yandex", timedOut.Set, TimeSpan.FromMilliseconds(80));

        Assert.True(timedOut.Wait(TimeSpan.FromSeconds(5)));
        LoginFlow.Finish();
    }

    [Fact]
    public void NoTimeoutWhenTheBrowserCameBack()
    {
        using var timedOut = new ManualResetEventSlim();

        LoginFlow.Begin("yandex", timedOut.Set, TimeSpan.FromMilliseconds(150));
        LoginFlow.Arrived();

        Assert.False(timedOut.Wait(TimeSpan.FromMilliseconds(600)));
        LoginFlow.Finish();
    }

    [Fact]
    public void ANewSignInCancelsTheTimeoutOfTheOldOne()
    {
        using var first = new ManualResetEventSlim();

        LoginFlow.Begin("google", first.Set, TimeSpan.FromMilliseconds(150));
        LoginFlow.Begin("yandex");

        Assert.False(first.Wait(TimeSpan.FromMilliseconds(600)));
        LoginFlow.Finish();
    }
}

public class OAuthMessagesTests
{
    [Theory]
    [InlineData("session_expired", "Сессия входа истекла, начните вход заново")]
    [InlineData("nickname_taken", "Этот ник уже занят. Выберите другой")]
    [InlineData("nickname_required", "Укажите ник")]
    [InlineData("already_registered", "Этот аккаунт уже зарегистрирован")]
    public void TheNewCodesHaveRussianTexts(string code, string expected)
    {
        var body = $$"""{"error":"whatever the backend says","code":"{{code}}"}""";

        Assert.Equal(expected, AuthMessages.Describe(HttpStatusCode.Conflict, body));
    }

    [Fact]
    public void TheTimeoutHintSendsThePlayerToTheirProfileOnTheSite()
    {
        Assert.Contains("привяжите Google/Яндекс в профиле на сайте", AuthMessages.OAuthTimeoutHint);
    }
}
