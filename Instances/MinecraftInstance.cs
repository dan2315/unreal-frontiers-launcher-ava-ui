using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnrealFrontiersLauncher;

public class MinecraftInstance
{
    public const int DefaultRamMb = 4096;
    public const int MinRamMb = 1024;

    private string _id;
    private string _name;
    private string _gameVersion;
    private string _forgeVersion;
    private string _loader;
    private string _serverIp;
    private string _serverPort;
    [JsonIgnore] private LaunchConfiguration _launchConfiguration;

    public string Id => _id;
    public string Name => _name;
    public string GameVersion => _gameVersion;
    public string ForgeVersion => _forgeVersion;
    public string Loader => _loader;
    public string ServerIp => _serverIp;
    public string ServerPort => _serverPort;
    public string Address => _serverPort.Length == 0 ? _serverIp : $"{_serverIp}:{_serverPort}";

    public MinecraftInstance(string instanceId, string instanceName, string gameVersion, string forgeVersion, string serverAddress, int defaultRamValue, string? loader = null)
    {
        _id = instanceId;
        _name = instanceName;
        _gameVersion = gameVersion;
        _forgeVersion = forgeVersion;
        _loader = ModLoader.Resolve(loader, gameVersion, forgeVersion);

        (_serverIp, _serverPort) = SplitAddress(serverAddress);
        _launchConfiguration = new (defaultRamValue);
    }

    [JsonConstructor]
    public MinecraftInstance(string id, string name, string gameVersion, string forgeVersion, string serverIp, string serverPort, string? loader = null)
    {
        _id = id;
        _name = name;
        _gameVersion = gameVersion;
        _forgeVersion = forgeVersion;
        _loader = ModLoader.Resolve(loader, gameVersion, forgeVersion);
        _serverIp = serverIp;
        _serverPort = serverPort;
        _launchConfiguration = new (DefaultRamMb);
    }

    public static (string Ip, string Port) SplitAddress(string? address)
    {
        var parts = (address ?? string.Empty).Split(':', 2);
        return (parts[0], parts.Length > 1 ? parts[1] : string.Empty);
    }

    public void SaveToFile()
    {
        var path = Path.Combine(GetResourcePath(), "instance.json");
        Directory.CreateDirectory(GetResourcePath());

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var jsonData = JsonSerializer.Serialize(this, jsonOptions);

        File.WriteAllText(path, jsonData);
    }

    public string GetResourcePath()
    {
        var path = Path.Combine(AppStaticData.LauncherBasePath, "Instances", PathSafety.SanitizeFileName(_name));
        return path;
    }

    public void ApplyNewRamValue(int value)
    {
        _launchConfiguration.Ram = value;
        SaveLaunchConfiguration();
    }

    public void ApplyNewShouldCloseValue(bool value)
    {
        _launchConfiguration.NeedToClose = value;
        SaveLaunchConfiguration();
    }

    public void ApplyNewShouldOpenConsoleValue(bool value)
    {
        _launchConfiguration.NeedToOpenConsole = value;
        SaveLaunchConfiguration();
    }

    public void SaveLaunchConfiguration()
    {
        var path = Path.Combine(GetResourcePath(), "launch.json");

        Directory.CreateDirectory(GetResourcePath());

        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var jsonData = JsonSerializer.Serialize(_launchConfiguration, jsonOptions);

            File.WriteAllText(path, jsonData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
        }
    }


    public void LoadLaunchConfiguration()
    {
        var path = Path.Combine(GetResourcePath(), "launch.json");

        if (!File.Exists(path))
        {
            Console.WriteLine("Configuration file not found. Using default settings.");
            _launchConfiguration = new LaunchConfiguration
            {
                Ram = _launchConfiguration.Ram > 0 ? _launchConfiguration.Ram : DefaultRamMb,
                NeedToClose = true,
                NeedToOpenConsole = false
            };
            SaveLaunchConfiguration();
            return;
        }

        try
        {
            var jsonData = File.ReadAllText(path);

            _launchConfiguration = JsonSerializer.Deserialize<LaunchConfiguration>(jsonData)!;

            if (_launchConfiguration == null)
            {
                throw new Exception("Failed to deserialize LaunchConfiguration. File might be corrupted.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}");
            _launchConfiguration = new LaunchConfiguration
            {
                Ram = DefaultRamMb,
                NeedToClose = true,
                NeedToOpenConsole = false
            };
        }
    }

    public LaunchConfiguration GetLaunchConfiguration() => _launchConfiguration;

}