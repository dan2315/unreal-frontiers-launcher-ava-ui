using System;
using System.IO;

namespace UnrealFrontiersLauncher;

public static class AppStaticData
{
#if LOCAL_BACKEND
    public const bool IsLocalBackend = true;
    private const string DefaultServerUrl = "http://localhost:3000";
    public const string InstanceSuffix = ".Local";
#else
    public const bool IsLocalBackend = false;
    private const string DefaultServerUrl = "https://unrealfrontiers.com";
    public const string InstanceSuffix = "";
#endif

    private const string UpdateRoute = "/updateclient";
    public const string DataFolderName = "UnrealFrontiers" + InstanceSuffix;

    public static readonly string ServerURL = ResolveServerUrl();

    public static readonly string LauncherBasePath = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataFolderName)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", DataFolderName);

    public static string RemoteIndexesURI(string instanceId) => GetFileDownloadPath("indexes.json", instanceId);
    public static string GetFileDownloadPath(string filename, string instanceId) => $"{ServerURL}{UpdateRoute}/download_file/{instanceId}?file={Uri.EscapeDataString(filename)}";
    public static string GetVersionInfoURL(string instanceId) => $"{ServerURL}{UpdateRoute}/get_version_data/{instanceId}";
    public static string GetInstanceIndexes(string instanceId) => $"{ServerURL}{UpdateRoute}/indexes/{instanceId}";
    public static string GetInstanceObsoleteFiles(string instanceId) => $"{ServerURL}{UpdateRoute}/obsolete/{instanceId}";

    private static string ResolveServerUrl()
    {
#if LOCAL_BACKEND
        var overrideUrl = Environment.GetEnvironmentVariable("UF_SERVER_URL");
        if (!string.IsNullOrWhiteSpace(overrideUrl)) return overrideUrl.TrimEnd('/');
#endif
        return DefaultServerUrl;
    }
}
