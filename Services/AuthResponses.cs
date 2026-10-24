using System.Net;
using System.Text.Json;

namespace UnrealFrontiersLauncher;

/// <summary>Why a sign-in failed, when the launcher has to react to it differently from a plain error text.</summary>
public enum LoginFailure
{
    None,
    NoConnection,
    EmailNotVerified,
    Other
}

/// <summary>Reads the error bodies of the auth endpoints: {"error": "...", "code": "..."}; old servers send no code.</summary>
public static class AuthResponses
{
    public const string VerificationPending = "verification_pending";
    public const string EmailNotVerifiedText = "Please verify your email first";

    public static string? ErrorCode(string? body) => ReadString(body, "code");

    public static string ErrorMessage(string? body) => ReadString(body, "error") ?? body ?? "";

    /// <param name="status">The HTTP status, or null when there was no answer at all.</param>
    public static LoginFailure LoginFailureOf(HttpStatusCode? status, string? body)
    {
        if (status is null or 0) return LoginFailure.NoConnection;
        if (status is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices) return LoginFailure.None;
        return ErrorMessage(body).Trim() == EmailNotVerifiedText ? LoginFailure.EmailNotVerified : LoginFailure.Other;
    }

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
