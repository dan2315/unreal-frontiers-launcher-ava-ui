namespace UnrealFrontiersLauncher
{
    public class VersionInfo
    {
        public string modpackName { get; set; }
        public string version { get; set; }
        public string modpackId { get; set; }
        public string minecraftVersion { get; set; }
        public string forgeVersion { get; set; }
        public string serverAddress {get; set; }
        public int minRam {get; set; }

        public static VersionInfo Default(string instanceId) => new VersionInfo
        {
            modpackId = instanceId,
            modpackName = "No version info available",
            forgeVersion = string.Empty,
            minecraftVersion = "1.0.0",
            minRam = 2000,
            serverAddress = string.Empty,
            version = "1.0.0"
        };
    }
}