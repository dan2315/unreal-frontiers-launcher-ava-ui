using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UnrealFrontiersLauncher;

public partial class NewsPanel : UserControl
{
    public event Action? Closed;

    public NewsPanel()
    {
        InitializeComponent();
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Closed?.Invoke();
}
