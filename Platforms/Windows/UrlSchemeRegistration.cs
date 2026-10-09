using System;
using Microsoft.Win32;

namespace UnrealFrontiersLauncher.Windows;

public static class UrlSchemeRegistration
{
    private const string SchemeKey = @"Software\Classes\uflauncher";

    public static void Register()
    {
        if (!OperatingSystem.IsWindows() || Environment.ProcessPath is not { } exePath) return;

        using var scheme = Registry.CurrentUser.CreateSubKey(SchemeKey);
        scheme.SetValue("", "URL:uflauncher Protocol");
        scheme.SetValue("URL Protocol", "");

        using var icon = scheme.CreateSubKey("DefaultIcon");
        icon.SetValue("", $"{exePath},0");

        using var command = scheme.CreateSubKey(@"shell\open\command");
        command.SetValue("", $"\"{exePath}\" \"%1\"");
    }

    public static void Unregister()
    {
        if (!OperatingSystem.IsWindows()) return;
        Registry.CurrentUser.DeleteSubKeyTree(SchemeKey, throwOnMissingSubKey: false);
    }
}
