using Xunit;
using UnrealFrontiersLauncher;

public class AccountRulesTests
{
    [Theory]
    [InlineData("abc")]
    [InlineData("Steve_01")]
    [InlineData("abcdefghijklmnop")]
    public void GoodNicknamesPass(string nickname)
    {
        Assert.True(AccountRules.ValidateNickname(nickname).IsValid);
    }

    [Theory]
    [InlineData(null, "А как ты будешь играть без имени?")]
    [InlineData("  ", "А как ты будешь играть без имени?")]
    [InlineData("ab", "Минимум 3 символа.")]
    [InlineData("abcdefghijklmnopq", "Максимум 16 символов.")]
    [InlineData("Вася_1", "Напрудил каких-то не тех буковок.")]
    [InlineData("a b c", "Напрудил каких-то не тех буковок.")]
    public void BadNicknamesGetTheirMessage(string? nickname, string message)
    {
        var (isValid, text) = AccountRules.ValidateNickname(nickname);
        Assert.False(isValid);
        Assert.Equal(message, text);
    }
}

public class AccountLinksTests
{
    [Fact]
    public void AuthAndProfileLinksFollowTheSiteOfTheBackend()
    {
        Assert.Equal("https://unrealfrontiers.com/?auth=register", SiteLinks.AuthUrl("https://unrealfrontiers.com/", "register"));
        Assert.Equal("http://localhost:8180/?auth=forgot", SiteLinks.AuthUrl("http://localhost:3200", "forgot"));
        Assert.Equal("https://unrealfrontiers.com/profile", SiteLinks.ProfileUrl("https://unrealfrontiers.com"));
        Assert.Equal("http://10.0.0.1:8180/profile", SiteLinks.ProfileUrl("http://10.0.0.1:3200"));
    }

    [Theory]
    [InlineData("invalid_nickname", "Ник не подходит: от 3 до 16 символов, только латинские буквы, цифры и подчёркивание.")]
    [InlineData("weak_password", "Пароль слишком простой: нужны большая и маленькая буквы, цифра и минимум 8 символов.")]
    public void TheNewServerCodesAreTranslated(string code, string text)
    {
        Assert.Equal(text, AuthMessages.Describe(System.Net.HttpStatusCode.BadRequest, $$"""{"error":"x","code":"{{code}}"}"""));
    }
}
