using System;
using System.IO;
using System.Linq;

namespace UnrealFrontiersLauncher;

/// <summary>
/// Where the game files live: the "Instances" and "Install" folders sit in <see cref="DataRoot"/>, which is the default launcher
/// folder unless the player chose another one in the settings. The settings file and the logs always stay in the default folder.
/// A new choice is applied at the next start (<see cref="ApplyPendingChange"/>, before anything opens a game file): the two folders
/// are moved to the new place, so nothing is in use while it happens.
/// </summary>
public static class InstallLocation
{
    private static readonly string[] Entries = ["Instances", "Install"];
    private static string? _root;

    /// <summary>The result of the move at this start, for the settings panel; null if there was nothing to report.</summary>
    public static string? LastError { get; private set; }

    /// <summary>The folder with Instances and Install, fixed for the whole session.</summary>
    public static string DataRoot => _root ??= Normalize(Preferences.Preferences.Get().InstallPathApplied) ?? AppStaticData.LauncherBasePath;

    public static string InstancesPath => Path.Combine(DataRoot, "Instances");
    public static string InstallPath => Path.Combine(DataRoot, "Install");

    /// <summary>The folder the player wants (null = the default one).</summary>
    public static string WantedRoot => Normalize(Preferences.Preferences.Get().InstallPath) ?? AppStaticData.LauncherBasePath;

    public static bool HasPendingChange => !SamePath(WantedRoot, DataRoot);

    /// <summary>A problem in Russian for the player, or null if the folder can be used for the game files.</summary>
    public static string? Validate(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            return "Укажите полный путь к папке";
        var full = Path.GetFullPath(path);
        if (Path.GetPathRoot(full)!.Equals(full, StringComparison.OrdinalIgnoreCase))
            return "Выберите папку, а не корень диска";
        if (IsInside(full, DataRoot) && !SamePath(full, DataRoot))
            return "Нельзя выбрать папку внутри текущей папки установки";
        try
        {
            Directory.CreateDirectory(full);
            var probe = Path.Combine(full, $".uf-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return "В эту папку нельзя записывать";
        }
        return null;
    }

    /// <summary>Remembers the choice; null or the default folder means "default". Nothing moves until the next start.</summary>
    public static void Choose(string? path)
    {
        var preferences = Preferences.Preferences.Get();
        var normalized = Normalize(path);
        preferences.InstallPath = normalized is not null && !SamePath(normalized, AppStaticData.LauncherBasePath) ? normalized : null;
        preferences.Save();
    }

    /// <summary>Moves the game folders to the chosen place. Run once at start, before <see cref="DataRoot"/> is used.</summary>
    public static void ApplyPendingChange()
    {
        var preferences = Preferences.Preferences.Get();
        var from = Normalize(preferences.InstallPathApplied) ?? AppStaticData.LauncherBasePath;
        var to = WantedRoot;
        if (SamePath(from, to))
            return;

        var error = MoveEntries(from, to);
        if (error is null)
        {
            preferences.InstallPathApplied = SamePath(to, AppStaticData.LauncherBasePath) ? null : to;
            preferences.Save();
            Console.WriteLine($"Game files moved from {from} to {to}");
        }
        else
        {
            LastError = $"Не удалось перенести данные в «{to}»: {error}. Лаунчер продолжает работать в «{from}».";
            Console.WriteLine(LastError);
        }
    }

    /// <returns>A problem, or null if everything that existed in <paramref name="from"/> is now in <paramref name="to"/>.</returns>
    public static string? MoveEntries(string from, string to)
    {
        try
        {
            Directory.CreateDirectory(to);
            foreach (var name in Entries)
            {
                var source = Path.Combine(from, name);
                var target = Path.Combine(to, name);
                if (!Directory.Exists(source))
                    continue;
                if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
                    return $"в новой папке уже есть непустая «{name}»";
                if (Directory.Exists(target))
                    Directory.Delete(target);

                try
                {
                    Directory.Move(source, target);
                }
                catch (IOException)
                {
                    // Another disk: copy, and delete the source only when the copy is whole.
                    try
                    {
                        CopyDirectory(source, target);
                    }
                    catch
                    {
                        if (Directory.Exists(target))
                            Directory.Delete(target, recursive: true);
                        throw;
                    }
                    Directory.Delete(source, recursive: true);
                }
            }
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (var directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    private static string? Normalize(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool SamePath(string a, string b) =>
        string.Equals(a.TrimEnd(Path.DirectorySeparatorChar), b.TrimEnd(Path.DirectorySeparatorChar), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool IsInside(string path, string folder)
    {
        var prefix = folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
