using System;

namespace UnrealFrontiersLauncher;

public static class AppSettings
{
    public static string ApiServerURL = "https://api.unrealfrontiers.xyz";
    public static string LauncherBasePath = AppContext.BaseDirectory;
    public static string RemoteIndexesURI (string instanceId)  => GetFileDownloadPath("indexes.json", instanceId);
    public static string GetFileDownloadPath(string filename, string instanceId) => $"{ApiServerURL}/updateclient/download_file/{instanceId}?file={Uri.EscapeDataString(filename)}";
    public static string GetVersionInfoURL(string instanceId) => $"{ApiServerURL}/updateclient/get_version_data/{instanceId}";
    public static string GetInstanceIndexes(string instanceId) => $"{ApiServerURL}/updateclient/indexes/{instanceId}";
    public static string GetInstanceObsoleteFiles(string instanceId) => $"{ApiServerURL}/updateclient/obsolete/{instanceId}";
}