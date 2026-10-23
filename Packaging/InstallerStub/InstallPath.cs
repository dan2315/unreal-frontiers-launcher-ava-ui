using System;
using System.IO;
using System.Linq;

namespace UnrealFrontiersInstaller;

/// <summary>
/// Where the launcher really goes for the folder the user picked. Velopack removes the whole install folder when the app is
/// uninstalled, so a folder that already holds something else (D:\Games, the Desktop) must never be the install folder itself:
/// the launcher goes into a subfolder of it.
/// </summary>
public static class InstallPath
{
    public static string Resolve(string chosen, string subfolder)
    {
        if (string.IsNullOrWhiteSpace(chosen)) throw new ArgumentException("The path is empty.", nameof(chosen));

        var full = Path.GetFullPath(chosen.Trim().Trim('"'));
        var root = Path.GetPathRoot(full);
        if (root != null && string.Equals(full.TrimEnd('\\', '/'), root.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            return Path.Combine(full, subfolder);

        if (!Directory.Exists(full) || IsEmpty(full) || IsOurInstall(full)) return full;

        // Already ends with the subfolder name (the user typed it): the subfolder of a subfolder would be silly, but the folder holds foreign files.
        return Path.Combine(full, subfolder);
    }

    private static bool IsEmpty(string folder) => !Directory.EnumerateFileSystemEntries(folder).Any();

    /// <summary>A Velopack install: Update.exe next to the "current" folder.</summary>
    public static bool IsOurInstall(string folder) =>
        File.Exists(Path.Combine(folder, "Update.exe")) && Directory.Exists(Path.Combine(folder, "current"));
}
