using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace UnrealFrontiersLauncher;

public class ClickableLabel : TextBlock
{
    private IBrush NormalColor { get; set; } = Brushes.SkyBlue;
    private IBrush HoverColor { get; set; } = Brushes.DodgerBlue;
    private IBrush DisabledColor { get; set; } = Brushes.Gray;

    public ClickableLabel()
    {
        // Default Visual States
        Foreground = NormalColor;

        // Attach Event Handlers
        PointerEntered += OnPointerEnter;
        PointerExited += OnPointerLeave;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
    }

    private void OnPointerEnter(object? sender, PointerEventArgs e)
    {
        if (IsEnabled)
        {
            Foreground = HoverColor;
        }
    }

    private void OnPointerLeave(object? sender, PointerEventArgs e)
    {
        if (IsEnabled)
        {
            Foreground = NormalColor;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsEnabled)
        {
            Foreground = HoverColor; // Optionally, change color on click
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (IsEnabled)
        {
            Foreground = NormalColor; // Reset color after release
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsEnabledProperty)
        {
            Foreground = IsEnabled ? NormalColor : DisabledColor;
        }
    }
}
