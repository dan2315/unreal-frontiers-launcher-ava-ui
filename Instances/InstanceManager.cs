using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher
{
    public class InstanceManager
    {
        public static string instancesPath = Path.Combine(AppStaticData.LauncherBasePath, "Instances");
        private readonly Dictionary<string, MinecraftInstance> minecraftInstances = new();
        public MinecraftInstance? selectedInstance;
        public IReadOnlyCollection<MinecraftInstance> Instances => minecraftInstances.Values;

        public InstanceManager()
        {
            LoadInstances();
        }

        private void LoadInstances()
        {
            if (!Directory.Exists(instancesPath))
            {
                Console.WriteLine("Instances directory does not exist.");
                return;
            }

            foreach (string directory in Directory.GetDirectories(instancesPath))
            {
                string instanceFilePath = Path.Combine(directory, "instance.json");

                if (!File.Exists(instanceFilePath))
                {
                    Console.WriteLine($"No instance.json file found in {directory}");
                    continue;
                }

                try
                {
                    var instance = JsonSerializer.Deserialize<MinecraftInstance>(File.ReadAllText(instanceFilePath));
                    if (instance == null) continue;

                    minecraftInstances[instance.Id] = instance;
                    instance.LoadLaunchConfiguration();
                    Console.WriteLine($"Loaded instance: {instance.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading instance from {instanceFilePath}: {ex.Message}");
                }
            }
        }

        public async Task<bool> SelectInstance(string instanceId)
        {
            var versionInfo = await FetchVersionInfo(instanceId);

            if (versionInfo != null)
            {
                var instance = CreateMinecraftInstance(instanceId, versionInfo);
                if (minecraftInstances.TryGetValue(instanceId, out var previous))
                {
                    instance.RestoreInstalledVersion(previous.InstalledVersion);
                }

                instance.SaveToFile();
                minecraftInstances[instanceId] = instance;
                selectedInstance = instance;
                Console.WriteLine($"Selected instance fetched from server: {instance.Name}");
                return true;
            }

            if (minecraftInstances.TryGetValue(instanceId, out var cachedInstance))
            {
                selectedInstance = cachedInstance;
                Console.WriteLine($"Server unavailable, using cached instance: {cachedInstance.Name}");
                return true;
            }

            selectedInstance = null;
            return false;
        }

        public static async Task<VersionInfo?> FetchVersionInfo(string instanceId)
        {
            try
            {
                var json = await SharedHttp.Api.GetStringAsync(AppStaticData.GetVersionInfoURL(instanceId));
                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(json);

                return versionInfo != null && !string.IsNullOrWhiteSpace(versionInfo.modpackName)
                    ? versionInfo
                    : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching version info: {ex.Message}");
                return null;
            }
        }

        // Materializes a working instance from catalog data alone (offline path:
        // the entry carries everything needed for settings/launch config).
        public MinecraftInstance EnsureOfflineInstance(CatalogEntry entry)
        {
            if (minecraftInstances.TryGetValue(entry.Id, out var existing))
            {
                selectedInstance = existing;
                return existing;
            }

            var instance = new MinecraftInstance(
                entry.Id,
                entry.Name,
                entry.MinecraftVersion,
                "",
                entry.ServerAddress,
                MinecraftInstance.DefaultRamMb,
                entry.Loader,
                entry.Version
            );
            instance.LoadLaunchConfiguration();
            instance.SaveToFile();
            minecraftInstances[entry.Id] = instance;
            selectedInstance = instance;
            Console.WriteLine($"Created offline instance from catalog: {instance.Name}");
            return instance;
        }

        public MinecraftInstance CreateMinecraftInstance(string instanceId, VersionInfo versionInfo)
        {
            var instance = new MinecraftInstance(
                instanceId,
                versionInfo.modpackName,
                versionInfo.minecraftVersion,
                versionInfo.forgeVersion,
                versionInfo.serverAddress,
                versionInfo.minRam > 0 ? versionInfo.minRam : MinecraftInstance.DefaultRamMb,
                versionInfo.loader,
                versionInfo.version
            );
            instance.LoadLaunchConfiguration();
            return instance;
        }
    }
}
