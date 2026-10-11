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

    public static readonly string ServerURL = LauncherModeResolver.ResolveServerUrl(
        Mode, Environment.GetEnvironmentVariable("UF_SERVER_URL"));

    public static string TestBanner => LauncherModeResolver.DescribeTestBackend(ServerURL);

    public static readonly string LauncherBasePath = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataFolderName)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", DataFolderName);

    public static string RemoteIndexesURI(string instanceId) => GetFileDownloadPath("indexes.json", instanceId);
    public static string GetFileDownloadPath(string filename, string instanceId) => $"{ServerURL}{UpdateRoute}/download_file/{instanceId}?file={Uri.EscapeDataString(filename)}";
    public static string GetModpackListURL() => $"{ServerURL}{UpdateRoute}/modpacks";
    public static string GetVersionInfoURL(string instanceId) => $"{ServerURL}{UpdateRoute}/get_version_data/{instanceId}";
    public static string GetInstanceIndexes(string instanceId) => $"{ServerURL}{UpdateRoute}/indexes/{instanceId}";
    public static string GetInstanceObsoleteFiles(string instanceId) => $"{ServerURL}{UpdateRoute}/obsolete/{instanceId}";
}
