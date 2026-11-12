using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

    private Bitmap _packIcon;
    // A network shows its own icon; the pack's icon belongs to the server it was made for.
    public Bitmap Icon => Group != null ? GroupIcon(Group.Id) : _packIcon;
    private string _value;
    public string Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                _packIcon = LoadIcon(_value);
                OnPropertyChanged(nameof(Icon));
            }
        }
    }

    // Raw catalog data, kept so an offline instance can be materialized on demand.
    public CatalogEntry Entry { get; private set; }

    private bool _nameIsAmbiguous;

    public void Apply(CatalogEntry entry, bool nameIsAmbiguous, ServerGroup? group = null)
    {
        Entry = entry;
        _nameIsAmbiguous = nameIsAmbiguous;
        StatusText = StatusLabel(entry.Status);
        StatusColor = StatusBrush(entry.Status);
        ApplyGroup(group);
        _ = LoadRemoteIconAsync(entry.ImageUrl);
    }

    // ---- Network of servers on this pack: one row in the list that opens into its servers ----

    public ServerGroup? Group { get; private set; }
    public bool IsGroup => Group != null;
    public IReadOnlyList<GroupServerRow> GroupServers { get; private set; } = [];

    // The network's row in the list opens into its servers.
    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowNestedServers));
            OnPropertyChanged(nameof(ChevronAngle));
            OnPropertyChanged(nameof(ExpandTip));
        }
    }

    public bool ShowNestedServers => IsGroup && IsExpanded;
    public double ChevronAngle => IsExpanded ? 0 : 180;
    public string ExpandTip => IsExpanded ? "Свернуть серверы" : "Показать серверы";

    /// <summary>The server whose news the card shows.</summary>
    public string NewsServerId => Group?.PrimaryServerId ?? Value;

    public void ApplyGroup(ServerGroup? group)
    {
        Group = group;
        var entry = Entry;
        if (group == null)
        {
            Name = _nameIsAmbiguous ? $"{entry.Name} ({entry.Id})" : entry.Name;
            Subtitle = BuildSubtitle(entry);
            GroupServers = [];
        }
        else
        {
            Name = group.Name;
            // One pack for the whole network: its game version and loader say nothing to the player.
            Subtitle = entry.Version.Length > 0 ? $"Сеть · {ServersCount(group.Servers.Count)} · v{entry.Version}" : $"Сеть · {ServersCount(group.Servers.Count)}";
            GroupServers = group.Servers.Select(server => new GroupServerRow(server, BundledIcon(server.Id) ?? (server.Id == Value ? _packIcon : null))).ToList();
            _hasPlayerData = true;
            CurrentPlayers = group.Online;
            MaxPlayers = group.MaxPlayers;
            OnPropertyChanged(nameof(PlayerCount));
            OnPropertyChanged(nameof(PlayerCountColor));
        }
        OnPropertyChanged(nameof(Group));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(IsGroup));
        OnPropertyChanged(nameof(ShowNestedServers));
        OnPropertyChanged(nameof(GroupServers));
    }

    private static string ServersCount(int count)
    {
        var lastTwo = count % 100;
        var last = count % 10;
        var word = lastTwo is >= 11 and <= 14 ? "серверов" : last == 1 ? "сервер" : last is >= 2 and <= 4 ? "сервера" : "серверов";
        return $"{count} {word}";
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

    private static Bitmap LoadIcon(string id) => BundledIcon(id) ?? new Bitmap(AssetLoader.Open(new Uri(AssetRoot + "logo_badge.png")));

    /// <summary>The icon shipped with the launcher for a pack or a server (Assets/Images/ModpackIcons/{id}_icon.png), or null.</summary>
    public static Bitmap? BundledIcon(string id)
    {
        var uri = new Uri($"{AssetRoot}ModpackIcons/{id}_icon.png");
        return AssetLoader.Exists(uri) ? new Bitmap(AssetLoader.Open(uri)) : null;
    }

    private static readonly Dictionary<string, Bitmap> GroupIcons = new();

    private static Bitmap GroupIcon(string id)
    {
        if (!GroupIcons.TryGetValue(id, out var icon)) GroupIcons[id] = icon = LoadIcon(id);
        return icon;
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
            _packIcon = new Bitmap(stream);
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
    // "—" while loading or when the server is unreachable, so the row
    // and the online bar below it never shift or disappear.
    // A network whose front server gives no limit shows just the players.
    public string PlayerCount => !_hasPlayerData ? "—" : MaxPlayers > 0 ? $"{CurrentPlayers}/{MaxPlayers}" : $"{CurrentPlayers}";

    // Mint when live, muted gray while loading or unavailable.
    public IBrush PlayerCountColor => _hasPlayerData
        ? Brush("UfMintBrush", "#00FFA3")
        : new SolidColorBrush(Color.Parse("#8E939B"));
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

    // Selected: lifted surface + mint edge. Hovered (not selected):
    // gentle lift, neutral edge — the mint is reserved for selection so
    // hover feels like an invitation, not a second selection.
    public IBrush CardBackground => IsSelected
        ? Brush("UfCardHiBrush", "#2A2731")
        : IsHovered
            ? new SolidColorBrush(Color.Parse("#26242D"))
            : Brush("UfCardBrush", "#201E24");

    public IBrush CardBorder => IsSelected
        ? Brush("UfMintBrush", "#00FFA3")
        : IsHovered
            ? new SolidColorBrush(Color.Parse("#4A4753"))
            : Brush("UfDividerBrush", "#2E2C34");

    public IBrush AccentBrush => Brush("UfMintBrush", "#00FFA3");

    public IBrush BackgroundColor => !IsEnabled
        ? new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22))
        : IsSelected
            ? new SolidColorBrush(Color.FromRgb(0x2A, 0x27, 0x31))
            : new SolidColorBrush(Color.Parse("Transparent"));

    public async Task UpdatePlayerCount(ServerService server)
    {
        // A network's online is the sum the group list gives.
        if (IsGroup) return;
        var (success, currentPlayers, maxPlayers) = await server.GetPlayerCount(Value);
        // The pack may have turned into a network while the request was out.
        if (IsGroup) return;
        if (success)
        {
            _hasPlayerData = true;
            CurrentPlayers = currentPlayers;
            MaxPlayers = maxPlayers;
            OnPropertyChanged(nameof(PlayerCount));
            OnPropertyChanged(nameof(PlayerCountColor));
        }
        else if (!_hasPlayerData)
        {
            _playerDataUnavailable = true;
            OnPropertyChanged(nameof(PlayerCount));
            OnPropertyChanged(nameof(PlayerCountColor));
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
                OnPropertyChanged(nameof(CardBackground));
                OnPropertyChanged(nameof(CardBorder));
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