using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UnrealFrontiersLauncher;

public partial class NewsPanel : UserControl
{
    public event Action? Closed;

    public IReadOnlyList<NewsEntry> Items { get; } = [];
    public string Message { get; } = "";
    public string Subtitle { get; } = "";
    public bool HasMessage => Message.Length > 0;

    public NewsPanel() : this("", [], "")
    {
    }

    public NewsPanel(string serverName, IReadOnlyList<NewsEntry> items, string message)
    {
        Items = items;
        Message = message;
        Subtitle = serverName.Length > 0 ? serverName : "Новости серверов";
        DataContext = this;
        InitializeComponent();
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Closed?.Invoke();
}
