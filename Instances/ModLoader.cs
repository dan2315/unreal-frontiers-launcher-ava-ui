using System;

namespace UnrealFrontiersLauncher;

public static class ModLoader
{
    public const string Forge = "forge";
    public const string NeoForge = "neoforge";

    public static string Resolve(string? explicitLoader, string gameVersion, string loaderVersion)
    {
        var normalized = explicitLoader?.Trim().ToLowerInvariant().Replace("_", "").Replace("-", "");
        if (normalized == NeoForge) return NeoForge;
        if (normalized == Forge) return Forge;

        return LooksLikeNeoForge(gameVersion, loaderVersion) ? NeoForge : Forge;
    }

    // NeoForge versions are "<minecraft minor>.<minecraft patch>.<build>", e.g. 21.1.252 for Minecraft 1.21.1.
    private static bool LooksLikeNeoForge(string gameVersion, string loaderVersion)
    {
        var parts = gameVersion.Split('.');
        if (parts.Length < 2 || parts[0] != "1") return false;

        var patch = parts.Length > 2 ? parts[2] : "0";
        return loaderVersion.StartsWith($"{parts[1]}.{patch}.", StringComparison.Ordinal);
    }
}
