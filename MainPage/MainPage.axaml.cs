using System;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using UnrealFrontiersLauncher.Platforms;
using UnrealFrontiersLauncher.ViewModels;

namespace UnrealFrontiersLauncher;

public partial class MainPage : UserControl
{
    private readonly PopupService _popUpService;
    private readonly InstanceManager _instanceManager;
    private readonly GameLauncher _gameLauncher;
    private readonly PlayerProfileProvider _playerProfileProvider;
    private readonly MainPageViewModel _viewModel;
    private readonly MainWindow _mainWindow;
    private readonly ServerService _serverService;
    private bool _settingsPanelInitialized;
#if WINDOWS
    private readonly ISystemInfo _systemInfo = new Windows.SystemInfo();
#elif LINUX
    private readonly ISystemInfo _systemInfo = new Linux.SystemInfo();
#endif
    public PlayerProfile PlayerProfile => _playerProfileProvider.GetProfile();

    private MinecraftInstance? SelectedInstance => _instanceManager.selectedInstance;

    public MainPage() {}
    public MainPage(PopupService popUpService, GameLauncher gameLauncher, InstanceManager instanceManager, PlayerProfileProvider playerProfileProvider, ServerService serverService, MainWindow mainWindow)
    {
        _playerProfileProvider = playerProfileProvider;
        _popUpService = popUpService;
        _gameLauncher = gameLauncher;
        _instanceManager = instanceManager;
        _serverService = serverService;
        _mainWindow = mainWindow;
        _viewModel = new MainPageViewModel(this, _serverService, _instanceManager);
        DataContext = _viewModel;

        InitializeComponent();

        _playerProfileProvider.OnProfileUpdate(() =>
        {
            _viewModel.OnPropertyChanged(nameof(_viewModel.PlayerName));
            _viewModel.OnPropertyChanged(nameof(_viewModel.PlayText));
        });

        Loaded += (_, _) =>
        {
            InitSettingsPanel();
            UpdateSettingsPanel();
            _ = _viewModel.RefreshCatalogAsync();
        };
    }

    public void InitSettingsPanel()
    {
        if (_settingsPanelInitialized) return;
        _settingsPanelInitialized = true;

        RamSlider.ValueChanged += UpdateRamThroughSlider;
        RamBase.PropertyChanged += (_, e) =>
        {
            if (e.Property == Avalonia.Visual.BoundsProperty && int.TryParse(RamValueLabel?.Text, out var v))
                SetRamDisplay(v);
        };
        RamEntry.TextChanged += UpdateRamThroughEntry;
        RamEntry.LostFocus += (_, _) => NormalizeRamEntry();
        CloseLauncherBox.IsCheckedChanged += UpdateCloseLauncher;
        OpenConsoleBox.IsCheckedChanged += UpdateOpenConsole;
    }

    public void UpdateSettingsPanel()
    {
        // Server may be unreachable (no cached instance yet) — still show
        // lively defaults so the tab never looks dead.
        var configuration = SelectedInstance?.GetLaunchConfiguration();
        var ram = configuration?.Ram ?? MinecraftInstance.DefaultRamMb;
        var needClose = configuration?.NeedToClose ?? true;
        var needConsole = configuration?.NeedToOpenConsole ?? false;
        var systemRamMb = (double)_systemInfo.GetRamInfo().value / 1024 / 1024;

        RamSlider.Minimum = MinecraftInstance.MinRamMb;
        RamSlider.Maximum = Math.Max(Math.Max(systemRamMb, MinecraftInstance.MinRamMb), ram);
        RamSlider.Value = Math.Clamp(ram, RamSlider.Minimum, RamSlider.Maximum);
        RamEntry.Text = ((int)RamSlider.Value).ToString();
        RamValueLabel.Text = ((int)RamSlider.Value).ToString();
        if (RamMaxLabel != null)
            RamMaxLabel.Text = $"{(int)(RamSlider.Maximum / 1024)} ГБ";
        SetRamDisplay((int)RamSlider.Value);
        CloseLauncherBox.IsChecked = needClose;
        OpenConsoleBox.IsChecked = needConsole;
    }

    private void UpdateRamThroughSlider(object? sender, RangeBaseValueChangedEventArgs args)
    {
        var value = (int)args.NewValue;
        RamEntry.Text = value.ToString();
        SetRamDisplay(value);
        SelectedInstance?.ApplyNewRamValue(value);
    }

    private void SetRamDisplay(int value)
    {
        if (RamValueLabel != null) RamValueLabel.Text = value.ToString();
        if (RamFill != null && RamBase != null && RamSlider.Maximum > RamSlider.Minimum)
        {
            // Fill lives in the same grid cell as the base track: anchor it to
            // the base width so both follow the resize identically.
            if (RamBase.Bounds.Width <= 0)
            {
                RamBase.LayoutUpdated += UpdateRamFillOnce;
                return;
            }
            var ratio = (value - RamSlider.Minimum) / (RamSlider.Maximum - RamSlider.Minimum);
            // Fill starts at x=0; thumb center travels 12..W-12 (half-thumb
            // insets), so the fill must end exactly under the thumb center.
            var track = Math.Max(0, RamBase.Bounds.Width - 24);
            RamFill.Width = Math.Max(0, Math.Min(RamBase.Bounds.Width, 12 + track * ratio));
        }
    }

