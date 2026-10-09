using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using UnrealFrontiersLauncher;

public class DeepLinkHandler
{
    private static Mutex? _mutex;
    const string appMutexName = "Global\\UnrealFrontiersLauncher" + AppStaticData.InstanceSuffix;
    const string PipeName = "OnLaunchPipe" + AppStaticData.InstanceSuffix;
    const string FocusMessage = "uflauncher://focus";

    AuthService _authService;
    PlayerProfileProvider _profileProvider;
    PopupService _popupService;
    NavigationService _navigation;

    public DeepLinkHandler(AuthService authService, PlayerProfileProvider profileProvider, NavigationService navigation, PopupService popupService) 
    {
        _authService = authService;
        _profileProvider = profileProvider;
        _navigation = navigation;
        _popupService = popupService;
    }

    public async Task InitializeAsync(string[] args)
    {
        _mutex = new Mutex(true, appMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            var deepLink = args.FirstOrDefault(arg => arg.StartsWith("uflauncher://")) ?? FocusMessage;
            await SendMessageToMainInstance(deepLink);
            Environment.Exit(0);
            return;
        }

        StartNamedPipeServer();
    }

    private async Task SendMessageToMainInstance(string message)
    {
        try
        {
            using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
            {
                await client.ConnectAsync(1000);
                using (var writer = new StreamWriter(client))
                {
                    writer.AutoFlush = true;
                    await writer.WriteLineAsync(message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Could not send message to main instance: " + ex.Message);
        }
    }

    private void StartNamedPipeServer()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                using (var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                {
                    try
                    {
                        await server.WaitForConnectionAsync();

                        using (var reader = new StreamReader(server))
                        {
                            var message = await reader.ReadLineAsync();
                            if (message != null)
                            {
                                await HandleDeepLinkAsync(message);
                            }
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        Console.WriteLine("Pipe server was disposed.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Pipe server encountered an error: " + ex.Message);
                    }
                }
            }
        });
    }

    private async Task HandleDeepLinkAsync(string message)
    {
        if (Uri.TryCreate(message, UriKind.Absolute, out Uri? uri))
        {
           await OnDeepLinkRecieved(uri); 
        }
    }

    private async Task OnDeepLinkRecieved(Uri uri)
    {
        if (uri.Scheme == "uflauncher")
        {
            AppForegroundHelper.BringAppToForeground();

            if (uri.Host == "auth-response")
            {
                DiscordAuth.OnDiscordAuthResponseRecieved(uri, _authService, _profileProvider, _navigation , _popupService);
            }
            if (uri.Host == "email-verified-response")
            {
                EmailVerificationCallback.OnEmailVerified(uri, _authService ,_profileProvider, _navigation);
            }
        }
    }
}
