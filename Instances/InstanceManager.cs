using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher
{
    public class InstanceManager
    {
        public static string instancesPath = Path.Combine(AppSettings.LauncherBasePath, "Instances");
        Dictionary<string, MinecraftInstance> minecraftInstances = new();
        public MinecraftInstance selectedInstance = new MinecraftInstance("sosal_question_mark", "DefaultProfile", "1.0.0", "localhost:25565", 0);

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

                if (File.Exists(instanceFilePath))
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(instanceFilePath);
                        var instance = JsonSerializer.Deserialize<MinecraftInstance>(jsonContent);

                        if (instance != null)
                        {
                            minecraftInstances.Add(instance.Id, instance);
                            instance.LoadLaunchConfiguration();
                            Console.WriteLine($"Loaded instance: {instance.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading instance from {instanceFilePath}: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"No instance.json file found in {directory}");
                }
            }
        }
        public async Task SelectInstance(string instanceId)
        {
            try
            {
                if (minecraftInstances.ContainsKey(instanceId))
                {
                    selectedInstance = minecraftInstances[instanceId];
                    Console.WriteLine($"Selected instance set from existing: {selectedInstance.Name}");
                }
                else
                {
                    VersionInfo versionInfo = await GetVersionInfo(instanceId);
                    selectedInstance = CreateMinecraftInstance(instanceId, versionInfo);
                    minecraftInstances[instanceId] = selectedInstance;
                    Console.WriteLine($"Selected instance fetched from server: {selectedInstance.Name}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }

        public static async Task<VersionInfo> GetVersionInfo(string instanceId)
        {
            string url = AppSettings.GetVersionInfoURLOld(instanceId);
            Console.WriteLine(url);

            using (var httpClient = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<VersionInfo>(jsonResponse);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching version info: {ex.Message}");
                    return VersionInfo.Default(instanceId);
                }
            }
        }

        public MinecraftInstance CreateMinecraftInstance(string instanceId, VersionInfo versionInfo)
        {
            if (versionInfo == null)
            {
                Console.WriteLine("VersionInfo is null. Cannot create MinecraftInstance.");
                return null;
            }
            var instance = new MinecraftInstance(
                instanceId,
                versionInfo.modpackName,
                versionInfo.minecraftVersion,
                versionInfo.forgeVersion,
                versionInfo.serverAddress,
                versionInfo.minRam
            );
            // TODO: Create instance.json
            instance.LoadLaunchConfiguration();
            return instance;
        }
    }
}
