using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace UnrealFrontiersLauncher.Packs;

/// <summary>One version of a modpack as the server publishes it (schema 1): the game and the files by SHA-256.</summary>
public sealed record PackManifest(
    int Schema,
    string Pack,
    string Name,
    string Version,
    PackGame Game,
    int MinRam,
    string? ServerAddress,
    string? Image,
    List<PackFile> Files)
{
    /// <summary>The newest schema this launcher understands; a manifest with a higher one asks for an update.</summary>
    public const int SupportedSchema = 1;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { MaxDepth = 16 };

    public static PackManifest Parse(string json)
    {
        PackManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PackManifest>(json, Options);
        }
        catch (JsonException e)
        {
            throw new PackException($"Манифест сборки повреждён: {e.Message}");
        }
        if (manifest is null || manifest.Files is null || manifest.Game is null)
            throw new PackException("Манифест сборки пуст");
        if (manifest.Schema > SupportedSchema)
            throw new PackException("Сборка опубликована в новом формате: обновите лаунчер");
        if (manifest.Schema < 1)
            throw new PackException("Манифест сборки не распознан");
        return manifest;
    }

    /// <summary>The files this player needs: everything but what is only for the game server.</summary>
    public IEnumerable<PackFile> ClientFiles => Files.Where(f => f.Side != "server");
}

public sealed record PackGame(string Minecraft, string Loader, string? LoaderVersion);

/// <param name="Path">Relative to the instance folder, '/' separated.</param>
/// <param name="Side"><c>both</c>, <c>client</c> or <c>server</c> (never installed here).</param>
/// <param name="Overwrite"><c>always</c>: the file is made equal to the manifest; <c>once</c>: installed only if missing, then the player's.</param>
/// <param name="Urls">Extra sources, tried after the pack server. Whatever the source, the SHA-256 must match.</param>
public sealed record PackFile(string Path, string Sha256, long Size, string Side = "both", string Overwrite = "always", List<string>? Urls = null);

/// <summary>A problem to show the player (in Russian) instead of a stack trace.</summary>
public class PackException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The pack is not on the server (or has no released version).</summary>
public sealed class PackNotFoundException(string message) : PackException(message);

/// <summary>A pack as the list of packs shows it: the latest version without its files.</summary>
public sealed record PackSummary(
    string Id, string Name, string Version, PackGame Game, int MinRam, string? ServerAddress, string? Image);
