using System;
using System.IO;

namespace UnrealFrontiersLauncher;

public static class AppSettings
{
    public static string ServerURL = "https://unrealfrontiers.xyz";
#if WINDOWS
    public static string LauncherBasePath = AppContext.BaseDirectory;
#elif LINUX
    public static string LauncherBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "UnrealFrontiers");
#endif
    public static string RemoteIndexesURI (string instanceId)  => GetFileDownloadPath("indexes.json", instanceId);
    public static string GetFileDownloadPath(string filename, string instanceId) => $"{ServerURL}/updateclient/download_file/{instanceId}?file={Uri.EscapeDataString(filename)}";
    public static string GetVersionInfoURL(string instanceId) => $"{ServerURL}/updateclient/get_version_data/{instanceId}";
    public static string GetInstanceIndexes(string instanceId) => $"{ServerURL}/updateclient/indexes/{instanceId}";
    public static string GetInstanceObsoleteFiles(string instanceId) => $"{ServerURL}/updateclient/obsolete/{instanceId}";
}
