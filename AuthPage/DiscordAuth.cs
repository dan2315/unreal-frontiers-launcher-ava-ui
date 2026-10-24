using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia.Threading;
using UnrealFrontiersLauncher;

namespace UnrealFrontiersLauncher;

/// <summary>
/// The return of the browser after a Discord / Google / Yandex sign-in (uflauncher://auth-response?...). The parameters are the same for
/// all of them: a known player brings the tokens, a new one a registration session (state) and asks for a nickname.
/// </summary>
class DiscordAuth
{
    public static void OnDiscordAuthResponseRecieved(Uri uri, AuthService authService, PlayerProfileProvider profileProvider, NavigationService navigation, PopupService popupService)
    {
        LoginFlow.Arrived();

        Dispatcher.UIThread.Post(async () =>
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var requestNickname = queryParams["requestNickname"];

            if (string.Equals(requestNickname, "true"))
            {
                await CreateNewBindingAndProceed(authService, profileProvider, navigation, popupService, queryParams);
            }
            else
            {
                LoginFlow.Finish();
                authService.GrantAccessAndProceed(profileProvider, navigation, queryParams);
            }
        });
    }

    private static Task CreateNewBindingAndProceed(AuthService authService, PlayerProfileProvider profileProvider, NavigationService navigation, PopupService popupService, NameValueCollection queryParams)
    {
        string state = queryParams["state"];
        var oauth = LoginFlow.IsOAuth;

        var entryModal = new EntryModal(navigation, "Введите никнейм для вашего аккаунта", async (inputNickname) =>
        {
            if (oauth)
            {
                var result = await authService.CompleteOAuthSession(state, inputNickname ?? "");
                if (result.Success)
                {
                    LoginFlow.Finish();
                    authService.GrantAccess(profileProvider, new() { nickname = result.Nickname });
                    navigation.NavigateTo<MainPage>();
                    return;
                }

                await popupService.ShowOkDialogAsync("Вход", result.Message);
                // The session cannot be used again: start over from the sign-in page. A taken or empty nickname can be retyped.
                if (result.Code == "session_expired")
                {
                    LoginFlow.Finish();
                    navigation.GetBack();
                }
                return;
            }

            var response = await authService.CompleteDiscordAuthSession(state, inputNickname);
            if (response.success)
            {
                LoginFlow.Finish();
                authService.GrantAccess(profileProvider, new() {nickname = response.nickname, accessToken = response.accessToken});
                navigation.NavigateTo<MainPage>();
            }
        });
        navigation.NavigateTo(entryModal);
        return Task.CompletedTask;
    }
}
