using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia.Threading;
using UnrealFrontiersLauncher;

namespace UnrealFrontiersLauncher;

class DiscordAuth
{
    public static void OnDiscordAuthResponseRecieved(Uri uri, AuthService authService, UserProvider profileProvider, NavigationService navigation, EntryModal entryModal, PopupService popupService)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var requestNickname = queryParams["requestNickname"];
            var nickname = queryParams["nickname"];


            popupService.ShowOkDialogAsync("",$"Текущий никнейм : {nickname}    Req nickname: {requestNickname}");


            if (string.Equals(requestNickname, "true"))
            {
                await CreateNewDiscordBindingAndProceed(authService,  profileProvider, navigation, entryModal, queryParams);
            }
            else
            {
                //TODO: authService.GrantAccessAndProceed(profileProvider, navigation, queryParams);
            }
        });
    }

    private static async Task CreateNewDiscordBindingAndProceed(AuthService authService, UserProvider profileProvider, NavigationService navigation, EntryModal nicknameRequestModal, NameValueCollection queryParams)
    {
        string state = queryParams["state"];
        navigation.NavigateTo<EntryModal>();
        nicknameRequestModal.InitializeComponentOverride("Введите никнейм для вашего аккаунта", async (inputNickname) =>
        {
            var response = await authService.CompleteDiscordAuthSession(state, inputNickname);
            if (response.success)
            {
                // TODO: authService.GrantAccess(profileProvider, new() {login = response.nickname, accessToken = response.accessToken});
                navigation.NavigateTo<MainPage>();
            }
        });
    }
}