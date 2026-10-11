using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher;

public enum ModpackStatus
{
    NotInstalled,
    Installed,
    UpdateAvailable,
    RemovedFromServer,
    ServerUnavailable
}

public record CatalogEntry(
    string Id,
    string Name,
    string MinecraftVersion,
    string Loader,
    string Version,
    string ServerAddress,
    ModpackStatus Status,
    string ImageUrl = "");

public static class ModpackCatalog
{
    // Shown when the server list cannot be loaded and nothing is installed yet.
    public static readonly IReadOnlyList<CatalogEntry> BuiltIn =
    [
        new("create", "Unreal Domains: Create", "1.20.1", ModLoader.Forge, "", "", ModpackStatus.ServerUnavailable),
        new("monifactory", "Monifactory", "1.20.1", ModLoader.Forge, "", "", ModpackStatus.ServerUnavailable),
        new("zaruba", "Unreal Zaruba", "1.21.1", ModLoader.NeoForge, "", "", ModpackStatus.ServerUnavailable),
    ];

    public static async Task<List<VersionInfo>?> FetchRemoteAsync(string url, HttpClient client, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Modpack list unavailable: HTTP {(int)response.StatusCode}");
                return null;
            }

            return Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Console.WriteLine($"Modpack list unavailable: {ex.Message}");
            return null;
        }
    }

    public static List<VersionInfo> Parse(string json)
    {
        var all = JsonSerializer.Deserialize<List<VersionInfo?>>(json) ?? [];

        return all
            .Where(info => info != null && !string.IsNullOrWhiteSpace(info.modpackId))
            .Select(info => info!)
            .GroupBy(info => info.modpackId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    public static List<CatalogEntry> Merge(IReadOnlyList<VersionInfo>? remote, IEnumerable<MinecraftInstance> local)
    {
        var installed = local
            .Where(instance => instance.IsInstalled)
            .GroupBy(instance => instance.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        if (remote == null)
        {
            return MergeWhenServerUnavailable(installed);
        }

        var entries = new List<CatalogEntry>();
        foreach (var info in remote)
        {
            installed.TryGetValue(info.modpackId, out var instance);
            entries.Add(ToEntry(info, instance));
        }

        var listed = remote.Select(info => info.modpackId).ToHashSet(StringComparer.Ordinal);
        entries.AddRange(installed.Values
            .Where(instance => !listed.Contains(instance.Id))
            .OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase)
            .Select(instance => FromInstance(instance, ModpackStatus.RemovedFromServer)));

        return entries;
    }

    private static List<CatalogEntry> MergeWhenServerUnavailable(Dictionary<string, MinecraftInstance> installed)
    {
        var entries = installed.Values
            .OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase)
            .Select(instance => FromInstance(instance, ModpackStatus.Installed))
            .ToList();

        entries.AddRange(BuiltIn.Where(entry => !installed.ContainsKey(entry.Id)));
        return entries;
    }

    private static CatalogEntry ToEntry(VersionInfo info, MinecraftInstance? installed)
    {
        var name = string.IsNullOrWhiteSpace(info.modpackName) ? info.modpackId : info.modpackName;

        var status = installed == null ? ModpackStatus.NotInstalled
            : IsOutdated(installed.InstalledVersion, info.version) ? ModpackStatus.UpdateAvailable
            : ModpackStatus.Installed;

        return new CatalogEntry(
            info.modpackId,
            name,
            info.minecraftVersion,
            ModLoader.Resolve(info.loader, info.minecraftVersion, info.forgeVersion),
            info.version,
            info.serverAddress,
            status,
            info.imageURl);
    }

    private static CatalogEntry FromInstance(MinecraftInstance instance, ModpackStatus status) =>
        new(instance.Id, instance.Name, instance.GameVersion, instance.Loader, instance.PackVersion, instance.Address, status);

    private static bool IsOutdated(string? installedVersion, string serverVersion) =>
        !string.IsNullOrEmpty(installedVersion)
        && installedVersion != "unknown"
        && !string.IsNullOrEmpty(serverVersion)
        && installedVersion != serverVersion;
}
