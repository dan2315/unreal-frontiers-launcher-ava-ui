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
        private readonly UserProvider _userProvider;
        private readonly NavigationService _navigationService;

        public Button ActionButtonRef => ActionButton;
        public Button StartDiscordAuthButtonRef => StartDiscordAuthButton;

        public AuthPage(IServiceProvider serviceProvider)
        {
            _authService = serviceProvider.GetRequiredService<AuthService>();
            _userProvider = serviceProvider.GetRequiredService<UserProvider>();
            _navigationService = serviceProvider.GetRequiredService<NavigationService>();
            _ = VerifyToken();
            
            InitializeComponent();

            var viewModel = new AuthPageViewModel(_authService, _userProvider, _navigationService, serviceProvider.GetRequiredService<PopupService>());
            viewModel.PageReference = this;
            DataContext = viewModel;
        }

        public async Task VerifyToken()
        {
            User? user = await _authService.TryGetUser();
            
            if (user == null) return;

            //TODO: Check if login present, otherwise ask user to enter it
            
            _userProvider.SetProfile(user);
            _navigationService.NavigateTo<MainPage>();
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

        public void InitiatePasswordRecovery(object? sender, PointerPressedEventArgs e)
        {
            ((AuthPageViewModel) DataContext).InitiatePasswordRecovery();
        }

        public struct AccessData
        {
            public string login;
            public string? accessToken;
        }

        public static implicit operator Window(AuthPage v)
        {
            throw new NotImplementedException();
        }
    }
}
