using System;
using Avalonia.Threading;

namespace  UnrealFrontiersLauncher
{
    class EmailVerificationCallback
    {
        public static void OnEmailVerified(Uri uri,AuthService authService ,PlayerProfileProvider profileProvider, NavigationService navigation)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);

                authService.GrantAccessAndProceed(profileProvider, navigation, queryParams);
            });
        }
    }
}