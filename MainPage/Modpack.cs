using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;

namespace UnrealFrontiersLauncher;

public class Modpack : INotifyPropertyChanged
{
    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public Bitmap Icon { get; private set; }
    private string _value;
    public string Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                Icon = BundledIcon(_value);
                OnPropertyChanged(nameof(Icon));
            }
        }
    }

    /// <summary>A modpack of the server list: its name, and the icon from imageURl when that is a URL.</summary>
    public static Modpack From(VersionInfo info)
    {
        var modpack = new Modpack { Name = info.modpackName, Value = info.modpackId };
        _ = modpack.LoadIconAsync(info.imageURl);
        return modpack;
    }

    public void Update(VersionInfo info)
    {
        Name = info.modpackName;
        _ = LoadIconAsync(info.imageURl);
    }

    private string? _iconUrl;

    /// <summary>Replaces the bundled icon with the one at <paramref name="url"/> ("none" or not a URL: keeps it).</summary>
    private async Task LoadIconAsync(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || url == _iconUrl)
            return;
        _iconUrl = url;
        try
        {
            using var stream = new System.IO.MemoryStream(await SharedHttp.Api.GetByteArrayAsync(uri));
            Icon = new Bitmap(stream);
            OnPropertyChanged(nameof(Icon));
        }
        catch (Exception e)
        {
            _iconUrl = null;
            Console.WriteLine($"Could not load the icon of {Value} from {url}: {e.Message}");
        }
    }

    /// <summary>Assets/Images/ModpackIcons/{id}_icon.png, or the launcher logo for modpacks without one.</summary>
    private static Bitmap BundledIcon(string id)
    {
        var icon = new Uri($"avares://UnrealFrontiersLauncher/Assets/Images/ModpackIcons/{id}_icon.png");
        return new Bitmap(AssetLoader.Open(AssetLoader.Exists(icon)
            ? icon
            : new Uri("avares://UnrealFrontiersLauncher/Assets/Images/logo_unrealfrontiers.png")));
    }

    private int _currentPlayers;
    public int CurrentPlayers
    {
        get => _currentPlayers;
        set
        {
            if (_currentPlayers != value)
            {
                _currentPlayers = value;
                OnPropertyChanged(nameof(CurrentPlayers));
                OnPropertyChanged(nameof(PlayerCount));
                OnPropertyChanged(nameof(PlayerCountProgress));
            }
        }
    }

    private int _maxPlayers;
    public int MaxPlayers
    {
        get => _maxPlayers;
        set
        {
            if (_maxPlayers != value)
            {
                _maxPlayers = value;
                OnPropertyChanged(nameof(MaxPlayers));
                OnPropertyChanged(nameof(PlayerCount));
                OnPropertyChanged(nameof(PlayerCountProgress));
            }
        }
    }

    private bool _hasPlayerData;
    private bool _playerDataUnavailable;
    public string PlayerCount => _hasPlayerData ? $"{CurrentPlayers}/{MaxPlayers}" : _playerDataUnavailable ? "Unavailable" : "Loading...";
    public double PlayerCountProgress => MaxPlayers > 0 ? ((double)CurrentPlayers / MaxPlayers) * 100 : 0;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
                OnPropertyChanged(nameof(BackgroundColor));
                OnPropertyChanged(nameof(TextColor));
            }
        }
    }
    public IBrush BackgroundColor => !IsEnabled ?  new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)) : IsSelected ? 
    new SolidColorBrush(Color.FromRgb(0x6D, 0x20, 0x8F)) : 
    new SolidColorBrush(Color.Parse("Transparent"));

    public async Task UpdatePlayerCount(ServerService server)
    {
        var (success, currentPlayers, maxPlayers) = await server.GetPlayerCount(Value);
        if (success)
        {
            _hasPlayerData = true;
            CurrentPlayers = currentPlayers;
            MaxPlayers = maxPlayers;
            OnPropertyChanged(nameof(PlayerCount));
        }
        else if (!_hasPlayerData)
        {
            _playerDataUnavailable = true;
            OnPropertyChanged(nameof(PlayerCount));
        }
    }
    
    private bool _isHovered;
    public bool IsHovered
    {
        get => _isHovered;
        set
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                OnPropertyChanged(nameof(IsHovered));
                OnPropertyChanged(nameof(TextColor));
            }
        }
    }

    public IBrush TextColor
    {
        get
        {
            if ((IsSelected || IsHovered) && _isEnabled )
            {
                return new SolidColorBrush(Color.Parse("#ffffff"));
            }
            return new SolidColorBrush(Color.Parse("#606060"));
        }
    }

    private bool _isLastItem;

    public bool IsLastItem
    {
        get => !_isLastItem;
        set
        {
            if (_isLastItem != value)
            {
                _isLastItem = value;
                OnPropertyChanged(nameof(IsLastItem));
            }
        }
    }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                 _isEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}