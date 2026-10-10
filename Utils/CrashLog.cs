using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace UnrealFrontiersLauncher;

public static class CrashLog
{
    public static string LogDirectory { get; set; } = Path.Combine(AppStaticData.LauncherBasePath, "logs");

    public static string LogFilePath => Path.Combine(LogDirectory, "launcher.log");

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("Unhandled exception", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => Write("Unobserved task exception", e.Exception);
        Dispatcher.UIThread.UnhandledException += (_, e) => Write("UI thread exception", e.Exception);
    }

    public static void Write(string source, Exception? exception)
    {
        Console.WriteLine($"{source}: {exception}");

        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
        }
    }
}
