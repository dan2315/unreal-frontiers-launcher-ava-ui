using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher;

/// <summary>A sign-in provider the backend has switched on (GET /auth/oauth/providers), e.g. google or yandex.</summary>
public sealed record OAuthProvider(string Name, string Title);

public static partial class OAuth
{
    // The name goes into a URL that is opened in the browser: only plain names from the server are used.
    [GeneratedRegex("^[a-z0-9_-]{1,32}$")]
    private static partial Regex ValidName();

    public static bool IsValidName(string? name) => name != null && ValidName().IsMatch(name);

    /// <summary>{"providers":[{"name":"google","title":"Google"}]}; empty when none is enabled or the answer is not that.</summary>
    public static IReadOnlyList<OAuthProvider> ParseProviders(string? json)
    {
        var result = new List<OAuthProvider>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("providers", out var providers)
                || providers.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in providers.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("name", out var nameElement)
                    || nameElement.ValueKind != JsonValueKind.String)
                    continue;

                var name = nameElement.GetString();
                if (!IsValidName(name) || name == LoginFlow.Discord) continue;

                var title = item.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String
                    ? titleElement.GetString() : null;
                result.Add(new OAuthProvider(name!, string.IsNullOrWhiteSpace(title) ? name! : title!));
            }
        }
        catch (JsonException)
        {
        }
        return result;
    }

    /// <summary>The start of a launcher sign-in: no "?client=web", the callback then returns to the launcher through uflauncher://.</summary>
    public static string StartUrl(string baseUrl, string name) => $"{baseUrl.TrimEnd('/')}/auth/oauth/{Uri.EscapeDataString(name)}";
}

/// <summary>
/// Which sign-in is waiting for its browser to come back. The return link (uflauncher://auth-response) is the same for Discord and
/// the OAuth providers and does not say which one it is, so the launcher remembers; a new player's nickname goes to the matching endpoint.
/// </summary>
public static class LoginFlow
{
    public const string Discord = "discord";

    private static CancellationTokenSource? _wait;

    public static string? Provider { get; private set; }

    public static bool IsOAuth => Provider is { } provider && provider != Discord;

    /// <summary>Starts waiting; <paramref name="onTimeout"/> runs (on a pool thread) when the browser does not come back in time.</summary>
    public static void Begin(string provider, Action? onTimeout = null, TimeSpan? timeout = null)
    {
        _wait?.Cancel();
        Provider = provider;
        if (onTimeout == null) return;

        var source = _wait = new CancellationTokenSource();
        _ = Task.Delay(timeout ?? TimeSpan.FromMinutes(2), source.Token).ContinueWith(
            task => { if (!task.IsCanceled) onTimeout(); }, TaskScheduler.Default);
    }

    /// <summary>The browser came back (with a result or an error page that still opens the launcher).</summary>
    public static void Arrived() => _wait?.Cancel();

    public static void Finish()
    {
        _wait?.Cancel();
        Provider = null;
    }
}
