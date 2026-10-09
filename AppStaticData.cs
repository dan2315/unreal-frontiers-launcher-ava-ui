using System;
using System.IO;

namespace UnrealFrontiersLauncher;

public static class AppStaticData
{
    public static string ServerURL = "https://unrealfrontiers.xyz";
    public static string LauncherBasePath = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnrealFrontiers")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "UnrealFrontiers");
    public static string RemoteIndexesURI (string instanceId)  => GetFileDownloadPath("indexes.json", instanceId);
    public static string GetFileDownloadPath(string filename, string instanceId) => $"{ServerURL}/update/download_file/{instanceId}?file={Uri.EscapeDataString(filename)}";
    public static string GetVersionInfoURL(string instanceId) => $"{ServerURL}/update/get_version_data/{instanceId}";
    public static string GetInstanceIndexes(string instanceId) => $"{ServerURL}/update/indexes/{instanceId}";
    public static string GetInstanceObsoleteFiles(string instanceId) => $"{ServerURL}/update/obsolete/{instanceId}";
}
