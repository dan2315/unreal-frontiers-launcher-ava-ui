using System;
using System.IO;
using System.Text.Json;

namespace UnrealFrontiersLauncher.Preferences;

public class Preferences(string lastSelectedModpack)
{
    private const string DefaultModpack = "create";
    private static String _path = Path.Combine(AppStaticData.LauncherBasePath, "preferences.json");

    public string LastSelectedModpack { get; set; } = lastSelectedModpack;

    // Global launch behaviour (applies to every modpack).
    public bool NeedToClose { get; set; } = true;
    public bool NeedToOpenConsole { get; set; } = false;

    // UI animation switches (see Utils/UiAnimations.cs for the full list of effects).
    // All on by default; turning them off kills running animations and skips transitions.
    public bool UiAnimationsEnabled { get; set; } = true;
    public bool UiButtonGlowEnabled { get; set; } = true;
    public bool UiScreenTransitionsEnabled { get; set; } = true;
    public bool UiOverlayFadeEnabled { get; set; } = true;
    public bool UiBackgroundPulseEnabled { get; set; } = true;

    private static Preferences? _instance;
    public static Preferences Get()
    {
        if (_instance == null)
        {
            _instance = Load();
        }
        return _instance;
    }

    public void SetSelectedModpack(string modpack)
    {
        LastSelectedModpack = modpack;
    }
    
    public void Save()
    {
        try
        {
            JsonSerializerOptions options = new JsonSerializerOptions()
            {
                WriteIndented = true,
            };
            var json = JsonSerializer.Serialize(this, options);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, json);
        }
        catch (Exception e)
        {
            Console.WriteLine("Failed saving preferences: " + e);
        }
        
    }

    public static Preferences Load()
    {
        if (!File.Exists(_path)) return new Preferences(DefaultModpack);

        try
        {
            return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(_path)) ?? new Preferences(DefaultModpack);
        }
        catch (Exception e)
        {
            Console.WriteLine("Failed loading preferences: " + e);
            return new Preferences(DefaultModpack);
        }
    }
}