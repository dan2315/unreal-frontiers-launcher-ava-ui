using System;
using System.IO;

namespace UnrealFrontiersLauncher;

public static class AppStaticData
{
    public static string ServerURL = "https://unrealfrontiers.xyz";
#if WINDOWS
    public static string LauncherBasePath = AppContext.BaseDirectory;
#elif LINUX
    public static string LauncherBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "UnrealFrontiers");
#endif
    public static string RemoteIndexesURI (string instanceId)  => GetFileDownloadPath("indexes.json", instanceId);
    public static string GetFileDownloadPath(string filename, string instanceId) => $"{ServerURL}/update/download_file/{instanceId}?file={Uri.EscapeDataString(filename)}";
    public static string GetVersionInfoURL(string instanceId) => $"{ServerURL}/update/get_version_data/{instanceId}";
    public static string GetInstanceIndexes(string instanceId) => $"{ServerURL}/update/indexes/{instanceId}";
    public static string GetInstanceObsoleteFiles(string instanceId) => $"{ServerURL}/update/obsolete/{instanceId}";
}
