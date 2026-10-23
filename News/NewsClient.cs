using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher;

/// <summary>One news post of a server, as the backend sends it (plain text, never markup).</summary>
public sealed record NewsItem(int Id, string ServerId, string Title, string Body, bool Pinned, string Author, DateTimeOffset? CreatedAt);

/// <summary>A news post ready to show.</summary>
public sealed record NewsEntry(string Title, string Body, string DateText, bool Pinned)
{
    public string Marker => Pinned ? "★ " : "";
}

public static class NewsClient
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>
    /// GET /news?serverId=..&amp;page=1&amp;pageSize=..: the public news feed. The production site sends only some prefixes to the
    /// API, so behind https the feed is /api/news; a bare API address (http://host:3200) has it at /news. The other one is the fallback.
    /// </summary>
    /// <returns>The posts (pinned first), or null when the server cannot be reached or answers something else.</returns>
    public static async Task<IReadOnlyList<NewsItem>?> FetchAsync(
        HttpClient http, string baseUrl, string? serverId, int pageSize = 10, CancellationToken ct = default)
    {
        foreach (var url in Candidates(baseUrl, serverId, pageSize))
        {
            try
            {
                using var response = await http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) continue;
                if (Parse(await response.Content.ReadAsStringAsync(ct)) is { } items) return items;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                Console.WriteLine($"News unavailable at {url}: {e.Message}");
            }
        }
        return null;
    }

    public static IEnumerable<string> Candidates(string baseUrl, string? serverId, int pageSize)
    {
        var root = baseUrl.TrimEnd('/');
        var query = $"?page=1&pageSize={pageSize}" + (string.IsNullOrEmpty(serverId) ? "" : $"&serverId={Uri.EscapeDataString(serverId)}");
        var prefixes = root.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? new[] { "/api/news", "/news" } : ["/news", "/api/news"];
        return prefixes.Select(prefix => root + prefix + query);
    }

    /// <returns>The posts, or null when the body is not a news page (for example the site's HTML).</returns>
    public static IReadOnlyList<NewsItem>? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Array)
                return null;

            var result = new List<NewsItem>();
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var title = Text(item, "title");
                if (title.Length == 0) continue;

                result.Add(new NewsItem(
                    item.TryGetProperty("id", out var id) && id.TryGetInt32(out var number) ? number : 0,
                    Text(item, "serverId"),
                    title,
                    Text(item, "body"),
                    item.TryGetProperty("pinned", out var pinned) && pinned.ValueKind == JsonValueKind.True,
                    Text(item, "author"),
                    DateTimeOffset.TryParse(Text(item, "createdAt"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var created) ? created : null));
            }
            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static NewsEntry ToEntry(NewsItem item, DateTimeOffset now, TimeZoneInfo? zone = null) =>
        new(item.Title.Trim(), item.Body.Trim(), FormatDate(item.CreatedAt, now, zone ?? TimeZoneInfo.Local), item.Pinned);

    /// <summary>"12 окт" in the local time zone, with the year when it is not this year's.</summary>
    public static string FormatDate(DateTimeOffset? date, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (date is not { } value) return "";

        var local = TimeZoneInfo.ConvertTime(value, zone);
        var format = local.Year == TimeZoneInfo.ConvertTime(now, zone).Year ? "d MMM" : "d MMM yyyy";
        return local.ToString(format, Russian).Replace(".", "");
    }

    private static string Text(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
}
