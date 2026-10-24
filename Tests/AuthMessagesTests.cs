using System.Net;
using UnrealFrontiersLauncher;

public class AuthMessagesTests
{
    [Fact]
    public void AKnownCodeWinsOverTheEnglishText()
    {
        const string body = """{"error":"Invalid credentials","code":"verification_pending"}""";

        Assert.Equal("Этот email уже зарегистрирован, но не подтверждён", AuthMessages.Describe(HttpStatusCode.BadRequest, body));
    }

    [Fact]
    public void AnUnknownCodeFallsBackToTheEnglishText()
    {
        const string body = """{"error":"Invalid credentials","code":"something_new"}""";

        Assert.Equal("Неверный ник, email или пароль", AuthMessages.Describe(HttpStatusCode.Unauthorized, body));
    }

    [Theory]
    [InlineData("Invalid credentials", "Неверный ник, email или пароль")]
    [InlineData("Please verify your email first", "Сначала подтвердите email — ссылка пришла в письме")]
    [InlineData("Email or nickname already exists", "Такой email или ник уже занят")]
    [InlineData("Please wait a minute before requesting another email", "Подождите минуту перед повторной отправкой письма")]
    [InlineData("Invalid or expired token", "Сессия истекла, войдите снова")]
    [InlineData("Internal server error", "Ошибка сервера, попробуйте позже")]
    public void KnownEnglishTextsAreTranslated(string english, string russian)
    {
        Assert.Equal(russian, AuthMessages.Describe(HttpStatusCode.BadRequest, $$"""{"error":"{{english}}"}"""));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.NotFound, 404)]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    public void AnUnknownErrorGetsTheStatusText(HttpStatusCode status, int number)
    {
        Assert.Equal($"Ошибка {number}", AuthMessages.Describe(status, """{"error":"Some brand new problem"}"""));
        Assert.Equal($"Ошибка {number}", AuthMessages.Describe(status, "not json"));
        Assert.Equal($"Ошибка {number}", AuthMessages.Describe(status, null));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void ServerErrorsGetOneGenericText(HttpStatusCode status)
    {
        Assert.Equal("Ошибка сервера, попробуйте позже", AuthMessages.Describe(status, "<html>nginx</html>"));
    }

    [Fact]
    public void NoAnswerMeansNoConnection()
    {
        Assert.Equal("Нет связи с сервером", AuthMessages.Describe(null, null));
        Assert.Equal("Нет связи с сервером", AuthMessages.ForStatus(0));
    }

    [Fact]
    public void TheSpamHintsMatchTheSite()
    {
        Assert.Equal("Если письма нет, загляните в «Спам»: пока оно часто попадает туда.", AuthMessages.SpamHint);
        Assert.Equal("Если его нет, загляните в «Спам».", AuthMessages.ResetSpamHint);
    }
}
