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
    [InlineData(HttpStatusCode.OK, ResendStatus.Sent)]
    [InlineData(HttpStatusCode.Unauthorized, ResendStatus.InvalidCredentials)]
    [InlineData(HttpStatusCode.BadRequest, ResendStatus.AlreadyVerified)]
    [InlineData(HttpStatusCode.TooManyRequests, ResendStatus.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError, ResendStatus.Failed)]
    [InlineData(HttpStatusCode.BadGateway, ResendStatus.Failed)]
    public void ResendStatusOf_MapsTheEndpointAnswers(HttpStatusCode status, ResendStatus expected)
    {
        Assert.Equal(expected, AuthResponses.ResendStatusOf(status));
    }
}
