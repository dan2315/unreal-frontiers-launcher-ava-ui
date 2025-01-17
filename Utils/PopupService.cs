using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace UnrealFrontiersLauncher
{
    public class PopupService
    {
        private readonly MainWindow _mainWindow;

        public PopupService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        /// <summary>
        /// Displays a modal popup with a single OK button.
        /// </summary>
        /// <param name="title">The title of the popup.</param>
        /// <param name="message">The message to display in the popup.</param>
        public async Task ShowOkDialogAsync(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = CreateDialogContent(message, new[] { "OK" }, out var resultTask),
                CanResize = false,
            };

            await dialog.ShowDialog(_mainWindow);
            await resultTask.Task; // Wait for the user to close the dialog
        }

        /// <summary>
        /// Displays a modal popup with Yes and No buttons.
        /// </summary>
        /// <param name="title">The title of the popup.</param>
        /// <param name="message">The message to display in the popup.</param>
        /// <returns>True if Yes is clicked, False if No is clicked.</returns>
        public async Task<bool> ShowYesNoDialogAsync(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = CreateDialogContent(message, new[] { "Ok", "Cancel" }, out var resultTask),
                CanResize = false,
            };

            await dialog.ShowDialog(_mainWindow);
            var result = await resultTask.Task; // Wait for the user's response
            return result == "Ok";
        }

        private Control CreateDialogContent(string message, string[] buttons, out TaskCompletionSource<string> resultTask)
        {
            var resultCompletionSource = new TaskCompletionSource<string>();
            resultTask = resultCompletionSource;

            var stackPanel = new StackPanel
            {
                Spacing = 20,
                Margin = new Thickness(20)
            };

            // Message
            stackPanel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            });

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 10
            };

            foreach (var buttonText in buttons)
            {
                var button = new Button
                {
                    Content = buttonText,
                    Width = 80
                };
                button.Click += (_, _) =>
                {
                    resultCompletionSource.SetResult(buttonText);
                    var window = (Window)button.GetVisualRoot();
                    window.Close();
                };
                buttonPanel.Children.Add(button);
            }

            stackPanel.Children.Add(buttonPanel);

            return stackPanel;
        }
    }
}
