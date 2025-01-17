using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace UnrealFrontiersLauncher
{
    public partial class AuthPage : UserControl
    {
        private readonly AuthService _authService;
        private readonly PlayerProfileProvider _playerProfileProvider;
        private readonly NavigationService _navigationService;

        public PrimaryButton ActionButtonRef => ActionButton;
        public PrimaryButton StartDiscordAuthButtonRef => StartDiscordAuthButton;

        public AuthPage(AuthService authService, PlayerProfileProvider profileProvider, NavigationService navigationService)
        {
            InitializeComponent();
            _authService = authService;
            _playerProfileProvider = profileProvider;
            _navigationService = navigationService;

            var viewModel = new AuthPageViewModel(_authService, _playerProfileProvider, navigationService);
            viewModel.PageReference = this;
            DataContext = viewModel;

        }

        public async Task TryAutoAuth()
        {
            (bool success, string? nickname) = await _authService.TryAuthWithTokens();
            if (!success) return;

            GrantAccess(_playerProfileProvider, new() { nickname = nickname });
            _navigationService.NavigateTo<MainPage>();
        }

        public static void GrantAccessAndProceed(PlayerProfileProvider profileProvider, NavigationService navigationService, NameValueCollection queryParams)
        {
            var parameters = ReadAuthParams(queryParams);
            GrantAccess(profileProvider, parameters);
            navigationService.NavigateTo<MainPage>();
        }

        public static void GrantAccess(PlayerProfileProvider profileProvider, AccessData parameters)
        {
            if (parameters.accessToken != null && parameters.refreshToken != null)
            {
                TokenStorage.SaveTokens(parameters.accessToken, parameters.refreshToken);
            }

            var profile = new PlayerProfile(parameters.nickname);
            profileProvider.SetProfile(profile);
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

        public static AccessData ReadAuthParams(NameValueCollection queryParams)
        {
            return new()
            {
                nickname = queryParams["nickname"],
                accessToken = queryParams["accessToken"],
                refreshToken = queryParams["refreshToken"]
            };
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
