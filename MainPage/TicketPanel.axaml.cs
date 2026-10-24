using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UnrealFrontiersLauncher;

/// <summary>The "report a problem" form: a ticket for the signed-in player, sent to POST /tickets.</summary>
public partial class TicketPanel : UserControl
{
    public event Action? Closed;

    private readonly TicketService? _tickets;
    private readonly string? _serverId;
    private readonly string _client = "";
    private int _ticketId;
    private bool _sending;

    public TicketPanel()
    {
        InitializeComponent();
    }

    /// <param name="serverName">The modpack the problem is about (shown, and its id goes with the ticket); null for a general one.</param>
    public TicketPanel(TicketService tickets, string? serverId, string? serverName, string client,
        string kind = "problem", string title = "", string body = "")
    {
        _tickets = tickets;
        _serverId = serverId;
        _client = client;
        InitializeComponent();

        foreach (var option in TicketRules.Kinds)
        {
            KindsPanel.Children.Add(new RadioButton
            {
                Content = option.Label,
                Tag = option.Value,
                GroupName = "ticket-kind",
                IsChecked = option.Value == kind,
                FontSize = 13,
            });
        }

        TitleBox.Text = title;
        BodyBox.Text = body;
        TitleBox.TextChanged += (_, _) => UpdateCounters();
        BodyBox.TextChanged += (_, _) => UpdateCounters();
        ContextLabel.Text = serverName is { Length: > 0 }
            ? $"Сборка: {serverName}. К обращению добавится версия лаунчера и системы."
            : "К обращению добавится версия лаунчера и системы.";
        UpdateCounters();
    }

    private string SelectedKind => KindsPanel.Children.OfType<RadioButton>().FirstOrDefault(radio => radio.IsChecked == true)?.Tag as string ?? "problem";

    private void UpdateCounters()
    {
        TitleCounter.Text = $"{(TitleBox.Text ?? "").Length}/{TicketRules.TitleMax}";
        BodyCounter.Text = $"{(BodyBox.Text ?? "").Length}/{TicketRules.BodyMax}";
    }

    public async Task SubmitAsync()
    {
        if (_sending || _tickets == null) return;

        ErrorLabel.Text = TicketRules.Validate(TitleBox.Text, BodyBox.Text) ?? "";
        if (ErrorLabel.Text.Length > 0) return;

        _sending = true;
        SubmitButton.IsEnabled = false;
        SubmitButton.Content = "Отправляем…";
        try
        {
            var result = await _tickets.SubmitAsync(SelectedKind, TitleBox.Text ?? "", BodyBox.Text ?? "", _serverId, _client);
            if (!result.Success)
            {
                ErrorLabel.Text = result.Message;
                return;
            }

            _ticketId = result.Id;
            DoneTitle.Text = $"Обращение №{result.Id} отправлено.";
            FormView.IsVisible = false;
            DoneView.IsVisible = true;
            SubmitButton.IsVisible = false;
            CancelButton.Content = "Закрыть";
        }
        finally
        {
            _sending = false;
            SubmitButton.IsEnabled = true;
            SubmitButton.Content = "Отправить";
        }
    }

    private async void OnSubmitClicked(object? sender, RoutedEventArgs e) => await SubmitAsync();

    private void OnOpenSiteClicked(object? sender, RoutedEventArgs e)
    {
        if (_ticketId == 0) return;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = SupportInfo.TicketUrl(AppStaticData.ServerURL, _ticketId), UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ErrorLabel.Text = "Не удалось открыть браузер";
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Closed?.Invoke();
}
