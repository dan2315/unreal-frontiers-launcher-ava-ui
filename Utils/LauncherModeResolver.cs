using System;
using System.Collections.Generic;
using System.Linq;

namespace UnrealFrontiersLauncher;

public enum LauncherMode
{
    Production,
    Test
}

public static class LauncherModeResolver
{
    public const string ProductionUrl = "https://unrealfrontiers.com";
    public const string TestUrl = "http://localhost:3200";

    private static readonly string[] ProductionHosts = ["unrealfrontiers.com", "unrealfrontiers.xyz"];
    private static readonly string[] LocalHosts = ["localhost", "127.0.0.1", "::1", "[::1]", "host.docker.internal"];
    private const int ProductionLocalPort = 3000;

    public static LauncherMode Resolve(string? environmentValue, IEnumerable<string> arguments, LauncherMode defaultMode)
    {
        var args = arguments.ToList();
        if (args.Contains("--test", StringComparer.OrdinalIgnoreCase)) return LauncherMode.Test;
        if (args.Contains("--production", StringComparer.OrdinalIgnoreCase)) return LauncherMode.Production;

        var value = environmentValue?.Trim().ToLowerInvariant();
        return value switch
        {
            null or "" => defaultMode,
            "test" => LauncherMode.Test,
            "production" or "prod" => LauncherMode.Production,
            _ => throw new InvalidOperationException($"Unknown UF_MODE '{environmentValue}'. Use 'test' or 'production'.")
        };
    }

    /// <summary>The value of "--server-url &lt;url&gt;" or "--server-url=&lt;url&gt;", or null.</summary>
    public static string? ParseServerUrlArgument(IEnumerable<string> arguments)
    {
        const string name = "--server-url";
        var args = arguments.ToList();

        for (var i = 0; i < args.Count; i++)
        {
            if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                return args[i][(name.Length + 1)..];
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
                return args[i + 1];
        }

        return null;
    }

    public static string ResolveServerUrl(LauncherMode mode, string? overrideUrl)
    {
        if (mode == LauncherMode.Production) return ProductionUrl;

        var url = string.IsNullOrWhiteSpace(overrideUrl) ? TestUrl : overrideUrl.Trim().TrimEnd('/');

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"Test mode needs an http(s) backend URL, got '{url}'.");
        }

        if (IsProductionBackend(uri))
        {
            throw new InvalidOperationException(
                $"Test mode refuses to use the production backend ({uri.Authority}). Point UF_SERVER_URL at the test backend, e.g. {TestUrl}.");
        }

        return url;
    }

    public static bool IsProductionBackend(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();

        if (ProductionHosts.Any(production => host == production || host.EndsWith("." + production))) return true;

        return LocalHosts.Contains(host) && uri.Port == ProductionLocalPort;
    }

    // The game's JVM resolves "localhost" to ::1 and does not fall back, while local servers listen on IPv4 only.
    public static string ForGame(string serverUrl) =>
        Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) && uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            ? new UriBuilder(uri) { Host = "127.0.0.1" }.Uri.AbsoluteUri.TrimEnd('/')
            : serverUrl;

    public static string DescribeTestBackend(string serverUrl) =>
        Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) ? $"TEST ({uri.Authority})" : "TEST";
}
