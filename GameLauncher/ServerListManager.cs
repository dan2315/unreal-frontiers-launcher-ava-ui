using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpNBT;

public class ServerListManager
{
    private MinecraftInstance _minecraftInstance;

    public ServerListManager(MinecraftInstance minecraftInstance) 
    {
        _minecraftInstance = minecraftInstance;
    }

    /// <summary>
    /// Puts the server in the game's list: the pack's own one unless another name and address are given (a network's front
    /// server). An entry the launcher made before, under the pack's or that name, is pointed at the address instead of being
    /// kept beside it, so an old address (a network's servers refuse direct joins) does not linger in the list.
    /// </summary>
    public void EnsureCorrespondingServerEntryExist(string? name = null, string? address = null)
    {
        name ??= _minecraftInstance.Name;
        address ??= _minecraftInstance.Address;
        var serverDatPath = Path.Combine(_minecraftInstance.GetResourcePath(), "servers.dat");

        var servers = new ListTag("servers", TagType.Compound);
        if (File.Exists(serverDatPath))
        {
            try
            {
                using var stream = File.OpenRead(serverDatPath);
                var root = new TagReader(stream, FormatOptions.Java).ReadTag<CompoundTag>();
                if (root.TryGetValue<ListTag>("servers", out var list)) servers = list;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // An unreadable list is replaced, as the game itself would do.
                Console.WriteLine($"servers.dat unreadable, writing a new one: {e.Message}");
            }
        }

        if (!Update(servers, name, address, [name, _minecraftInstance.Name])) return;

        using (var stream = File.Create(serverDatPath))
        {
            Write(stream, servers);
        }

        Console.WriteLine($"servers.dat: '{name}' at {address}.");
    }

    /// <summary>
    /// Writes the list the way the game reads it: a named root compound holding "servers". (SharpNBT leaves out the header
    /// of a compound whose name is null or empty, and the game then refuses the whole file.)
    /// </summary>
    public static void Write(Stream stream, ListTag servers)
    {
        var list = new ListTag("servers", TagType.Compound, servers.Cast<Tag>().ToList());
        var root = new CompoundTag("servers") { list };
        new TagWriter(stream, FormatOptions.Java).WriteTag(root);
    }

    /// <summary>
    /// Makes the list hold one entry for <paramref name="name"/> at <paramref name="address"/>: the first entry named one of
    /// <paramref name="ownNames"/> is renamed and pointed there, the others of those names go. An entry the player made
    /// under another name is left alone; a hidden one the game made for a quick join to the address goes.
    /// </summary>
    /// <returns>Whether the list changed.</returns>
    public static bool Update(ListTag servers, string name, string address, IEnumerable<string> ownNames)
    {
        var own = ownNames.ToHashSet(StringComparer.Ordinal);
        var entries = servers.Cast<CompoundTag>().ToList();
        var ours = entries.Where(server => own.Contains(Text(server, "name"))).ToList();
        var kept = ours.FirstOrDefault();
        var changed = false;

        // By position: SharpNBT's Remove compares tags loosely and can take the wrong one.
        for (var i = servers.Count - 1; i >= 0; i--)
        {
            var server = (CompoundTag)servers[i];
            var duplicate = !ReferenceEquals(server, kept) && ours.Any(extra => ReferenceEquals(extra, server));
            var quickJoin = Hidden(server) && Text(server, "ip") == address;
            if (!duplicate && !quickJoin) continue;
            servers.RemoveAt(i);
            changed = true;
        }

        if (kept == null)
        {
            if (servers.Cast<CompoundTag>().Any(server => Text(server, "ip") == address)) return changed;
            servers.Add(new CompoundTag(null)
            {
                new StringTag("name", name),
                new StringTag("ip", address),
                new StringTag("icon", ""),
                new ByteTag("acceptTextures", 1)
            });
            return true;
        }

        if (Text(kept, "name") != name)
        {
            kept.Remove("name");
            kept.Add(new StringTag("name", name));
            changed = true;
        }
        if (Text(kept, "ip") != address)
        {
            kept.Remove("ip");
            kept.Add(new StringTag("ip", address));
            // The old server's icon is not this one's.
            kept.Remove("icon");
            changed = true;
        }
        return changed;
    }

    private static string? Text(CompoundTag server, string key) =>
        server.TryGetValue<StringTag>(key, out var tag) ? tag.Value : null;

    private static bool Hidden(CompoundTag server) =>
        server.TryGetValue<ByteTag>("hidden", out var tag) && tag.Value != 0;
}
