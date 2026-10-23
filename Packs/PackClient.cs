using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher.Packs;

/// <summary>Reads a pack's manifest from the pack server.</summary>
public static class PackClient
{
    public static async Task<PackManifest> FetchManifestAsync(HttpClient http, string baseUrl, string packId, string? version, CancellationToken ct = default)
    {
        var path = version is null ? $"packs/{Uri.EscapeDataString(packId)}/latest" : $"packs/{Uri.EscapeDataString(packId)}/versions/{Uri.EscapeDataString(version)}";
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var response = await http.GetAsync($"{baseUrl.TrimEnd('/')}/updateclient/{path}", ct);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    throw new PackNotFoundException("Такой сборки нет на сервере или у неё нет выпущенной версии");
                response.EnsureSuccessStatusCode();
                return PackManifest.Parse(await response.Content.ReadAsStringAsync(ct));
            }
            catch (Exception e) when (attempt < 3 && (e is HttpRequestException || (e is TaskCanceledException && !ct.IsCancellationRequested)))
            {
                await Task.Delay(PackInstaller.RetryDelay(e, attempt), ct);
            }
        }
    }
}
