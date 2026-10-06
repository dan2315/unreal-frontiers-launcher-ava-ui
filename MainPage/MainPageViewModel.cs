using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Threading.Tasks;
using System;
using System.Linq;
using UnrealFrontiersLauncher.Preferences;

namespace UnrealFrontiersLauncher.ViewModels;

public class MainPageViewModel : INotifyPropertyChanged
{
    private MainPage _pageReference;
    private readonly ServerService _serverService;
    public ObservableCollection<Modpack> Modpacks { get; set; }
    private Modpack _selectedModpack;
    public Modpack SelectedModpack
    {
        get
            {
                if (_selectedModpack == null) {
                    _selectedModpack = Modpacks[0];
                }
                return _selectedModpack;
            }
        set
        {
            if (_selectedModpack != value)
            {
                _selectedModpack = value;
                OnPropertyChanged(nameof(SelectedModpack));
            }
        }
    }

    public string SelectedModpackName => SelectedModpack.Name;
    public Bitmap SelectedModpackIcon => SelectedModpack.Icon;

    private string _hoveredTab;
    public string? HoveredTab
    {
        get => _hoveredTab;
        set
        {
            if (_hoveredTab != value)
            {
                _hoveredTab = value;
                OnPropertyChanged(nameof(HoveredTab));
            }
        }
    }
    private string _selectedTab;
    public string SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab != value)
            {
                _selectedTab = value;
                OnPropertyChanged(nameof(SelectedTab));
                OnPropertyChanged(nameof(IsSettingsPanelVisible));
            }
        }
    }

    public bool IsSettingsPanelVisible => SelectedTab == "settings";

    public string PlayText
    {
        get { return $"Зайти на сервер {SelectedModpack.Name}, как {_pageReference.PlayerProfile.Nickname}"; }
    }

    public string? PlayerName
    {
        get { return _pageReference.PlayerProfile?.Nickname; }
    }

    public void OnTabSelectedCommand(string selectedTab)
    {
        SelectedTab = selectedTab;
    }

    public void OnTabHoveredCommand(string hoveredTab)
    {
        HoveredTab = hoveredTab;
    }

    public void OnTabUnhoveredCommand()
    {
        HoveredTab = null;
    }

    public MainPageViewModel(MainPage page, ServerService serverService)
    {
        _pageReference = page;
        _serverService = serverService;
        Modpacks = new ObservableCollection<Modpack>
        {
            new Modpack { Name = "Unreal Domains: Create", Value = "create" },
            new Modpack { Name = "Monifactory", Value = "monifactory"},
        };

        if (Modpacks.Count > 0)
        {
            Modpacks[Modpacks.Count - 1].IsLastItem = true;
        }

        SelectedTab = "game";
        var preferences = Preferences.Preferences.Load();
        SelectedModpack = Modpacks.FirstOrDefault(modpack => modpack.Value == preferences.LastSelectedModpack, Modpacks[0]);
        SelectedModpack.IsSelected = true;
        StartPlayerCountUpdates();
    }

    private async void StartPlayerCountUpdates()
    {
        while (true)
        {
            foreach (var modpack in Modpacks)
            {
                _ = modpack.UpdatePlayerCount(_serverService);
            }
            await Task.Delay(TimeSpan.FromSeconds(30));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public Color GetTabBackgroundColor(string tabName)
    {
        if (SelectedTab == tabName)
        {
            return Color.Parse("#ffee4aef");
        }
        else if (HoveredTab == tabName)
        {
            return Color.FromArgb(0xff, 0xb0, 0xb0, 0xb0);
        }
        return Color.FromArgb(0x00, 0x00, 0x00, 0x00);
    }
}
