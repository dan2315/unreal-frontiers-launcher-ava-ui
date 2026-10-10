using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher;

/// <summary>
/// The modpacks the server publishes (GET /updateclient/modpacks: the version.json of each). The last
/// list is kept in modpacks.json, so the launcher starts with it before the server answers, or offline.
/// </summary>
public static class ModpackCatalog
{
    private static readonly string CachePath = Path.Combine(AppStaticData.LauncherBasePath, "modpacks.json");

    /// <summary>Shown when neither the server nor the cache has a list (first start offline).</summary>
    public static readonly IReadOnlyList<VersionInfo> Builtin =
    [
        new() { modpackId = "create", modpackName = "Unreal Domains: Create" },
        new() { modpackId = "monifactory", modpackName = "Monifactory" },
        new() { modpackId = "zaruba", modpackName = "Unreal Zaruba" },
    ];

    public static IReadOnlyList<VersionInfo> LoadCached()
    {
        try
        {
            if (File.Exists(CachePath) && Usable(JsonSerializer.Deserialize<List<VersionInfo>>(File.ReadAllText(CachePath))) is { Count: > 0 } cached)
                return cached;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Ignoring the modpack list cache: {e.Message}");
        }
        return Builtin;
    }

    /// <returns>The server's list, or null if it could not be fetched or is empty.</returns>
    public static async Task<IReadOnlyList<VersionInfo>?> FetchAsync()
    {
        try
        {
            var json = await SharedHttp.Api.GetStringAsync(AppStaticData.GetModpackListURL());
            var modpacks = Usable(JsonSerializer.Deserialize<List<VersionInfo>>(json));
            if (modpacks.Count == 0)
            {
                Console.WriteLine("The server lists no modpacks; keeping the current list");
                return null;
            }
            Save(modpacks);
            return modpacks;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error fetching the modpack list: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Modpacks the launcher can open: a selected modpack is loaded through get_version_data, which
    /// InstanceManager rejects without a modpackName.
    /// </summary>
    public static List<VersionInfo> Usable(IEnumerable<VersionInfo>? modpacks) =>
        (modpacks ?? [])
        .Where(m => !string.IsNullOrWhiteSpace(m.modpackId) && !string.IsNullOrWhiteSpace(m.modpackName))
        .DistinctBy(m => m.modpackId)
        .ToList();

    private static void Save(IReadOnlyList<VersionInfo> modpacks)
    {
        try
        {
            Directory.CreateDirectory(AppStaticData.LauncherBasePath);
            File.WriteAllText(CachePath, JsonSerializer.Serialize(modpacks));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Could not cache the modpack list: {e.Message}");
        }
    }
}
