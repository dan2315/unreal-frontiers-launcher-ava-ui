using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace UnrealFrontiersLauncher.Packs;

/// <summary>
/// What the launcher put into an instance (<c>.uf-installed.json</c> in the instance folder): path -> SHA-256, size and the file's
/// modification time when it was verified. It lets an update remove exactly the files an older version installed and that the new one
/// no longer lists, and lets a launch skip hashing files that have not changed since.
/// </summary>
public sealed class InstalledState
{
    public const string FileName = ".uf-installed.json";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public string? Pack { get; set; }
    public string? Version { get; set; }
    public Dictionary<string, InstalledFile> Files { get; set; } = new(StringComparer.Ordinal);

    public sealed record InstalledFile(string Sha256, long Size, long MTimeTicks);

    public static InstalledState Load(string instanceFolder)
    {
        var path = Path.Combine(instanceFolder, FileName);
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<InstalledState>(File.ReadAllText(path), Options) ?? new() : new();
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            // A damaged state only costs a re-check of the files.
            return new();
        }
    }

    public void Save(string instanceFolder)
    {
        Directory.CreateDirectory(instanceFolder);
        var path = Path.Combine(instanceFolder, FileName);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Options));
        File.Move(temp, path, overwrite: true);
    }
}
