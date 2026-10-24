using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace UnrealFrontiersLauncher
{
    /// <summary>
    /// Central hub for UI animations. All animated effects in the launcher go
    /// through here so the "Анимации интерфейса" switches in the global settings
    /// can turn them off on weak hardware.
    ///
    /// Effects covered:
    /// - Play-button gradient breathing + glow (see Button.uf-launch-animated,
    ///   applied via UiAnimations.ApplyLaunchStyle from MainPage)
    /// - Screen slide transitions (see Services/NavigationService.cs)
    /// - Settings overlay dim + panel pop (see MainPage.ShowSettingsOverlay)
    /// - Profile panel pop-in (see MainPage.OnProfileClicked)
    /// - Autoupdate loading pulse (see StackPanel.uf-pulse in Styles/Controls.axaml)
    /// - Server card hover fade (see UiAnimations.ApplyCardTransitions)
    /// </summary>
    public static class UiAnimations
    {
        private static Preferences.Preferences Prefs => Preferences.Preferences.Get();

        /// <summary>Master switch: every animated effect checks this first.</summary>
        public static bool Enabled => Prefs.UiAnimationsEnabled;

        /// <summary>Play-button breathing gradient + glow.</summary>
        public static bool ButtonGlow => Enabled && Prefs.UiButtonGlowEnabled;

        /// <summary>Vertical slide when switching screens (auth / main / autoupdate).</summary>
        public static bool ScreenTransitions => Enabled && Prefs.UiScreenTransitionsEnabled;

        /// <summary>Backdrop dim fade + panel scale pop for the settings overlay.</summary>
        public static bool OverlayFade => Enabled && Prefs.UiOverlayFadeEnabled;

        /// <summary>Loading pulse on the autoupdate page ("Булдыга собирает чанки…").</summary>
        public static bool BackgroundPulse => Enabled && Prefs.UiBackgroundPulseEnabled;

        /// <summary>
        /// Raised by the settings UI after animation flags are saved.
        /// Long-lived pages (MainPage, AutoupdatePage) subscribe once and
        /// re-apply classes/effects so toggling needs no restart.
        /// </summary>
        public static event Action? Changed;

        public static void NotifyChanged() => Changed?.Invoke();

        /// <summary>Adds or removes a style class without duplicating it.</summary>
        public static void SetClass(StyledElement target, string className, bool on)
        {
            if (target == null) return;
            bool has = target.Classes.Contains(className);
            if (on && !has) target.Classes.Add(className);
            else if (!on && has) target.Classes.Remove(className);
        }

        /// <summary>
        /// Applies the launch-button look: breathing gradient class + mint glow
        /// when enabled, plain static gradient otherwise. Called on page load
        /// and on every <see cref="Changed"/>.
        /// Breathing never runs on a disabled button: the animated gradient
        /// would paint over the gray disabled state and the button would
        /// drift back to bright while it must stay gray.
        /// </summary>
        public static void ApplyLaunchStyle(Button button)
        {
            if (button == null) return;
            // Disabled wins over glow — re-applied from SetLaunchEnabled.
            bool glow = ButtonGlow && button.IsEnabled;
            SetClass(button, "uf-launch-animated", glow);
            button.Effect = glow
                ? new DropShadowEffect { Color = Color.Parse("#00FFA3"), Opacity = 0.25, BlurRadius = 18 }
                : null;
        }

        /// <summary>
        /// Applies hover fade transitions to a server card. Called from the
        /// card pointer handlers so killing animations removes the fade too.
        /// </summary>
        public static void ApplyCardTransitions(Border card)
        {
            if (card == null) return;
            card.Transitions = Enabled
                ? new Transitions
                {
                    new BrushTransition { Property = Border.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(150) },
                    new BrushTransition { Property = Border.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(150) },
                }
                : null;
        }

        /// <summary>
        /// Runs a fade-in on a freshly shown control.
        /// No-op (leaves the control fully visible) when overlay effects are off.
        /// NB: only Opacity is animated — Avalonia 11 ships no animator for
        /// RenderTransform, so keyframes targeting it crash at runtime.
        /// </summary>
        /// <summary>How long an error stays when animations are off (no flash, no fade).</summary>
        public static readonly TimeSpan StaticErrorDuration = TimeSpan.FromSeconds(4);

        /// <summary>
        /// Blinks the element in and fades it out in about a second. Call it again for the next error: the running one is
        /// cancelled by the caller's token and this one starts over. With animations off the element just shows for a few seconds.
        /// </summary>
        public static async Task FlashFadeAsync(Control target, CancellationToken cancellationToken = default)
        {
            if (target == null) return;

            try
            {
                if (!Enabled)
                {
                    target.Opacity = 1.0;
                    await Task.Delay(StaticErrorDuration, cancellationToken);
                    target.Opacity = 0.0;
                    return;
                }

                var animation = new Animation
                {
                    Duration = TimeSpan.FromMilliseconds(1200),
                    FillMode = FillMode.Forward,
                    Children =
                    {
                        new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 0.0) } },
                        // The blink: in within a tenth of the time, then a short hold before the fade.
                        new KeyFrame { Cue = new Cue(0.08), Setters = { new Setter(Visual.OpacityProperty, 1.0) } },
                        new KeyFrame { Cue = new Cue(0.3), Setters = { new Setter(Visual.OpacityProperty, 1.0) } },
                        new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 0.0) } }
                    }
                };
                await animation.RunAsync(target, cancellationToken);
                target.Opacity = 0.0;
            }
            catch (OperationCanceledException)
            {
                // A newer error took over.
            }
        }

        public static Task PopInAsync(Control target, CancellationToken cancellationToken = default)
            => FadeInAsync(target, TimeSpan.FromMilliseconds(180), cancellationToken);

        /// <summary>
        /// Fades in the overlay backdrop. No-op when overlay effects are off.
        /// </summary>
        public static Task FadeInAsync(Visual target, CancellationToken cancellationToken = default)
            => FadeInAsync(target, TimeSpan.FromMilliseconds(150), cancellationToken);

        private static async Task FadeInAsync(Visual target, TimeSpan duration, CancellationToken cancellationToken)
        {
            if (!OverlayFade || target == null) return;

            var animation = new Animation
            {
                Duration = duration,
                Easing = new CubicEaseOut(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters = { new Setter(Visual.OpacityProperty, 0.0) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters = { new Setter(Visual.OpacityProperty, 1.0) }
                    }
                }
            };

            try
            {
                await animation.RunAsync(target, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                target.Opacity = 1.0;
            }
        }
    }
}
