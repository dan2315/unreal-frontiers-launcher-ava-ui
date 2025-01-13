using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;

namespace UnrealFrontiersLauncher
{
    public partial class AuthPage : UserControl
    {
        private readonly AuthService _authService;
        private readonly PlayerProfileProvider _playerProfileProvider;

        public PrimaryButton ActionButtonRef => ActionButton;
        public PrimaryButton StartDiscordAuthButtonRef => StartDiscordAuthButton;

        public AuthPage(AuthService authService, PlayerProfileProvider profileProvider)
        {
            InitializeComponent();
            _authService = authService;
            _playerProfileProvider = profileProvider;

            // Set ViewModel as DataContext
            var viewModel = new AuthPageViewModel(_authService, _playerProfileProvider);
            viewModel.PageReference = this;
            DataContext = viewModel;

            // Attempt auto-authentication
            _ = TryAutoAuth(authService);
        }

        private async Task TryAutoAuth(AuthService authService)
        {
            (bool success, string? nickname) = await authService.TryAuthWithTokens();
            if (!success) return;

            GrantAccess(_playerProfileProvider, new() { nickname = nickname });
            ProceedToMainPage();
        }

        public static void GrantAccessAndProceed(PlayerProfileProvider profileProvider, NameValueCollection queryParams)
        {
            var parameters = ReadAuthParams(queryParams);
            GrantAccess(profileProvider, parameters);
            ProceedToMainPage();
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

        public static void ProceedToMainPage()
        {
            // TODO: Navigate to MainPage
            // var mainPage = App.Current.Services.GetRequiredService<MainPage>();
            // if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            // {
            //     desktop.MainWindow.Content = mainPage;
            // }
        }

        public void AuthAction(object? sender, PointerPressedEventArgs e) 
        {
            ((AuthPageViewModel) DataContext).AuthAction();
        }

        public void InitiateDiscordAuth(object? sender, PointerPressedEventArgs e) 
        {
            ((AuthPageViewModel) DataContext).InitiateDiscordAuth(null);
        }

        public void ToggleView(object? sender, PointerPressedEventArgs e) 
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
