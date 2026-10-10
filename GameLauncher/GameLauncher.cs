using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.Installer.NeoForge.Installers;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;

namespace UnrealFrontiersLauncher
{
    public class GameLauncher
    {
        private static readonly TimeSpan SpeedWindow = TimeSpan.FromSeconds(5);
        private const string AccessTokenVariable = "UF_ACCESS_TOKEN";
        private const string BackendUrlVariable = "UF_BACKEND_URL";

        private readonly AuthService _authService;

        public GameLauncher(AuthService authService)
        {
            _authService = authService;
        }

        public async Task LaunchMinecraft(
            MinecraftInstance minecraftInstance,
            PlayerProfile playerProfile,
            Action<float> onProgressChanged,
            Action<string> onFileChanged,
            Action<string> onDownloadSpeedUpdated,
            Action onGameQuit,
            Action<string> onError,
            MainWindow mainWindow)
        {
            try
            {
                Console.WriteLine("Launching " + minecraftInstance.Name);
                var minecraftPath = CreateMinecraftPath(minecraftInstance);
                var reportSpeed = CreateSpeedReporter(onDownloadSpeedUpdated);

                var launcher = new MinecraftLauncher(minecraftPath);
                IProgress<ByteProgress> byteProgress = new Progress<ByteProgress>(progress =>
                {
                    if (progress.TotalBytes == 0) return;
                    onProgressChanged((float)progress.ProgressedBytes / progress.TotalBytes);
                    reportSpeed(progress.ProgressedBytes);
                });
                IProgress<InstallerProgressChangedEventArgs> fileProgress = new Progress<InstallerProgressChangedEventArgs>(args =>
                    onFileChanged($"[{args.ProgressedTasks}/{args.TotalTasks}] {args.EventType}: {args.Name}"));

                launcher.ByteProgressChanged += (_, args) => byteProgress.Report(args);
                launcher.FileProgressChanged += (_, args) => fileProgress.Report(args);

                var versionName = await InstallLoaderAsync(launcher, minecraftInstance, byteProgress, fileProgress);

                var launchConfiguration = minecraftInstance.GetLaunchConfiguration();
                var accessToken = await _authService.GetFreshAccessTokenAsync();
                var process = await launcher.InstallAndBuildProcessAsync(versionName, new MLaunchOption
                {
                    Session = MSession.CreateOfflineSession(playerProfile.Nickname),
                    MaximumRamMb = launchConfiguration.Ram,
                });
                ApplyGameEnvironment(process, accessToken);

                var modpackInstaller = new ModpackInstaller(SharedHttp.Transfer, 5, TimeSpan.FromSeconds(30));
                await modpackInstaller.InstallModpackAsync(minecraftInstance, byteProgress, fileProgress);
                minecraftInstance.MarkInstalled();

                new ServerListManager(minecraftInstance).EnsureCorrespondingServerEntryExist();

                Console.WriteLine("Executing command:");
                Console.WriteLine(process.StartInfo.FileName + " " + process.StartInfo.Arguments);

                LoggingWindow? loggingWindow = null;
                if (launchConfiguration.NeedToOpenConsole)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        loggingWindow = new LoggingWindow(minecraftInstance.Name);
                        loggingWindow.Show();
                    });
                }

                var processUtil = new ProcessWrapper(process);
                processUtil.OutputReceived += (_, line) =>
                {
                    Console.WriteLine(line);
                    loggingWindow?.AppendLog(line);
                };
                processUtil.StartWithEvents();

                if (launchConfiguration.NeedToClose)
                {
                    await Dispatcher.UIThread.InvokeAsync(mainWindow.Close);
                }

                await processUtil.WaitForExitTaskAsync();
                await Dispatcher.UIThread.InvokeAsync(() => loggingWindow?.Close());
                onGameQuit();
            }
            catch (Exception e)
            {
                CrashLog.Write($"Launching {minecraftInstance.Name} failed", e);
                onError(e.Message);
            }
        }

        private static Task<string> InstallLoaderAsync(
            MinecraftLauncher launcher,
            MinecraftInstance instance,
            IProgress<ByteProgress> byteProgress,
            IProgress<InstallerProgressChangedEventArgs> fileProgress)
        {
            if (instance.Loader == ModLoader.NeoForge)
            {
                return new NeoForgeInstaller(launcher).Install(
                    instance.GameVersion,
                    instance.ForgeVersion,
                    new NeoForgeInstallOptions { ByteProgress = byteProgress, FileProgress = fileProgress });
            }

            return new ForgeInstaller(launcher).Install(
                instance.GameVersion,
                instance.ForgeVersion,
                new ForgeInstallOptions { ByteProgress = byteProgress, FileProgress = fileProgress });
        }

        public static void ApplyGameEnvironment(Process process, string? accessToken)
        {
            var environment = process.StartInfo.EnvironmentVariables;
            environment[BackendUrlVariable] = AppStaticData.ServerURL;

            if (string.IsNullOrEmpty(accessToken))
                environment.Remove(AccessTokenVariable);
            else
                environment[AccessTokenVariable] = accessToken;
        }

        private static MinecraftPath CreateMinecraftPath(MinecraftInstance minecraftInstance)
        {
            var installFolder = Path.Combine(AppStaticData.LauncherBasePath, "Install");
            var resourcePath = EnsurePathExists(minecraftInstance.GetResourcePath());

            return new MinecraftPath
            {
                BasePath = resourcePath,
                Resource = resourcePath,
                Versions = EnsurePathExists(Path.Combine(installFolder, "versions")),
                Library = EnsurePathExists(Path.Combine(installFolder, "libraries")),
                Runtime = EnsurePathExists(Path.Combine(installFolder, "runtime")),
                Assets = EnsurePathExists(Path.Combine(installFolder, "assets"))
            };
        }

        private static Action<long> CreateSpeedReporter(Action<string> onSpeedUpdated)
        {
            var samples = new Queue<(DateTime Timestamp, long Bytes)>();

            return progressedBytes =>
            {
                var now = DateTime.UtcNow;
                samples.Enqueue((now, progressedBytes));

                while (samples.Count > 0 && samples.Peek().Timestamp < now - SpeedWindow)
                {
                    samples.Dequeue();
                }

                if (samples.Count < 2) return;

                var oldest = samples.Peek();
                var newest = samples.Last();
                var elapsedSeconds = (newest.Timestamp - oldest.Timestamp).TotalSeconds;

                if (elapsedSeconds > 0)
                {
                    var bytesPerSecond = (newest.Bytes - oldest.Bytes) / elapsedSeconds;
                    onSpeedUpdated($"{bytesPerSecond / 1024 / 1024:F2} MB/s");
                }
            };
        }

        public static string EnsurePathExists(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
