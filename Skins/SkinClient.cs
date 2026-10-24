using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher;

/// <summary>The player's active skin: its id (skins never change under one id) and the path of its PNG.</summary>
public sealed record ActiveSkin(int Id, string PreviewUrl);

public static class SkinClient
{
    /// <summary>
    /// GET /skins/my with the player's token. Like the news feed, the production site sends only some prefixes to the API,
    /// so behind https it is /api/skins/my and a bare API address has it at /skins/my.
    /// </summary>
    /// <returns>The active skin, or null when none is chosen (or it has no picture) or the server cannot be reached.</returns>
    public static async Task<ActiveSkin?> FetchActiveAsync(HttpClient http, string baseUrl, string accessToken, CancellationToken ct = default)
    {
        foreach (var url in Candidates(baseUrl, "/skins/my"))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using var response = await http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) continue;
                var body = await response.Content.ReadAsStringAsync(ct);
                if (LooksLikeJson(body)) return ParseActive(body);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                Console.WriteLine($"Skin list unavailable at {url}: {e.Message}");
            }
        }
        return null;
    }

    /// <summary>GET {base}{previewUrl}: the skin PNG, no token needed.</summary>
    public static async Task<byte[]?> FetchPreviewAsync(HttpClient http, string baseUrl, string previewUrl, CancellationToken ct = default)
    {
        foreach (var url in Candidates(baseUrl, previewUrl))
        {
            try
            {
                var bytes = await http.GetByteArrayAsync(url, ct);
                if (IsPng(bytes)) return bytes;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                Console.WriteLine($"Skin picture unavailable at {url}: {e.Message}");
            }
        }
        return null;
    }

    public static IEnumerable<string> Candidates(string baseUrl, string path)
    {
        var root = baseUrl.TrimEnd('/');
        path = path.StartsWith('/') ? path : "/" + path;
        var prefixes = root.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? new[] { "/api", "" } : ["", "/api"];
        return prefixes.Select(prefix => root + prefix + path);
    }

    /// <returns>The active skin (the list has it first, isActive=true), or null for no skin / no picture / not a skin list.</returns>
    public static ActiveSkin? ParseActive(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("isActive", out var active) || active.ValueKind != JsonValueKind.True)
                    continue;

                return item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out var number)
                       && item.TryGetProperty("previewUrl", out var preview) && preview.ValueKind == JsonValueKind.String
                       && !string.IsNullOrWhiteSpace(preview.GetString())
                    ? new ActiveSkin(number, preview.GetString()!)
                    : null;
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsPng(byte[] bytes) =>
        bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;

    /// <summary>The cached skin PNG of one skin id (a new skin is a new id, so the file never goes stale).</summary>
    public static string CachePath(string root, int id) => Path.Combine(root, "skins", $"{id}.png");

    private static bool LooksLikeJson(string body) => body.AsSpan().TrimStart().StartsWith("{");
}
