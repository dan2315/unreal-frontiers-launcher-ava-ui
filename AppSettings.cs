using System;

namespace UnrealFrontiersLauncher;

public static class AppSettings
{
    public static string ServerURL = "https://unrealfrontiers.xyz";
    public static string LauncherBasePath = AppContext.BaseDirectory;
    public static string RemoteIndexesURI (string instanceId)  => GetFileDownloadPath("indexes.json", instanceId);
    public static string GetFileDownloadPath(string filename, string instanceId) => $"{ServerURL}/updateclient/download_file/{instanceId}?file={Uri.EscapeDataString(filename)}";
    public static string GetVersionInfoURL(string instanceId) => $"{ServerURL}/updateclient/get_version_data/{instanceId}";
    public static string GetInstanceIndexes(string instanceId) => $"{ServerURL}/updateclient/indexes/{instanceId}";
    public static string GetInstanceObsoleteFiles(string instanceId) => $"{ServerURL}/updateclient/obsolete/{instanceId}";
}