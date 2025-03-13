using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;

namespace UnrealFrontiersLauncher
{
    public partial class LoggingWindow : Window
    {
        private TextBox logTextBox;

        public LoggingWindow()
        {
            InitializeComponent();
        }

        public LoggingWindow(string gameName) : this()
        {
            Title = $"{gameName} - Console Output";
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            logTextBox = this.FindControl<TextBox>("LogTextBox");
        }

        public void AppendLog(string message)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Store current scroll position
                bool shouldScroll = logTextBox.CaretIndex == logTextBox.Text.Length;
                
                // Append the new message
                logTextBox.Text += message + Environment.NewLine;
                
                // If we were at the end before, move caret to the end again
                if (shouldScroll)
                {
                    logTextBox.CaretIndex = logTextBox.Text.Length;
                }
            });
        }
    }
} 