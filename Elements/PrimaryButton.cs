using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;

namespace UnrealFrontiersLauncher;

public class PrimaryButton : Button, IStyleable
{
    Type IStyleable.StyleKey => typeof(Button);

    public static readonly StyledProperty<bool> AutoLockProperty =
        AvaloniaProperty.Register<PrimaryButton, bool>(nameof(AutoLock), true);

    public bool AutoLock
    {
        get => GetValue(AutoLockProperty);
        set => SetValue(AutoLockProperty, value);
    }

    // Colors for Visual States
    private IBrush NormalColor { get; set; } = Brushes.SkyBlue;
    private IBrush HoverColor => NormalColor.TryGetDarkerBrush(1.1);
    private IBrush PressedColor => NormalColor.TryGetDarkerBrush(1.25);
    private IBrush DisabledColor => NormalColor.TryGetDarkerBrush(1.8);

    public PrimaryButton()
    {
        // Attach Pointer Events
        PointerEntered += OnPointerEnter;
        PointerExited += OnPointerLeave;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;

        // Default Background
        Background = NormalColor;

        // Handle Click Event for AutoLock
        Click += (sender, args) =>
        {
            if (AutoLock)
            {
                Lock();
            }
        };

        // Handle IsEnabled Change for Disabled State
        PropertyChanged += (sender, args) =>
        {
            if (args.Property == IsEnabledProperty)
            {
                Background = IsEnabled ? NormalColor : DisabledColor;
            }
        };
    }

    private void OnPointerEnter(object? sender, PointerEventArgs e)
    {
        if (IsEnabled)
        {
            Background = HoverColor;
        }
    }

    private void OnPointerLeave(object? sender, PointerEventArgs e)
    {
        if (IsEnabled)
        {
            Background = NormalColor;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsEnabled)
        {
            Background = PressedColor;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (IsEnabled)
        {
            Background = NormalColor;
        }
    }

    public void Lock()
    {
        IsEnabled = false;
    }

    public void Unlock()
    {
        IsEnabled = true;
    }
}