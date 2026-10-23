using System;
using System.IO;

namespace UnrealFrontiersLauncher;

public static class AppStaticData
{
#if DEBUG || DEV_BUILD
    private const LauncherMode DefaultMode = LauncherMode.Test;
#else
    private const LauncherMode DefaultMode = LauncherMode.Production;
#endif

    private const string UpdateRoute = "/updateclient";

    public static readonly LauncherMode Mode = LauncherModeResolver.Resolve(
        Environment.GetEnvironmentVariable("UF_MODE"), Environment.GetCommandLineArgs(), DefaultMode);

    public static bool IsTestMode => Mode == LauncherMode.Test;

    public static readonly string InstanceSuffix = IsTestMode ? ".Test" : "";
    public static readonly string DataFolderName = "UnrealFrontiers" + InstanceSuffix;

#if DEV_BUILD
    // Only the DEV build takes the backend from the command line (--server-url), so a friend can test against a remote dev backend.
    // An installed copy has no arguments, so build-dev.ps1 -ServerUrl also leaves the URL in server-url.txt beside the exe.
    private static readonly string? ServerUrlArgument = LauncherModeResolver.ParseServerUrlArgument(Environment.GetCommandLineArgs())
        ?? ReadServerUrlFile();

    private static string? ReadServerUrlFile()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "server-url.txt");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }
#else
    private static readonly string? ServerUrlArgument = null;
#endif

    public static readonly string ServerURL = LauncherModeResolver.ResolveServerUrl(
        Mode, ServerUrlArgument ?? Environment.GetEnvironmentVariable("UF_SERVER_URL"));

    public static string TestBanner => LauncherModeResolver.DescribeTestBackend(ServerURL);

    public static readonly string LauncherBasePath = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataFolderName)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", DataFolderName);

    public static string GetModpackListURL() => $"{ServerURL}{UpdateRoute}/packs";
    public static string GetFileDownloadPath(string filename, string folder) => $"{ServerURL}{UpdateRoute}/download_file/{folder}?file={Uri.EscapeDataString(filename)}";
}
