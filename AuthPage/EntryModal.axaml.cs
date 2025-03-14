using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace UnrealFrontiersLauncher
{
    public partial class EntryModal : UserControl
    {
        Action<string> _clickAction;
        NavigationService _navigationService;
        
        public EntryModal(NavigationService navigationService, string info, Action<string> clickAction)
        {
            _navigationService = navigationService;
            _clickAction = clickAction;
            InitializeComponent();
            
            InfoLabel.Text = info;
            SubmitButton.Click += OnSubmitClicked;
            CancelButton.Click += OnCancelClicked;
        }

        private void OnSubmitClicked(object? sender, RoutedEventArgs e)
        {
            _clickAction?.Invoke(UserInput.Text);
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            _navigationService.GetBack();
        }
    }
}