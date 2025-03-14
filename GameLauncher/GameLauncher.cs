using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Threading;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;

namespace UnrealFrontiersLauncher
{
    public class GameLauncher
    {
        public async void LaunchMinecraft(
            MinecraftInstance minecraftInstance,
            PlayerProfile playerProfile,
            Action<float> onProgressChanged,
            Action<string> onFileChanged,
            Action<string> onDownloadSpeedUpdated,
            Action onGameQuit,
            MainWindow mainWindow)
        {
            try
            {
                Console.WriteLine("Launching " + minecraftInstance.Name);
                var installFolder = Path.Combine(AppSettings.LauncherBasePath, "Install");
                MinecraftPath minecraftPath = new MinecraftPath()
                {
                    BasePath = EnsurePathExists(minecraftInstance.GetResourcePath()),
                    Resource = EnsurePathExists(minecraftInstance.GetResourcePath()),
                    Versions = EnsurePathExists(Path.Combine(installFolder, "versions")),
                    Library = EnsurePathExists(Path.Combine(installFolder, "libraries")),
                    Runtime = EnsurePathExists(Path.Combine(installFolder, "runtime")),
                    Assets = EnsurePathExists(Path.Combine(installFolder, "assets"))
                };

                Queue<(DateTime Timestamp, long Bytes)> speedSamples = new Queue<(DateTime, long)>();
                TimeSpan rollingWindow = TimeSpan.FromSeconds(5);
                Stopwatch stopwatch = new Stopwatch();
                var evaluateSpeed = (long progressedBytes) =>
                {
                    DateTime now = DateTime.UtcNow;

                    // Add the current sample to the queue
                    speedSamples.Enqueue((now, progressedBytes));

                    // Remove samples outside the rolling window
                    while (speedSamples.Count > 0 && speedSamples.Peek().Timestamp < now - rollingWindow)
                    {
                        speedSamples.Dequeue();
                    }

                    // Calculate the speed over the rolling window
                    if (speedSamples.Count > 1)
                    {
                        var oldest = speedSamples.Peek();
                        var newest = speedSamples.Last();
                        double elapsedSeconds = (newest.Timestamp - oldest.Timestamp).TotalSeconds;
                        long bytesTransferred = newest.Bytes - oldest.Bytes;

                        if (elapsedSeconds > 0)
                        {
                            double speed = bytesTransferred / elapsedSeconds; // Bytes per second
                            string speedReadable = $"{speed / 1024 / 1024:F2} MB/s";
                            onDownloadSpeedUpdated.Invoke(speedReadable);
                        }
                    }
                };

                var launcher = new MinecraftLauncher(minecraftPath);
                var byteProgress = new Progress<ByteProgress>(progress =>
                {
                    if (progress.TotalBytes == 0) return;
                    onProgressChanged((float)progress.ProgressedBytes / progress.TotalBytes);
                    evaluateSpeed.Invoke(progress.ProgressedBytes);
                });
                var fileProgress = new Progress<InstallerProgressChangedEventArgs>(args => onFileChanged.Invoke($"[{args.ProgressedTasks}/{args.TotalTasks}] {args.EventType}: {args.Name}"));

                launcher.ByteProgressChanged += (sender, args) =>
                {
                    evaluateSpeed.Invoke(args.ProgressedBytes);
                    onProgressChanged.Invoke((float)args.ProgressedBytes / args.TotalBytes);
                };

                launcher.FileProgressChanged += (sender, args) =>
                {
                    onFileChanged.Invoke($"[{args.ProgressedTasks}/{args.TotalTasks}] {args.EventType}: {args.Name}");
                };

                var forge = new ForgeInstaller(launcher);
                var forgeInstallOptions = new ForgeInstallOptions();
                forgeInstallOptions.ByteProgress = byteProgress;
                forgeInstallOptions.FileProgress = fileProgress;
                var version_name = await forge.Install(minecraftInstance.GameVersion, minecraftInstance.ForgeVersion, forgeInstallOptions);

                var process = await launcher.InstallAndBuildProcessAsync(version_name, new MLaunchOption
                {
                    Session = MSession.CreateOfflineSession(playerProfile.Nickname),
                    MaximumRamMb = minecraftInstance.GetLaunchConfiguration().Ram,
                });
                process.StartInfo.Arguments += "";

                HttpClient httpClient = new();
                var modpackInstaller = new ModpackInstaller(httpClient, 5, TimeSpan.FromSeconds(5));
                await modpackInstaller.InstallModpackAsync(minecraftInstance, byteProgress, fileProgress);

                var serverList = new ServerListManager(minecraftInstance);
                serverList.EnsureCorrespondingServerEntryExist();

                Console.WriteLine("Executing command:");
                Console.WriteLine(process.StartInfo.FileName + " " + process.StartInfo.Arguments);

                LoggingWindow? loggingWindow = null;
                
                if (minecraftInstance.GetLaunchConfiguration().NeedToOpenConsole)
                {
                    loggingWindow = new LoggingWindow(minecraftInstance.Name);
                    loggingWindow.Show();
                }
                
                var processUtil = new ProcessWrapper(process);
                processUtil.OutputReceived += (s, e) => 
                {
                    Console.WriteLine(e);
                    if (minecraftInstance.GetLaunchConfiguration().NeedToOpenConsole)
                    {
                        loggingWindow?.AppendLog(e);
                    }
                };
                processUtil.StartWithEvents();
                
                if (minecraftInstance.GetLaunchConfiguration().NeedToClose)
                {
                    mainWindow.Close();
                }
                await processUtil.WaitForExitTaskAsync();
                onGameQuit.Invoke();
                loggingWindow?.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error occured while launching {minecraftInstance?.Name}. Exception: {e}");
            }
        }

        public static string EnsurePathExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Console.WriteLine($"Created missing directory: {path}");
            }
            return path;
        }
    }
}
