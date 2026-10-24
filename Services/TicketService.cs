using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher;

public sealed record TicketKind(string Value, string Label);

/// <summary>What the backend accepts in a ticket (POST /tickets); checked here first so the player gets the answer at once.</summary>
public static class TicketRules
{
    public const int TitleMin = 3, TitleMax = 120;
    public const int BodyMin = 10, BodyMax = 4000;
    public const int ClientMax = 300;

    public static readonly IReadOnlyList<TicketKind> Kinds =
    [
        new("bug", "Ошибка в игре или на сайте"),
        new("problem", "Проблема с лаунчером, сервером или аккаунтом"),
        new("question", "Вопрос"),
        new("idea", "Идея"),
    ];

    /// <returns>What is wrong with the text, or null when it can be sent.</returns>
    public static string? Validate(string? title, string? body)
    {
        var titleLength = (title ?? "").Trim().Length;
        if (titleLength < TitleMin || titleLength > TitleMax)
            return $"«Коротко»: от {TitleMin} до {TitleMax} символов.";

        var bodyLength = (body ?? "").Trim().Length;
        if (bodyLength < BodyMin || bodyLength > BodyMax)
            return $"«Что случилось»: от {BodyMin} до {BodyMax} символов.";

        return null;
    }
}

/// <summary>The line that tells the team where the problem happened: launcher, system, modpack, the last error.</summary>
public static class SupportInfo
{
    public static string BuildClient(string launcherVersion, string os, string? modpack, string? lastError)
    {
        var text = new StringBuilder($"лаунчер {launcherVersion}, {os}");
        if (!string.IsNullOrWhiteSpace(modpack)) text.Append(", сборка ").Append(modpack.Trim());
        if (!string.IsNullOrWhiteSpace(lastError)) text.Append(", ошибка: ").Append(OneLine(lastError));
        return Limit(text.ToString(), TicketRules.ClientMax);
    }

    public static string WindowsName(int build) => build >= 22000 ? "Windows 11" : "Windows 10";

    public static string CurrentOs()
    {
        if (OperatingSystem.IsWindows()) return WindowsName(Environment.OSVersion.Version.Build);
        if (OperatingSystem.IsLinux()) return LinuxName();
        return RuntimeInformation.OSDescription;
    }

    private static string LinuxName()
    {
        try
        {
            foreach (var line in System.IO.File.ReadLines("/etc/os-release"))
                if (line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
                    return line["PRETTY_NAME=".Length..].Trim('"');
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
        {
        }
        return "Linux";
    }

    /// <summary>The site of this backend. The dev API port (3200) has its site on 8180; everything else serves both on one address.</summary>
    public static string SiteUrl(string serverUrl)
    {
        var url = serverUrl.TrimEnd('/');
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Port == 3200
            ? new UriBuilder(uri) { Port = 8180 }.Uri.GetLeftPart(UriPartial.Authority)
            : url;
    }

    public static string TicketUrl(string serverUrl, int id) => $"{SiteUrl(serverUrl)}/support/{id}";

    private static string OneLine(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Limit(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}

public sealed record TicketResult(bool Success, int Id, string Message, string? Code, HttpStatusCode? Status = null);

/// <summary>Sends a ticket for the signed-in player to POST /tickets.</summary>
public class TicketService
{
    private readonly HttpClientService _http;
    private readonly AuthService _auth;

    public TicketService(HttpClientService http, AuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    public static string BuildPayload(string kind, string title, string body, string? serverId, string? client) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = kind,
            ["title"] = title.Trim(),
            ["body"] = body.Trim(),
            ["serverId"] = string.IsNullOrWhiteSpace(serverId) ? null : serverId,
            ["client"] = string.IsNullOrWhiteSpace(client) ? null : client,
        });

    /// <returns>The ticket number, or 0 when the answer is not a ticket.</returns>
    public static int ParseId(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out var number)
                ? number
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    public async Task<TicketResult> SubmitAsync(string kind, string title, string body, string? serverId, string? client)
    {
        if (TicketRules.Validate(title, body) is { } problem)
            return new TicketResult(false, 0, problem, null);

        if (await _auth.GetFreshAccessTokenAsync() == null)
            return new TicketResult(false, 0, "Нужно войти в аккаунт", null);

        var result = await SendAsync(kind, title, body, serverId, client);
        // The server id is optional: an unknown one must not stop the ticket.
        if (!result.Success && serverId != null && result.Status == HttpStatusCode.NotFound)
            result = await SendAsync(kind, title, body, null, client);
        return result;
    }

    private async Task<TicketResult> SendAsync(string kind, string title, string body, string? serverId, string? client)
    {
        HttpResponseMessage response;
        try
        {
            using var content = new StringContent(BuildPayload(kind, title, body, serverId, client), Encoding.UTF8, "application/json");
            response = await _http.PostAsync("/tickets", content);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return new TicketResult(false, 0, AuthMessages.NoConnection, null);
        }

        var text = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            var id = ParseId(text);
            return id > 0 ? new TicketResult(true, id, "", null) : new TicketResult(false, 0, AuthMessages.ForStatus(HttpStatusCode.InternalServerError), null);
        }

        return new TicketResult(false, 0, AuthMessages.Describe(response.StatusCode, text), AuthResponses.ErrorCode(text), response.StatusCode);
    }
}
