using System;
using System.IO;

namespace UnrealFrontiersLauncher;

public static class LegacyDataMigration
{
    private static readonly string[] MigratedEntries = ["Instances", "Install", "preferences.json"];

    public static void MigrateFromInnoInstall()
    {
        if (!OperatingSystem.IsWindows() || AppStaticData.IsLocalBackend) return;

        var legacyRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unreal Frontiers");
        Migrate(legacyRoot, AppStaticData.LauncherBasePath);
    }

    public static void Migrate(string legacyRoot, string targetRoot)
    {
        if (!Directory.Exists(legacyRoot)) return;
        if (IsRunningFrom(legacyRoot)) return;

        Directory.CreateDirectory(targetRoot);

        foreach (var name in MigratedEntries)
        {
            var source = Path.Combine(legacyRoot, name);
            var target = Path.Combine(targetRoot, name);
            if (Path.Exists(target)) continue;

            try
            {
                if (Directory.Exists(source)) Directory.Move(source, target);
                else if (File.Exists(source)) File.Move(source, target);
                else continue;

                Console.WriteLine($"Migrated {source} to {target}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not migrate {source}: {ex.Message}");
            }
        }
    }

    private static bool IsRunningFrom(string directory)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return AppContext.BaseDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
