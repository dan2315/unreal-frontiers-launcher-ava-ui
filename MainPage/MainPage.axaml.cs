using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using UnrealFrontiersLauncher.Platforms;
using UnrealFrontiersLauncher.ViewModels;

namespace UnrealFrontiersLauncher;

public partial class MainPage : UserControl
{
    private readonly PopupService _popUpService;
    private readonly InstanceManager _instanceManager;
    private readonly GameLauncher _gameLauncher;
    private readonly PlayerProfileProvider _playerProfileProvider;
    private readonly AuthService _authService;
    private readonly NavigationService _navigationService;
    private readonly MainPageViewModel _viewModel;
    private readonly MainWindow _mainWindow;
    private readonly ServerService _serverService;
    private readonly ISystemInfo _systemInfo;
    private readonly TicketService? _ticketService;
    private string? _lastLaunchError;
    public PlayerProfile PlayerProfile => _playerProfileProvider.GetProfile();

    private MinecraftInstance? SelectedInstance => _instanceManager.selectedInstance;

    private readonly LaunchProgress _launchProgress = new();
    private readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };

    public MainPage() {}
    public MainPage(PopupService popUpService, GameLauncher gameLauncher, InstanceManager instanceManager, PlayerProfileProvider playerProfileProvider, ServerService serverService, MainWindow mainWindow, AuthService authService, NavigationService navigationService, ISystemInfo systemInfo, TicketService ticketService)
    {
        _playerProfileProvider = playerProfileProvider;
        _popUpService = popUpService;
        _gameLauncher = gameLauncher;
        _instanceManager = instanceManager;
        _serverService = serverService;
        _mainWindow = mainWindow;
        _authService = authService;
        _navigationService = navigationService;
        _systemInfo = systemInfo;
        _ticketService = ticketService;
        _viewModel = new MainPageViewModel(this, _serverService, _instanceManager);
        DataContext = _viewModel;

        InitializeComponent();

        _playerProfileProvider.OnProfileUpdate(() =>
        {
            _viewModel.OnPropertyChanged(nameof(_viewModel.PlayerName));
        });

        Loaded += (_, _) =>
        {
            UiAnimations.ApplyLaunchStyle(LaunchButton);
            _ = _viewModel.RefreshCatalogAsync();
        };
        UiAnimations.Changed += OnAnimationsChanged;
        _progressTimer.Tick += (_, _) => RenderLaunchProgress();
    }

    private void OnAnimationsChanged() => UiAnimations.ApplyLaunchStyle(LaunchButton);

    // ---- Settings overlay (in-app dimmed panel) ----

    private async void ShowSettingsOverlay(UserControl panel)
    {
        // Set content first while the grid is still hidden, then fade the
        // whole overlay (backdrop + panel) as one piece — no flash of an
        // empty dim frame. No-op fade when effects are off in settings.
        SettingsOverlayContent.Content = panel;
        SettingsOverlay.Opacity = UiAnimations.OverlayFade ? 0 : 1;
        SettingsOverlay.IsVisible = true;
        await UiAnimations.FadeInAsync(SettingsOverlay);
    }

    private void CloseSettingsOverlay()
    {
        SettingsOverlay.IsVisible = false;
        SettingsOverlay.Opacity = 0;
        SettingsOverlayContent.Content = null;
    }

    private void OnOverlayBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        CloseSettingsOverlay();
    }

    private void OnModpackSettingsClicked(object? sender, RoutedEventArgs e)
    {
        var modpack = _viewModel.SelectedModpack;
        if (modpack == null) return;

        // Offline-safe: materialize a working instance from catalog data so
        // RAM/folder settings always open, even with no backend and no cache.
        var instance = SelectedInstance?.Id == modpack.Value
            ? SelectedInstance
            : _instanceManager.EnsureOfflineInstance(modpack.Entry);
        var panel = new ModpackSettingsPanel(instance, _systemInfo);
        panel.Closed += CloseSettingsOverlay;
        ShowSettingsOverlay(panel);
    }

    private void OnGlobalSettingsClicked(object? sender, RoutedEventArgs e)
    {
        var panel = new GlobalSettingsPanel();
        panel.Closed += CloseSettingsOverlay;
        ShowSettingsOverlay(panel);
    }

    // ---- News ----

    private void OnShowFullChangelogClicked(object? sender, RoutedEventArgs e)
    {
        var panel = new NewsPanel(_viewModel.SelectedModpackName, _viewModel.AllNews, _viewModel.NewsMessage);
        panel.Closed += CloseSettingsOverlay;
        ShowSettingsOverlay(panel);
    }

    // ---- Profile panel ----

    private async void OnProfileClicked(object? sender, RoutedEventArgs e)
    {
        // Popup floats above the sidebar — layout never shifts.
        ProfilePopup.IsVisible = !ProfilePopup.IsVisible;
        SetProfileChevron(ProfilePopup.IsVisible);
        if (ProfilePopup.IsVisible)
            await UiAnimations.PopInAsync(ProfilePopup);
    }

    private void OnProfileOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        // Click outside the card closes the popup; the card itself sits
        // above this catcher so its buttons stay clickable.
        ProfilePopup.IsVisible = false;
        SetProfileChevron(false);
    }

    private void SetProfileChevron(bool open)
    {
        // Chevron points up toward the menu when closed, flips down while
        // the menu is open (animated via the uf-chevron-open class).
        if (ProfileChevron != null)
            UiAnimations.SetClass(ProfileChevron, "uf-chevron-open", open);
    }

    private async void OnLogoutClicked(object? sender, RoutedEventArgs e)
    {
        ProfilePopup.IsVisible = false;
        SetProfileChevron(false);
        var answer = await _popUpService.ShowYesNoDialogAsync("Выйти из аккаунта?", "Придётся войти заново, чтобы запускать игру.");
        if (!answer) return;

        _authService.Logout();
        _playerProfileProvider.SetProfile(PlayerProfile.Default);
        _navigationService.NavigateTo<AuthPage>();
    }

    private void OnAddPasswordClicked(object? sender, RoutedEventArgs e)
    {
        ProfilePopup.IsVisible = false;
        SetProfileChevron(false);
        var entryModal = new EntryModal(_navigationService, "Укажите почту, куда отправить ресет пароля", async (email) =>
        {
            var message = await _authService.SendResetPasswordEmail(email);
            await _popUpService.ShowOkDialogAsync("Восстановление пароля", message);
            _navigationService.GetBack();
        });
        _navigationService.NavigateTo(entryModal);
    }

    private async void OnConnectDiscordClicked(object? sender, RoutedEventArgs e)
    {
        await _authService.DiscordAuth(PlayerProfile?.Nickname);
    }

    // ---- Launch ----

    private async void OnLaunchButtonClicked(object sender, RoutedEventArgs e)
    {
        var instance = SelectedInstance;
        if (instance == null) return;

        var profile = PlayerProfile;
        if (profile == null)
        {
            await _popUpService.ShowOkDialogAsync("Нет входа", "Войдите в аккаунт перед запуском игры.");
            return;
        }

        bool answer = await _popUpService.ShowYesNoDialogAsync("Запуск игры", "Запустить Minecraft с выбранной сборкой?");
        if (!answer) return;

        SetLaunchEnabled(false);
        _launchProgress.Reset();
        _progressTimer.Start();
        await _gameLauncher.LaunchMinecraft(
            instance,
            profile,
            _launchProgress,
            OnDownloadFileChanged,
            () => SetLaunchEnabled(true),
            OnLaunchFailed,
            _mainWindow);
        _progressTimer.Stop();
        RenderLaunchProgress();
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
        if (LaunchButton == null) return;
        LaunchButton.IsEnabled = enabled;
        // Re-apply the glow: breathing must stop while the button is gray
        // (disabled) and resume once it is clickable again.
        UiAnimations.ApplyLaunchStyle(LaunchButton);
    }

    private async void OnLaunchFailed(string message)
    {
        SetLaunchEnabled(true);
        _lastLaunchError = message;

        var report = await _popUpService.ShowYesNoDialogAsync("Не удалось запустить", message + "\n\nСообщить о проблеме?");
        if (report)
        {
            var name = _viewModel.SelectedModpackName;
            OpenTicketPanel("problem", string.IsNullOrEmpty(name) ? "Не удалось запустить игру" : $"Не удалось запустить {name}",
                $"При запуске игры появилась ошибка:\n{message}\n\n");
        }
    }

    // ---- Report a problem ----

    private void OnReportProblemClicked(object? sender, RoutedEventArgs e)
    {
        ProfilePopup.IsVisible = false;
        SetProfileChevron(false);
        OpenTicketPanel();
    }

    private void OpenTicketPanel(string kind = "problem", string title = "", string body = "")
    {
        if (_ticketService == null) return;

        var modpack = _viewModel.SelectedModpack;
        var version = SelectedInstance?.InstalledVersion ?? SelectedInstance?.PackVersion;
        var build = modpack == null ? null : string.IsNullOrEmpty(version) ? modpack.Name : $"{modpack.Name} v{version}";
        var client = SupportInfo.BuildClient(Autoupdater.CurrentVersion.ToString(3), SupportInfo.CurrentOs(), build, _lastLaunchError);

        var panel = new TicketPanel(_ticketService, modpack?.Value, modpack?.Name, client, kind, title, body);
        panel.Closed += CloseSettingsOverlay;
        ShowSettingsOverlay(panel);
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

        if (!loaded && TaskName != null)
        {
            TaskName.Text = $"Could not load version info for {currentModpack.Name}";
        }
    }

    private static readonly IBrush StageDone = new SolidColorBrush(Color.Parse("#B9BDC2"));
    private static readonly IBrush StageActive = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IBrush StagePending = new SolidColorBrush(Color.Parse("#6F747C"));

    private void RenderLaunchProgress()
    {
        _launchProgress.Tick();

        var overall = _launchProgress.Overall;
        LaunchProgressBar.Value = overall * 100;
        DownloadProgress.Text = _launchProgress.Finished ? "Downloaded!" : $"{overall * 100:F2} %";
        DownloadSpeed.Text = _progressTimer.IsEnabled && !_launchProgress.Finished
            ? LaunchProgress.FormatSpeed(_launchProgress.SpeedBytesPerSecond)
            : "â€”";

        TextBlock[] labels = [StageLabel0, StageLabel1, StageLabel2];
        for (var i = 0; i < labels.Length; i++)
        {
            labels[i].Foreground = _launchProgress.Finished || i < _launchProgress.Stage ? StageDone
                : i == _launchProgress.Stage && _progressTimer.IsEnabled ? StageActive
                : StagePending;
        }
    }
    private void OnDownloadFileChanged(string file)
    {
        TaskName.Text = file;
    }

    private void Modpack_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border card && card.DataContext is Modpack modpack)
        {
            UiAnimations.ApplyCardTransitions(card);
            modpack.IsHovered = true;
        }
    }

    private void Modpack_OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border card && card.DataContext is Modpack modpack)
        {
            UiAnimations.ApplyCardTransitions(card);
            modpack.IsHovered = false;
        }
    }
}
