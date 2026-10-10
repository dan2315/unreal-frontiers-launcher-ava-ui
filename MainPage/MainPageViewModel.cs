using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using UnrealFrontiersLauncher.Preferences;

namespace UnrealFrontiersLauncher.ViewModels;

public class MainPageViewModel : INotifyPropertyChanged
{
    private MainPage _pageReference;
    private readonly ServerService _serverService;
    private static readonly TimeSpan PlayerCountPollInterval = TimeSpan.FromSeconds(30);
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
        // The last known list right away; the server's replaces it once it answers.
        Modpacks = new ObservableCollection<Modpack>(ModpackCatalog.LoadCached().Select(Modpack.From));
        MarkLastItem();

        SelectedTab = "game";
        var preferences = Preferences.Preferences.Get();
        SelectedModpack = Modpacks.FirstOrDefault(modpack => modpack.Value == preferences.LastSelectedModpack, Modpacks[0]);
        SelectedModpack.IsSelected = true;
        _ = PollPlayerCountsAsync();
        _ = RefreshModpacksAsync();
    }

    /// <summary>
    /// Brings the list in line with the server's: new modpacks are added, removed ones dropped, names and
    /// icons updated. Items are matched by modpack id, so the selection survives; if the selected modpack
    /// is gone, MainPage.OnInstanceSelected selects the first one.
    /// </summary>
    private async Task RefreshModpacksAsync()
    {
        // Started on the UI thread, so the collection is changed there too.
        if (await ModpackCatalog.FetchAsync() is not { } fresh) return;

        var desired = fresh.Select(info =>
        {
            var existing = Modpacks.FirstOrDefault(m => m.Value == info.modpackId);
            if (existing == null) return Modpack.From(info);
            existing.Update(info);
            return existing;
        }).ToList();

        for (var i = 0; i < desired.Count; i++)
        {
            var at = Modpacks.IndexOf(desired[i]);
            if (at < 0)
            {
                Modpacks.Insert(i, desired[i]);
                _ = desired[i].UpdatePlayerCount(_serverService);
            }
            else if (at != i)
            {
                Modpacks.Move(at, i);
            }
        }
        while (Modpacks.Count > desired.Count)
            Modpacks.RemoveAt(Modpacks.Count - 1);

        MarkLastItem();
        OnPropertyChanged(nameof(PlayText));
    }

    private void MarkLastItem()
    {
        for (var i = 0; i < Modpacks.Count; i++)
            Modpacks[i].IsLastItem = i == Modpacks.Count - 1;
    }

    private async Task PollPlayerCountsAsync()
    {
        using var timer = new PeriodicTimer(PlayerCountPollInterval);
        do
        {
            await Task.WhenAll(Modpacks.Select(modpack => modpack.UpdatePlayerCount(_serverService)));
        }
        while (await timer.WaitForNextTickAsync());
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
