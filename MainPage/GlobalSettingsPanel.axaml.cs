using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace UnrealFrontiersLauncher;

public partial class GlobalSettingsPanel : UserControl
{
    public event Action? Closed;

    public GlobalSettingsPanel()
    {
        InitializeComponent();

        var preferences = Preferences.Preferences.Get();
        CloseLauncherBox.IsChecked = preferences.NeedToClose;
        OpenConsoleBox.IsChecked = preferences.NeedToOpenConsole;
        UiAnimationsBox.IsChecked = preferences.UiAnimationsEnabled;
        UiGlowBox.IsChecked = preferences.UiButtonGlowEnabled;
        UiTransitionsBox.IsChecked = preferences.UiScreenTransitionsEnabled;
        UiOverlayFadeBox.IsChecked = preferences.UiOverlayFadeEnabled;
        UiPulseBox.IsChecked = preferences.UiBackgroundPulseEnabled;
        UpdateEffectTogglesEnabled();

        CloseLauncherBox.IsCheckedChanged += UpdateCloseLauncher;
        OpenConsoleBox.IsCheckedChanged += UpdateOpenConsole;
        UiAnimationsBox.IsCheckedChanged += UpdateUiAnimations;
        UiGlowBox.IsCheckedChanged += UpdateUiGlow;
        UiTransitionsBox.IsCheckedChanged += UpdateUiTransitions;
        UiOverlayFadeBox.IsCheckedChanged += UpdateUiOverlayFade;
        UiPulseBox.IsCheckedChanged += UpdateUiPulse;
    }

    // The master switch gates the individual effects: the whole subgroup
    // dims (no hit-test, toggles show the disabled track) while it is off,
    // so a saved "all off" state cannot look half-on.
    private void UpdateEffectTogglesEnabled()
    {
        bool master = UiAnimationsBox.IsChecked == true;
        UiEffectsGroup.IsEnabled = master;
        UiEffectsGroup.Opacity = master ? 1.0 : 0.45;
    }

    private void UpdateCloseLauncher(object? sender, RoutedEventArgs args)
    {
        if (sender is ToggleButton toggle)
        {
            var preferences = Preferences.Preferences.Get();
            preferences.NeedToClose = toggle.IsChecked == true;
            preferences.Save();
        }
    }

    private void UpdateOpenConsole(object? sender, RoutedEventArgs args)
    {
        if (sender is ToggleButton toggle)
        {
            var preferences = Preferences.Preferences.Get();
            preferences.NeedToOpenConsole = toggle.IsChecked == true;
            preferences.Save();
        }
    }

    private void UpdateUiAnimations(object? sender, RoutedEventArgs args)
    {
        if (sender is not ToggleButton toggle) return;
        var preferences = Preferences.Preferences.Get();
        preferences.UiAnimationsEnabled = toggle.IsChecked == true;
        preferences.Save();
        UpdateEffectTogglesEnabled();
        UiAnimations.NotifyChanged();
    }

    private void UpdateUiGlow(object? sender, RoutedEventArgs args)
    {
        if (sender is not ToggleButton toggle) return;
        var preferences = Preferences.Preferences.Get();
        preferences.UiButtonGlowEnabled = toggle.IsChecked == true;
        preferences.Save();
        UiAnimations.NotifyChanged();
    }

    private void UpdateUiTransitions(object? sender, RoutedEventArgs args)
    {
        if (sender is not ToggleButton toggle) return;
        var preferences = Preferences.Preferences.Get();
        preferences.UiScreenTransitionsEnabled = toggle.IsChecked == true;
        preferences.Save();
        UiAnimations.NotifyChanged();
    }

    private void UpdateUiOverlayFade(object? sender, RoutedEventArgs args)
    {
        if (sender is not ToggleButton toggle) return;
        var preferences = Preferences.Preferences.Get();
        preferences.UiOverlayFadeEnabled = toggle.IsChecked == true;
        preferences.Save();
        UiAnimations.NotifyChanged();
    }

    private void UpdateUiPulse(object? sender, RoutedEventArgs args)
    {
        if (sender is not ToggleButton toggle) return;
        var preferences = Preferences.Preferences.Get();
        preferences.UiBackgroundPulseEnabled = toggle.IsChecked == true;
        preferences.Save();
        UiAnimations.NotifyChanged();
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Closed?.Invoke();
}
