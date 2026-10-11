using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;

namespace UnrealFrontiersLauncher;

public class Modpack : INotifyPropertyChanged
{
    private string _name = "";
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            OnPropertyChanged();
        }
    }

    private string _subtitle = "";
    public string Subtitle
    {
        get => _subtitle;
        private set
        {
            if (_subtitle == value) return;
            _subtitle = value;
            OnPropertyChanged();
        }
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value) return;
            _statusText = value;
            OnPropertyChanged();
        }
    }

    private IBrush _statusColor = Brushes.Gray;
    public IBrush StatusColor
    {
        get => _statusColor;
        private set
        {
            _statusColor = value;
            OnPropertyChanged();
        }
    }

    public Bitmap Icon { get; set; }
    private string _value;
    public string Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                Icon = LoadIcon(_value);
                OnPropertyChanged(nameof(Icon));
            }
        }
    }

    public void Apply(CatalogEntry entry, bool nameIsAmbiguous)
    {
        Name = nameIsAmbiguous ? $"{entry.Name} ({entry.Id})" : entry.Name;
        Subtitle = BuildSubtitle(entry);
        StatusText = StatusLabel(entry.Status);
        StatusColor = StatusBrush(entry.Status);
        _ = LoadRemoteIconAsync(entry.ImageUrl);
    }

    private static string BuildSubtitle(CatalogEntry entry)
    {
        var parts = new List<string>();
        if (entry.MinecraftVersion.Length > 0) parts.Add(entry.MinecraftVersion);
        parts.Add(entry.Loader == ModLoader.NeoForge ? "NeoForge" : "Forge");
        if (entry.Version.Length > 0) parts.Add("v" + entry.Version);
        return string.Join(" · ", parts);
    }

    private static string StatusLabel(ModpackStatus status) => status switch
    {
        ModpackStatus.Installed => "Installed",
        ModpackStatus.UpdateAvailable => "Update available",
        ModpackStatus.RemovedFromServer => "Removed from server",
        ModpackStatus.ServerUnavailable => "Server unavailable",
        _ => "Not installed"
    };

    private static IBrush StatusBrush(ModpackStatus status) => status switch
    {
        ModpackStatus.Installed => new SolidColorBrush(Color.Parse("#4CC24C")),
        ModpackStatus.UpdateAvailable => new SolidColorBrush(Color.Parse("#E8B83A")),
        ModpackStatus.RemovedFromServer => new SolidColorBrush(Color.Parse("#D9744F")),
        ModpackStatus.ServerUnavailable => new SolidColorBrush(Color.Parse("#8A8A8A")),
        _ => new SolidColorBrush(Color.Parse("#B0B0B0"))
    };

    private static readonly string AssetRoot = $"avares://{typeof(Modpack).Assembly.GetName().Name}/Assets/Images/";

    private static Bitmap LoadIcon(string id)
    {
        var iconUri = new Uri($"{AssetRoot}ModpackIcons/{id}_icon.png");
        var uri = AssetLoader.Exists(iconUri) ? iconUri : new Uri(AssetRoot + "logo_badge.png");
        return new Bitmap(AssetLoader.Open(uri));
    }

    private string? _iconUrl;

    // The bundled icon stays unless the server gives a URL ("none" or empty keeps it).
    private async Task LoadRemoteIconAsync(string imageUrl)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || imageUrl == _iconUrl)
            return;

        _iconUrl = imageUrl;
        try
        {
            using var stream = new System.IO.MemoryStream(await SharedHttp.Api.GetByteArrayAsync(uri));
            Icon = new Bitmap(stream);
            OnPropertyChanged(nameof(Icon));
        }
        catch (Exception ex)
        {
            _iconUrl = null;
            Console.WriteLine($"Could not load the icon of {Value}: {ex.Message}");
        }
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
                OnPropertyChanged(nameof(CardBackground));
                OnPropertyChanged(nameof(CardBorder));
                OnPropertyChanged(nameof(TextColor));
            }
        }
    }
    private static IBrush Brush(string key, string fallback)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out var value) == true && value is IBrush brush)
            return brush;
        return new SolidColorBrush(Color.Parse(fallback));
    }

    public IBrush CardBackground => IsSelected
        ? Brush("UfCardHiBrush", "#2A2731")
        : Brush("UfCardBrush", "#201E24");

    public IBrush CardBorder => IsSelected
        ? Brush("UfMintBrush", "#00FFA3")
        : Brush("UfDividerBrush", "#2E2C34");

    public IBrush AccentBrush => Brush("UfMintBrush", "#00FFA3");

    public IBrush BackgroundColor => !IsEnabled
        ? new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22))
        : IsSelected
            ? new SolidColorBrush(Color.FromRgb(0x2A, 0x27, 0x31))
            : new SolidColorBrush(Color.Parse("Transparent"));

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
            if ((IsSelected || IsHovered) && _isEnabled)
            {
                return new SolidColorBrush(Color.Parse("#ffffff"));
            }
            return new SolidColorBrush(Color.Parse("#B9BDC2"));
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