using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = CreateDialogContent(title, message, new[] { ("ОК", true) }, out var resultTask),
                CanResize = false,
                Background = Brushes.Transparent,
                SystemDecorations = SystemDecorations.None,
                CornerRadius = CornerRadius.Parse("16")
            };

            await dialog.ShowDialog(_mainWindow);
            await resultTask.Task; // Wait for the user to close the dialog
        }

        /// <summary>
        /// Displays a modal popup with confirm and cancel buttons.
        /// </summary>
        /// <param name="title">The title of the popup.</param>
        /// <param name="message">The message to display in the popup.</param>
        /// <returns>True if confirm is clicked, False if cancel is clicked.</returns>
        public async Task<bool> ShowYesNoDialogAsync(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 430,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = CreateDialogContent(title, message, new[] { ("Да", true), ("Отмена", false) }, out var resultTask),
                CanResize = false,
                Background = Brushes.Transparent,
                SystemDecorations = SystemDecorations.None,
                CornerRadius = CornerRadius.Parse("16"),
            };

            await dialog.ShowDialog(_mainWindow);
            var result = await resultTask.Task; // Wait for the user's response
            return result == "Да";
        }

        /// <summary>
        /// Displays a modal popup with a scrollable body and a single close button.
        /// Used for long content like the full changelog.
        /// </summary>
        public async Task ShowLongTextDialogAsync(string title, string body)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 480,
                Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = CreateLongTextContent(title, body, out var resultTask),
                CanResize = false,
                Background = Brushes.Transparent,
                SystemDecorations = SystemDecorations.None,
                CornerRadius = CornerRadius.Parse("16")
            };

            await dialog.ShowDialog(_mainWindow);
            await resultTask.Task;
        }

        private Control CreateDialogContent(string title, string message, (string Label, bool IsPrimary)[] buttons, out TaskCompletionSource<string> resultTask)
        {
            var resultCompletionSource = new TaskCompletionSource<string>();
            resultTask = resultCompletionSource;

            var border = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1B191F")),
                BorderBrush = new SolidColorBrush(Color.Parse("#2E2C34")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(20, 16)
            };

            var stackPanel = new StackPanel
            {
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = stackPanel;

            // Title row: icon tile + title + accent line underneath.
            var titleRow = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleRow.Children.Add(new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(10),
                Background = Application.Current?.Resources.TryGetResource("UfAccentGradient", null, out var accent) == true && accent is IBrush accentBrush
                    ? accentBrush
                    : new SolidColorBrush(Color.Parse("#00FFA3")),
                Child = new TextBlock
                {
                    Text = "?",
                    FontSize = 20,
                    FontFamily = MontserratBold,
                    Foreground = new SolidColorBrush(Color.Parse("#06281C")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
            titleRow.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 17,
                FontFamily = MontserratBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            stackPanel.Children.Add(titleRow);

            stackPanel.Children.Add(new Border
            {
                Height = 2,
                Width = 64,
                CornerRadius = new CornerRadius(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Application.Current?.Resources.TryGetResource("UfAccentGradient", null, out var accent2) == true && accent2 is IBrush accentBrush2
                    ? accentBrush2
                    : new SolidColorBrush(Color.Parse("#00FFA3"))
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
                Margin = new Thickness(0, 4, 0, 0)
            };

            foreach (var (label, isPrimary) in buttons)
            {
                var button = new Button
                {
                    Content = label,
                    MinWidth = 110,
                    Padding = new Thickness(16, 8),
                    FontFamily = MontserratBold,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    HorizontalContentAlignment = HorizontalAlignment.Center
                };
                button.Classes.Add(isPrimary ? "uf-accent" : "uf-card");
                button.Click += (_, _) =>
                {
                    resultCompletionSource.SetResult(label);
                    var window = (Window)button.GetVisualRoot();
                    window.Close();
                };
                buttonPanel.Children.Add(button);
            }

            stackPanel.Children.Add(buttonPanel);

            return border;
        }

        private Control CreateLongTextContent(string title, string body, out TaskCompletionSource<string> resultTask)
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
                VerticalAlignment = VerticalAlignment.Stretch
            };
            border.Child = stackPanel;

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

            stackPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 17,
                FontFamily = MontserratBold,
                Foreground = Brushes.White
            });

            var scroll = new ScrollViewer
            {
                MaxHeight = 260,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new TextBlock
                {
                    Text = body,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = MontserratRegular,
                    Foreground = new SolidColorBrush(Color.Parse("#B9BDC2"))
                }
            };
            stackPanel.Children.Add(scroll);

            var closeButton = new Button
            {
                Content = "Закрыть",
                MinWidth = 110,
                Padding = new Thickness(16, 8),
                FontFamily = MontserratBold,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand),
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = Brushes.White
            };
            if (Application.Current?.Resources.TryGetResource("UfLaunchGradient", null, out var gradient) == true && gradient is IBrush gradientBrush)
                closeButton.Background = gradientBrush;
            closeButton.Click += (_, _) =>
            {
                resultCompletionSource.SetResult("OK");
                ((Window)closeButton.GetVisualRoot()).Close();
            };
            stackPanel.Children.Add(closeButton);

            return border;
        }
    }
}
