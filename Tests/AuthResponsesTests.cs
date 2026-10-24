using System.Net;
using UnrealFrontiersLauncher;

public class AuthResponsesTests
{
    [Fact]
    public void ErrorCode_ReadsTheVerificationPendingCode()
    {
        const string body = """{"error":"Email already registered, verification pending","code":"verification_pending"}""";

        Assert.Equal(AuthResponses.VerificationPending, AuthResponses.ErrorCode(body));
        Assert.Equal("Email already registered, verification pending", AuthResponses.ErrorMessage(body));
    }

    [Theory]
    [InlineData("""{"error":"Email or nickname already exists"}""")]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("[1,2]")]
    [InlineData("""{"code":5}""")]
    public void ErrorCode_IsNullWhenTheServerSendsNone(string? body)
    {
        Assert.Null(AuthResponses.ErrorCode(body));
    }

    [Fact]
    public void ErrorMessage_FallsBackToTheRawBody()
    {
        Assert.Equal("Email or nickname already exists", AuthResponses.ErrorMessage("""{"error":"Email or nickname already exists"}"""));
        Assert.Equal("Bad Gateway", AuthResponses.ErrorMessage("Bad Gateway"));
        Assert.Equal("", AuthResponses.ErrorMessage(null));
    }

    [Theory]
    [InlineData(null, "", LoginFailure.NoConnection)]
    [InlineData(HttpStatusCode.OK, "", LoginFailure.None)]
    [InlineData(HttpStatusCode.Forbidden, """{"error":"Please verify your email first"}""", LoginFailure.EmailNotVerified)]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":"Invalid credentials"}""", LoginFailure.Other)]
    [InlineData(HttpStatusCode.InternalServerError, "oops", LoginFailure.Other)]
    public void LoginFailureOf_TellsWhatTheLauncherMustDo(HttpStatusCode? status, string body, LoginFailure expected)
    {
        Assert.Equal(expected, AuthResponses.LoginFailureOf(status, body));
    }
}
