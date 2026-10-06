using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnrealFrontiersLauncher;

public class MinecraftInstance
{
    private string _id;
    private string _name;
    private string _gameVersion;
    private string _forgeVersion;
    private string _serverIp;
    private string _serverPort;
    [JsonIgnore] private LaunchConfiguration _launchConfiguration;

    public string Id => _id;
    public string Name => _name;
    public string GameVersion => _gameVersion;
    public string ForgeVersion => _forgeVersion;
    public string ServerIp => _serverIp;
    public string ServerPort => _serverPort;
    public string Address => _serverPort.Equals(String.Empty) ? _serverIp : _serverIp + ":" + _serverPort;

    public MinecraftInstance(string instanceId, string instanceName, string gameVersion, string forgeVersion, string serverAddress, int defaultRamValue)
    {
        _id = instanceId;
        _name = instanceName;
        _gameVersion = gameVersion;
        _forgeVersion = forgeVersion;

        var splitedAddress = serverAddress.Split(":");
        _serverIp = splitedAddress[0];
        if (splitedAddress.Length > 1)
        {
            _serverPort = splitedAddress[1];
        }
        else
        {
            _serverPort = String.Empty;
        }

        _launchConfiguration = new (defaultRamValue);
    }

    public MinecraftInstance(string instanceId, string instanceName, string gameVersion, string serverAddress, int defaultRamValue)
        : this(instanceId, instanceName, gameVersion, "none", serverAddress, defaultRamValue) { }

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
        var path = Path.Combine(AppStaticData.LauncherBasePath, "Instances", _name);
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
                Ram = _launchConfiguration.Ram,
                NeedToClose = true,
                NeedToOpenConsole = false
            };
            SaveLaunchConfiguration();
            return;
        }

        try
        {
            var jsonData = File.ReadAllText(path);

            _launchConfiguration = JsonSerializer.Deserialize<LaunchConfiguration>(jsonData);

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
                Ram = 8128, // Fallback to default value in MB
                NeedToClose = true,
                NeedToOpenConsole = false
            };
        }
    }

    public LaunchConfiguration GetLaunchConfiguration() => _launchConfiguration;

}