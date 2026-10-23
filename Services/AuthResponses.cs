using System.Net;
using System.Text.Json;

namespace UnrealFrontiersLauncher;

public enum ResendStatus
{
    Sent,
    InvalidCredentials,
    AlreadyVerified,
    TooManyRequests,
    Failed
}

/// <summary>Reads the error bodies of the auth endpoints: {"error": "...", "code": "..."}; old servers send no code.</summary>
public static class AuthResponses
{
    public const string VerificationPending = "verification_pending";

    public static string? ErrorCode(string? body) => ReadString(body, "code");

    public static string ErrorMessage(string? body) => ReadString(body, "error") ?? body ?? "";

    public static ResendStatus ResendStatusOf(HttpStatusCode status) => status switch
    {
        HttpStatusCode.OK => ResendStatus.Sent,
        HttpStatusCode.Unauthorized => ResendStatus.InvalidCredentials,
        HttpStatusCode.BadRequest => ResendStatus.AlreadyVerified,
        HttpStatusCode.TooManyRequests => ResendStatus.TooManyRequests,
        _ => ResendStatus.Failed
    };

    private static string? ReadString(string? body, string property)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty(property, out var value)
                   && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
