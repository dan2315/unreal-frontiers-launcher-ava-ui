using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace UnrealFrontiersLauncher
{
    public partial class EntryModal : UserControl
    {
        Action<string> _clickAction;
        Func<string, (bool IsValid, string Message)>? _validate;
        NavigationService _navigationService;

        /// <param name="validate">Checked before the action; its message is shown under the field when the input is not valid.</param>
        public EntryModal(NavigationService navigationService, string info, Action<string> clickAction,
            Func<string, (bool IsValid, string Message)>? validate = null)
        {
            _navigationService = navigationService;
            _clickAction = clickAction;
            _validate = validate;
            InitializeComponent();

            InfoLabel.Text = info;
            SubmitButton.Click += OnSubmitClicked;
            CancelButton.Click += OnCancelClicked;
            UserInput.TextChanged += (_, _) => ErrorLabel.IsVisible = false;

            // Enter in the field presses the main button.
            UserInput.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                e.Handled = true;
                Submit();
            };
        }

        private void OnSubmitClicked(object? sender, RoutedEventArgs e) => Submit();

        private void Submit()
        {
            var input = UserInput.Text ?? "";
            if (_validate != null)
            {
                var (isValid, message) = _validate(input);
                if (!isValid)
                {
                    ErrorLabel.Text = message;
                    ErrorLabel.IsVisible = true;
                    return;
                }
            }

            _clickAction?.Invoke(input);
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            _navigationService.GetBack();
        }
    }
}