    private void UpdateRamFillOnce(object? sender, EventArgs e)
    {
        RamBase.LayoutUpdated -= UpdateRamFillOnce;
        if (int.TryParse(RamValueLabel?.Text, out var value)) SetRamDisplay(value);
    }

    private void UpdateRamThroughEntry(object? sender, TextChangedEventArgs args)
    {
        if (sender is not TextBox entry) return;

        var digits = string.Concat((entry.Text ?? "").Where(char.IsDigit));
        if (entry.Text != digits)
        {
            entry.Text = digits;
            return;
        }

        if (!int.TryParse(digits, out var value) || value > RamSlider.Maximum)
        {
            value = (int)RamSlider.Maximum;
            entry.Text = value.ToString();
            return;
        }

        if (value < MinecraftInstance.MinRamMb) return;

        RamSlider.Value = value;
        RamValueLabel.Text = value.ToString();
        SelectedInstance?.ApplyNewRamValue(value);
    }

    private void NormalizeRamEntry()
    {
        if (int.TryParse(RamEntry.Text, out var value) && value >= MinecraftInstance.MinRamMb) return;
        RamEntry.Text = MinecraftInstance.MinRamMb.ToString();
    }

    private void UpdateCloseLauncher(object? sender, RoutedEventArgs args)
    {
        if (sender is ToggleButton toggle)
        {
            SelectedInstance?.ApplyNewShouldCloseValue(toggle.IsChecked == true);
        }
    }

    private void UpdateOpenConsole(object? sender, RoutedEventArgs args)
    {
        if (sender is ToggleButton toggle)
        {
            SelectedInstance?.ApplyNewShouldOpenConsoleValue(toggle.IsChecked == true);
        }
    }

    private async void OnLaunchButtonClicked(object sender, RoutedEventArgs e)
    {
        var instance = SelectedInstance;
        if (instance == null) return;

        var profile = PlayerProfile;
        if (profile == null)
        {
            await _popUpService.ShowOkDialogAsync("Not signed in", "Log in to your account before launching the game.");
            return;
        }

        bool answer = await _popUpService.ShowYesNoDialogAsync("Launching...", "Вы намерены запустить майнкрафт?");
        if (!answer) return;

        SetLaunchEnabled(false);
        await _gameLauncher.LaunchMinecraft(
            instance,
            profile,
            OnDownloadProgress,
            OnDownloadFileChanged,
            OnDownloadSpeedChanged,
            () => SetLaunchEnabled(true),
            OnLaunchFailed,
            _mainWindow);
    }

    private async void OnRefreshModpacksClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button) button.IsEnabled = false;
        await _viewModel.RefreshCatalogAsync(force: true);
        if (sender is Button refreshed) refreshed.IsEnabled = true;
    }

    private void SetLaunchEnabled(bool enabled)
    {
        // Selection events can fire while the XAML is still being loaded.
        if (LaunchButton != null) LaunchButton.IsEnabled = enabled;
    }

    private async void OnLaunchFailed(string message)
    {
        SetLaunchEnabled(true);
        await _popUpService.ShowOkDialogAsync("Launch failed", message);
    }

    private void OnDownloadSpeedChanged(string speedText)
    {
        DownloadSpeed.Text = speedText;
    }

    private async void OnInstanceSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not Modpack currentModpack)
        {
            ModpackCollectionView.SelectedItem = _viewModel.Modpacks.FirstOrDefault();
            if (_viewModel.Modpacks.Count == 0) SetLaunchEnabled(false);
            return;
        }

        if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is Modpack previousModpack)
        {
            if (!currentModpack.IsEnabled)
            {
                ModpackCollectionView.SelectedItem = previousModpack;
                return;
            }
            previousModpack.IsSelected = false;
        }

        currentModpack.IsSelected = true;
        var preferences = Preferences.Preferences.Get();
        preferences.SetSelectedModpack(currentModpack.Value);
        preferences.Save();
        Console.WriteLine($"Selected Server: {currentModpack.Name}");

        SetLaunchEnabled(false);
        var loaded = await _instanceManager.SelectInstance(currentModpack.Value);
        if (_viewModel.SelectedModpack != currentModpack) return;

        SetLaunchEnabled(loaded);
        UpdateSettingsPanel();

        if (!loaded && TaskName != null)
        {
            TaskName.Text = $"Could not load version info for {currentModpack.Name}";
        }
    }

    private void OnDownloadProgress(float progress)
    {
        LaunchProgressBar.Value = progress * 100;
        DownloadProgress.Text = Math.Abs(progress - 1f) < 0.001f ? "Downloaded!" : $"{progress * 100:F2} %";
    }

    private void OnDownloadFileChanged(string file)
    {
        TaskName.Text = file;
    }

    private void OnShowFolderClicked(object sender, RoutedEventArgs e)
    {
        if (SelectedInstance == null) return;

        Process.Start(new ProcessStartInfo
        {
            FileName = GameLauncher.EnsurePathExists(SelectedInstance.GetResourcePath()),
            UseShellExecute = true,
            Verb = "open"
        });
    }

    private void Modpack_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border { DataContext: Modpack modpack })
        {
            modpack.IsHovered = true;
        }
    }

    private void Modpack_OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border { DataContext: Modpack modpack })
        {
            modpack.IsHovered = false;
        }
    }
}
