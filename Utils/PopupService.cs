using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace UnrealFrontiersLauncher
{
    public class PopupService
    {
        private readonly MainWindow _mainWindow;
        
        private FontFamily MontserratBold = (FontFamily)Application.Current.Resources["MontserratBold"];
        private FontFamily MontserratRegular = (FontFamily)Application.Current.Resources["MontserratRegular"];

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
                Width = 430,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = CreateDialogContent(title, message, new[] { "OK" }, out var resultTask),
                CanResize = false,
                Background = Brushes.Transparent,
                SystemDecorations = SystemDecorations.None,
                CornerRadius = CornerRadius.Parse("16")
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
                Width = 430,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = CreateDialogContent(title, message, new[] { "Ok", "Cancel" }, out var resultTask),
                CanResize = false,
                Background = Brushes.Transparent,
                SystemDecorations = SystemDecorations.None,
                CornerRadius = CornerRadius.Parse("16"),
            };

            await dialog.ShowDialog(_mainWindow);
            var result = await resultTask.Task; // Wait for the user's response
            return result == "Ok";
        }

        private Control CreateDialogContent(string title, string message, string[] buttons, out TaskCompletionSource<string> resultTask)
        {
            var resultCompletionSource = new TaskCompletionSource<string>();
            resultTask = resultCompletionSource;

            var border = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1B191F")),
                BorderBrush = new SolidColorBrush(Color.Parse("#2E2C34")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(24, 20)
            };

            var stackPanel = new StackPanel
            {
                Spacing = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = stackPanel;

            // Accent line
            stackPanel.Children.Add(new Border
            {
                Height = 3,
                Width = 56,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Application.Current?.Resources.TryGetResource("UfAccentGradient", null, out var accent) == true && accent is IBrush accentBrush
                    ? accentBrush
                    : new SolidColorBrush(Color.Parse("#00FFA3"))
            });

            // Title
            stackPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 17,
                FontFamily = MontserratBold,
                Foreground = Brushes.White
            });

            // Message
            stackPanel.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = MontserratRegular,
                Foreground = new SolidColorBrush(Color.Parse("#B9BDC2"))
            });

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 10,
                Margin = new Thickness(0, 8, 0, 0)
            };

            foreach (var buttonText in buttons)
            {
                var button = new Button
                {
                    Content = buttonText,
                    MinWidth = 90,
                    Padding = new Thickness(16, 8),
                    FontFamily = MontserratBold,
                    CornerRadius = new CornerRadius(10),
                    BorderThickness = new Thickness(0),
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                if (buttonText == "Ok" || buttonText == "OK")
                {
                    if (Application.Current?.Resources.TryGetResource("UfLaunchGradient", null, out var gradient) == true && gradient is IBrush gradientBrush)
                        button.Background = gradientBrush;
                    button.Foreground = Brushes.White;
                }
                else
                {
                    button.Background = new SolidColorBrush(Color.Parse("#2A2731"));
                    button.Foreground = new SolidColorBrush(Color.Parse("#B9BDC2"));
                }
                button.Click += (_, _) =>
                {
                    resultCompletionSource.SetResult(buttonText);
                    var window = (Window)button.GetVisualRoot();
                    window.Close();
                };
                buttonPanel.Children.Add(button);
            }

            stackPanel.Children.Add(buttonPanel);

            return border;
        }
    }
}
