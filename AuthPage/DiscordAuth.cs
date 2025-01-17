using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia.Threading;
using UnrealFrontiersLauncher;

namespace UnrealFrontiersLauncher;

class DiscordAuth
{
    public static void OnDiscordAuthResponseRecieved(Uri uri, AuthService authService, PlayerProfileProvider profileProvider, NavigationService navigation, EntryModal entryModal)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var requestNickname = queryParams["requestNickname"];

            if (string.Equals(requestNickname, "true"))
            {
                await CreateNewDiscordBindingAndProceed(authService,  profileProvider, navigation, entryModal, queryParams);
            }
            else
            {
                AuthPage.GrantAccessAndProceed(profileProvider, navigation, queryParams);
            }
        });
    }

    private static async Task CreateNewDiscordBindingAndProceed(AuthService authService, PlayerProfileProvider profileProvider, NavigationService navigation, EntryModal nicknameRequestModal, NameValueCollection queryParams)
    {
        string state = queryParams["state"];
        navigation.NavigateTo<EntryModal>();
        nicknameRequestModal.InitializeComponentOverride("Введите никнейм для вашего аккаунта", async (inputNickname) =>
        {
            var response = await authService.CompleteDiscordAuthSession(state, inputNickname);
            if (response.success)
            {
                AuthPage.GrantAccess(profileProvider, new() {nickname = response.nickname, accessToken = response.accessToken, refreshToken = response.refreshToken});
                navigation.NavigateTo<MainPage>();
            }
        });
    }
}