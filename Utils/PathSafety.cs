using System;
using System.IO;
using System.Linq;

namespace UnrealFrontiersLauncher;

public static class PathSafety
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string ResolveInside(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        var rootWithSeparator = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, PathComparison))
        {
            throw new InvalidOperationException($"Path '{relativePath}' escapes '{root}'.");
        }

        return fullPath;
    }

    public static string SanitizeFileName(string name)
    {
        var cleaned = new string(name.Select(c => IsInvalidInFileName(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return cleaned.Length == 0 ? "_" : cleaned;
    }

    // The Windows set on every system, so a folder gets the same name everywhere. On Linux ':' would be legal, but it is
    // the Java classpath separator and breaks a game directory that contains it.
    private static bool IsInvalidInFileName(char c) => c < ' ' || "<>:\"/\\|?*".Contains(c);
}
