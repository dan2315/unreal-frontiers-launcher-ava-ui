using System;
using System.IO;
using UnrealFrontiersLauncher.Platforms;

namespace UnrealFrontiersLauncher.Linux
{
    public class SystemInfo : ISystemInfo
    {
        public (ulong value, string richValue) GetRamInfo()
        {
            try
            {
                string[] memInfoLines = File.ReadAllLines("/proc/meminfo");
                ulong totalMemKb = 0;

                foreach (string line in memInfoLines)
                {
                    if (line.StartsWith("MemTotal:"))
                    {
                        // Extract the total memory value in kB
                        string memValue = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1];
                        totalMemKb = ulong.Parse(memValue);
                        break;
                    }
                }

                // Convert kB to bytes
                ulong totalMemBytes = totalMemKb * 1024;
                string richValue = $"{totalMemBytes / (1024 * 1024 * 1024)} GB";

                return (totalMemBytes, richValue);
            }
            catch (Exception ex)
            {
                return (0, $"Error retrieving RAM info: {ex.Message}");
            }
        }
    }
}