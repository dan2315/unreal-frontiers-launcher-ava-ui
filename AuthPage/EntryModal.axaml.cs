
using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UnrealFrontiersLauncher
{
    public partial class EntryModal : UserControl
    {
        Action<string> _clickAction;
        NavigationService _navigationService;
        
        public EntryModal(NavigationService navigationService)
        {
            _navigationService = navigationService;

        }

        public void InitializeComponentOverride(string info, Action<string> clickAction)  {
            this.InitializeComponent();
            _clickAction = clickAction;
            InfoLabel.Text = info;
            SubmitButton.Click += OnSubmitClicked;
            CancelButton.Click += OnCancelClicked;
        }

        private void OnSubmitClicked(object? sender, RoutedEventArgs e)
        {
            _clickAction?.Invoke(UserInput.Text);
            _navigationService.GetBack();
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            _navigationService.GetBack();
        }
    }
}
