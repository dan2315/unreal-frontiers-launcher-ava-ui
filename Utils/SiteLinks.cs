using System;
using System.Diagnostics;

namespace UnrealFrontiersLauncher;

/// <summary>Links from the launcher to the project's site (support, the player's tickets).</summary>
public static class SiteLinks
{
    /// <summary>The site of this backend. The dev API port (3200) has its site on 8180; everything else serves both on one address.</summary>
    public static string SiteUrl(string serverUrl)
    {
        var url = serverUrl.TrimEnd('/');
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Port == 3200
            ? new UriBuilder(uri) { Port = 8180 }.Uri.GetLeftPart(UriPartial.Authority)
            : url;
    }

    /// <summary>Where a player reports a problem and follows the answers ("Мои обращения").</summary>
    public static string SupportUrl(string serverUrl) => $"{SiteUrl(serverUrl)}/support";

    /// <returns>false when no browser could be started.</returns>
    public static bool OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Console.WriteLine($"Could not open {url}: {e.Message}");
            return false;
        }
    }
}
