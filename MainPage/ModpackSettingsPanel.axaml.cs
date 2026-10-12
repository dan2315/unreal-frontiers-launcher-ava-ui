using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using UnrealFrontiersLauncher.Platforms;

namespace UnrealFrontiersLauncher;

public partial class ModpackSettingsPanel : UserControl
{
    // RAM snap stops: powers of two plus the midpoints between them,
    // so e.g. 6 ГБ (6144) and 12 ГБ (12288) are reachable too.
    private static readonly int[] RamSnapStops =
    {
        512, 768,
        1024, 1536,
        2048, 3072,
        4096, 6144,
        8192, 12288,
        16384, 24576,
        32768, 49152,
        65536
    };

    private readonly MinecraftInstance _instance;
    private readonly ISystemInfo _systemInfo;
    private bool _suppressSliderEvents;

    public event Action? Closed;

    public ModpackSettingsPanel(MinecraftInstance instance, ISystemInfo systemInfo)
    {
        _instance = instance;
        _systemInfo = systemInfo;
        InitializeComponent();

        TitleLabel.Text = $"«{instance.Name}» — память";
        RamSlider.ValueChanged += UpdateRamThroughSlider;
        RamBase.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.BoundsProperty && int.TryParse(RamValueLabel?.Text, out var v))
                SetRamDisplay(v);
        };
        RamEntry.TextChanged += UpdateRamThroughEntry;
        RamEntry.LostFocus += (_, _) => NormalizeRamEntry();
        RamEntry.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) NormalizeRamEntry();
        };

        var ram = instance.GetLaunchConfiguration().Ram;
        var systemRamMb = (double)_systemInfo.GetRamInfo().value / 1024 / 1024;
        RamSlider.Minimum = RamSnapStops[0];
        RamSlider.Maximum = Math.Max(Math.Max(systemRamMb, RamSnapStops[0]), ram);
        SetRamValue(SnapRam(Math.Clamp(ram, RamSlider.Minimum, RamSlider.Maximum)), save: false);
        RamMinLabel.Text = FormatRamLabel((int)RamSlider.Minimum);
        RamMaxLabel.Text = FormatRamLabel((int)RamSlider.Maximum);
    }

    private static string FormatRamLabel(int mb) =>
        mb >= 1024 && mb % 1024 == 0 ? $"{mb / 1024} ГБ" : $"{mb} МБ";

    private static int SnapRam(double value)
    {
        var best = RamSnapStops[0];
        var bestDistance = Math.Abs(value - best);
        foreach (var stop in RamSnapStops)
        {
            var distance = Math.Abs(value - stop);
            if (distance < bestDistance)
            {
                best = stop;
                bestDistance = distance;
            }
        }
        return best;
    }

    private void SetRamValue(int value, bool save)
    {
        value = Math.Clamp(value, (int)RamSlider.Minimum, (int)RamSlider.Maximum);
        _suppressSliderEvents = true;
        RamSlider.Value = value;
        _suppressSliderEvents = false;
        RamEntry.Text = value.ToString();
        SetRamDisplay(value);
        if (save) _instance.ApplyNewRamValue(value);
    }

    private void UpdateRamThroughSlider(object? sender, RangeBaseValueChangedEventArgs args)
    {
        if (_suppressSliderEvents) return;
        SetRamValue(SnapRam(args.NewValue), save: true);
    }

    private void SetRamDisplay(int value)
    {
        RamValueLabel.Text = value.ToString();
        if (RamSlider.Maximum > RamSlider.Minimum)
        {
            if (RamBase.Bounds.Width <= 0)
            {
                RamBase.LayoutUpdated += UpdateRamFillOnce;
                return;
            }
            var ratio = (value - RamSlider.Minimum) / (RamSlider.Maximum - RamSlider.Minimum);
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
        }
    }

    private void NormalizeRamEntry()
    {
        if (!int.TryParse(RamEntry.Text, out var value))
            value = MinecraftInstance.MinRamMb;
        SetRamValue(SnapRam(value), save: true);
    }

    private void OnShowFolderClicked(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = GameLauncher.EnsurePathExists(_instance.GetResourcePath()),
            UseShellExecute = true,
            Verb = "open"
        });
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Closed?.Invoke();
}
