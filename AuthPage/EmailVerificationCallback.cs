using System;
using Avalonia.Threading;

namespace  UnrealFrontiersLauncher
{
    class EmailVerificationCallback
    {
        public static void OnEmailVerified(Uri uri, PlayerProfileProvider profileProvider, NavigationService navigation)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);

                // it makes no sense, but fit very well, lol
                AuthPage.GrantAccessAndProceed(profileProvider, navigation, queryParams);
            });
        }
    }
}