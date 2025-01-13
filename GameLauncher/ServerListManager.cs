using System;
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

    public void EnsureCorrespondingServerEntryExist()
    {
        var serverDatPath = Path.Combine(_minecraftInstance.GetResourcePath(), "servers.dat");

        CompoundTag root;

        if (File.Exists(serverDatPath))
        {
            using (var stream = File.OpenRead(serverDatPath))
            {
                var reader = new TagReader(stream, FormatOptions.Java);
                root = reader.ReadTag<CompoundTag>();
            }
        }
        else
        {
            root = new CompoundTag("servers")
            {
                new ListTag("servers", TagType.Compound)
            };
        }

        var serversList = root.Get<ListTag>("servers");
        var existingServer = serversList
            .Cast<CompoundTag>()
            .FirstOrDefault(server => server.Get<StringTag>("ip")?.Value == _minecraftInstance.Address);

        if (existingServer != null)
        {
            return; 
        }

        // Create a new server entry
        var newServer = new CompoundTag(null)
        {
            new StringTag("name", _minecraftInstance.Name),
            new StringTag("ip", _minecraftInstance.Address),
            new StringTag("icon", ""), 
            new ByteTag("acceptTextures", 1) 
        };

        // Add the new server entry to the list
        serversList.Add(newServer);

        // Write the updated data back to servers.dat
        using (var stream = File.Create(serverDatPath))
        {
            var writer = new TagWriter(stream, FormatOptions.Java);
            writer.WriteTag(root);
        }

        Console.WriteLine($"Added server '{_minecraftInstance.Name}' to servers.dat.");
    }
}
