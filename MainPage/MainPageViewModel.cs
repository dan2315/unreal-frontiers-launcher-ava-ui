using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Collections.Generic;
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
    private readonly InstanceManager _instanceManager;
    private static readonly TimeSpan PlayerCountPollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MinCatalogRefreshGap = TimeSpan.FromSeconds(10);
    private DateTime _lastCatalogRefresh = DateTime.MinValue;
    private bool _refreshingCatalog;

    public ObservableCollection<Modpack> Modpacks { get; set; }

    private Modpack? _selectedModpack;
    public Modpack? SelectedModpack
    {
        get => _selectedModpack;
        set
        {
            if (_selectedModpack != value)
            {
                _selectedModpack = value;
                OnPropertyChanged(nameof(SelectedModpack));
                OnPropertyChanged(nameof(SelectedModpackName));
                OnPropertyChanged(nameof(SelectedModpackIcon));
            }
        }
    }

    public string SelectedModpackName => SelectedModpack?.Name ?? "";
    public Bitmap? SelectedModpackIcon => SelectedModpack?.Icon;

    private string _catalogMessage = "";
    public string CatalogMessage
    {
        get => _catalogMessage;
        private set
        {
            if (_catalogMessage == value) return;
            _catalogMessage = value;
            OnPropertyChanged(nameof(CatalogMessage));
            OnPropertyChanged(nameof(HasCatalogMessage));
        }
    }

    public bool HasCatalogMessage => _catalogMessage.Length > 0;

    public string? PlayerName
    {
        get { return _pageReference.PlayerProfile?.Nickname; }
    }

    public MainPageViewModel(MainPage page, ServerService serverService, InstanceManager instanceManager)
    {
        _pageReference = page;
        _serverService = serverService;
        _instanceManager = instanceManager;
        Modpacks = new ObservableCollection<Modpack>();

        ReconcileModpacks(ModpackCatalog.Merge(null, _instanceManager.Instances.ToList()));

        var preferences = Preferences.Preferences.Get();
        SelectedModpack = Modpacks.FirstOrDefault(modpack => modpack.Value == preferences.LastSelectedModpack) ?? Modpacks.FirstOrDefault();
        if (SelectedModpack != null) SelectedModpack.IsSelected = true;

        _ = RefreshCatalogAsync(force: true);
        _ = PollPlayerCountsAsync();
    }

    public async Task RefreshCatalogAsync(bool force = false)
    {
        if (_refreshingCatalog) return;
        if (!force && DateTime.UtcNow - _lastCatalogRefresh < MinCatalogRefreshGap) return;

        _refreshingCatalog = true;
        try
        {
            var remote = await ModpackCatalog.FetchRemoteAsync(AppStaticData.GetModpackListURL(), SharedHttp.Api);
            var entries = ModpackCatalog.Merge(remote, _instanceManager.Instances.ToList());

            CatalogMessage = remote == null ? "Could not load the server list. Showing installed and default modpacks."
                : entries.Count == 0 ? "No modpacks are available yet."
                : "";

            var previousSelection = SelectedModpack?.Value;
            ReconcileModpacks(entries);

            if (SelectedModpack == null || !Modpacks.Contains(SelectedModpack))
            {
                SelectedModpack = Modpacks.FirstOrDefault(modpack => modpack.Value == previousSelection) ?? Modpacks.FirstOrDefault();
            }
            if (SelectedModpack != null) SelectedModpack.IsSelected = true;

            _lastCatalogRefresh = DateTime.UtcNow;
        }
        finally
        {
            _refreshingCatalog = false;
        }
    }

    // Updates the collection in place so the selection and the player counts survive a refresh.
    private void ReconcileModpacks(IReadOnlyList<CatalogEntry> entries)
    {
        var duplicateNames = entries
            .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var existingIndex = Modpacks.ToList().FindIndex(modpack => modpack.Value == entry.Id);

            Modpack modpack;
            if (existingIndex < 0)
            {
                modpack = new Modpack { Value = entry.Id };
                Modpacks.Insert(Math.Min(i, Modpacks.Count), modpack);
            }
            else
            {
                modpack = Modpacks[existingIndex];
                if (existingIndex != i) Modpacks.Move(existingIndex, Math.Min(i, Modpacks.Count - 1));
            }

            modpack.Apply(entry, duplicateNames.Contains(entry.Name));
        }

        var wanted = entries.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var stale in Modpacks.Where(modpack => !wanted.Contains(modpack.Value)).ToList())
        {
            Modpacks.Remove(stale);
        }

        for (var i = 0; i < Modpacks.Count; i++)
        {
            Modpacks[i].IsLastItem = i == Modpacks.Count - 1;
        }
    }

    private async Task PollPlayerCountsAsync()
    {
        using var timer = new PeriodicTimer(PlayerCountPollInterval);
        do
        {
            await Task.WhenAll(Modpacks.ToList().Select(modpack => modpack.UpdatePlayerCount(_serverService)));
        }
        while (await timer.WaitForNextTickAsync());
    }

    public event PropertyChangedEventHandler PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
