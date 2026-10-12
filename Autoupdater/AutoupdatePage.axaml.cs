using System;
using Avalonia.Controls;

namespace UnrealFrontiersLauncher;

public partial class AutoupdatePage : UserControl
{
    NavigationService _navigationService;
    public AutoupdatePage(NavigationService navigationService)
    {
        _navigationService = navigationService;
        DataContext = new AutoupdatePageViewModel();
        InitializeComponent();
        Loaded += (_, _) => ApplyPulse();
        UiAnimations.Changed += ApplyPulse;
        Unloaded += (_, _) => UiAnimations.Changed -= ApplyPulse;
    }

    public void TryAutoupdateOrOtherwise(Action? updateIsNotRequiredFallback = null)
    {
        _ = Autoupdater.CheckForUpdatesAsync(updateIsNotRequiredFallback ?? (() => { }));
    }

    private void ApplyPulse() => UiAnimations.SetClass(PulsePanel, "uf-pulse", UiAnimations.BackgroundPulse);
}