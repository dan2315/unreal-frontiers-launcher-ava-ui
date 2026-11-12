using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher;

/// <summary>One server of a network. Players never join it directly: the front server decides who goes where.</summary>
public sealed record GroupServer(
    string Id, string Name, string Description, string Kind, string? Color,
    bool Up, int Online, int MaxPlayers);

/// <summary>
/// A network of servers on one pack with one entry point (the front server at <see cref="Address"/>), which sends the
/// player to the server they were on last. <see cref="Online"/> and <see cref="MaxPlayers"/> are the front server's own
/// (its limit need not be the sum of the servers' limits); a limit of 0 means the backend does not give one.
/// </summary>
public sealed record ServerGroup(
    string Id, string Name, string Description, string? Color, string Host, int Port, int Online, int MaxPlayers,
    string? PrimaryServerId, string? PackId, IReadOnlyList<GroupServer> Servers)
{
    public string Address => Port > 0 ? $"{Host}:{Port}" : Host;
}

public static class ServerGroups
{
    /// <summary>GET /server-management/groups.</summary>
    /// <returns>The groups, or null when the server cannot be reached or does not know about groups yet.</returns>
    public static async Task<IReadOnlyList<ServerGroup>?> FetchAsync(HttpClient http, string baseUrl, CancellationToken ct = default)
    {
        var body = await GetAsync(http, baseUrl.TrimEnd('/') + "/server-management/groups", ct);
        return body == null ? null : Parse(body);
    }

    private static async Task<string?> GetAsync(HttpClient http, string url, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(url, ct);
            if (response.IsSuccessStatusCode) return await response.Content.ReadAsStringAsync(ct);
            Console.WriteLine($"{url}: HTTP {(int)response.StatusCode}");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            Console.WriteLine($"{url} unavailable: {e.Message}");
        }
        return null;
    }

    /// <returns>The groups with an id, a name and an address to connect to; null when the body is not a list.</returns>
    public static IReadOnlyList<ServerGroup>? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return null;

            var groups = new List<ServerGroup>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var id = Text(item, "id");
                var host = Text(item, "host");
                if (id.Length == 0 || host.Length == 0) continue;

                var servers = new List<GroupServer>();
                if (item.TryGetProperty("servers", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var server in list.EnumerateArray())
                    {
                        if (server.ValueKind != JsonValueKind.Object || Text(server, "id").Length == 0) continue;
                        servers.Add(new GroupServer(
                            Text(server, "id"),
                            OrElse(Text(server, "name"), Text(server, "id")),
                            Text(server, "description"),
                            Text(server, "kind"),
                            NullIfEmpty(Text(server, "color")),
                            Flag(server, "up"),
                            Number(server, "online"),
                            Number(server, "maxPlayers")));
                    }
                }

                groups.Add(new ServerGroup(
                    id,
                    OrElse(Text(item, "name"), id),
                    Text(item, "description"),
                    NullIfEmpty(Text(item, "color")),
                    host,
                    Number(item, "port"),
                    Number(item, "online"),
                    Number(item, "maxPlayers"),
                    NullIfEmpty(Text(item, "primaryServerId")),
                    NullIfEmpty(Text(item, "packId")),
                    servers));
            }
            return groups;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The group a pack belongs to: by its packId, or (when the backend has no pack for it) by its primary server.</summary>
    public static ServerGroup? ForPack(IEnumerable<ServerGroup>? groups, string packId) =>
        groups?.FirstOrDefault(group => group.PackId == packId)
        ?? groups?.FirstOrDefault(group => group.PackId == null && group.PrimaryServerId == packId);

    private static string Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : "";

    private static int Number(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;

    private static bool Flag(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string OrElse(string value, string fallback) => value.Length > 0 ? value : fallback;
    private static string? NullIfEmpty(string value) => value.Length > 0 ? value : null;
}
