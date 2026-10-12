using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace UnrealFrontiersLauncher
{
    public partial class AuthPage : UserControl
    {
        private readonly AuthService _authService;
        private readonly PlayerProfileProvider _playerProfileProvider;
        private readonly NavigationService _navigationService;
        private readonly PopupService _popupService;
        
        public Button ActionButtonRef => ActionButton;
        public Button StartDiscordAuthButtonRef => StartDiscordAuthButton;

        public AuthPage(IServiceProvider serviceProvider)
        {
            _authService = serviceProvider.GetRequiredService<AuthService>();
            _playerProfileProvider = serviceProvider.GetRequiredService<PlayerProfileProvider>();
            _navigationService = serviceProvider.GetRequiredService<NavigationService>();
            _popupService = serviceProvider.GetRequiredService<PopupService>();
            
            var viewModel = new AuthPageViewModel(_authService, _playerProfileProvider, _navigationService, _popupService);
            viewModel.PageReference = this;
            DataContext = viewModel;
            
            InitializeComponent();
        }


        public async Task TryAutoAuth()
        {
            (bool success, string? nickname) = await _authService.TryAuthWithTokens();
            if (!success)
            {
                _navigationService.NavigateTo<AuthPage>();
            }
            else
            {
                _authService.GrantAccess(_playerProfileProvider, new() { nickname = nickname });
                _navigationService.NavigateTo<MainPage>();
            }
        }

        public void AuthAction(object? sender, RoutedEventArgs  e) 
        {
            ((AuthPageViewModel) DataContext).AuthAction();
        }

        public void InitiateDiscordAuth(object? sender, RoutedEventArgs  e) 
        {
            ((AuthPageViewModel) DataContext).InitiateDiscordAuth(null);
        }

        public void ToggleView(object? sender, RoutedEventArgs  e)
        {
            ((AuthPageViewModel) DataContext).ToggleView();
        }

        public void ToggleViewButton(object? sender, RoutedEventArgs e)
        {
            ((AuthPageViewModel) DataContext).ToggleView();
        }

        public void InitiatePasswordRecovery(object? sender, PointerPressedEventArgs e)
        {
            ((AuthPageViewModel) DataContext).InitiatePasswordRecovery();
        }

        public struct AccessData
        {
            public string nickname;
            public string? accessToken;
            public string? refreshToken;
        }

        public static implicit operator Window(AuthPage v)
        {
            throw new NotImplementedException();
        }
    }
}
