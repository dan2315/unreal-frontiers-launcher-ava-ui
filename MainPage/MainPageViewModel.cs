using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace UnrealFrontiersLauncher.ViewModels;

public class MainPageViewModel : INotifyPropertyChanged
{
    private MainPage _pageReference;
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

    public MainPageViewModel(MainPage page)
    {
        _pageReference = page;
        Modpacks = new ObservableCollection<Modpack>
        {
            new Modpack { Name = "All The Mods 9", Value = "all_the_mods_nine", IsSelected=true },
            new Modpack { Name = "(Beta) Create", Value = "create" },
            new Modpack { Name = "Unreal Zaruba", Value = "unreal_zaruba", IsEnabled = false },
        };

        if (Modpacks.Count > 0)
        {
            Modpacks[Modpacks.Count - 1].IsLastItem = true;
        }

        SelectedTab = "game";
        SelectedModpack = Modpacks[0];
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
