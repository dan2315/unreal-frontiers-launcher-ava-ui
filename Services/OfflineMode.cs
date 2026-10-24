using System;
using System.IO;
using System.Threading.Tasks;

namespace UnrealFrontiersLauncher;

/// <summary>
/// The launcher without a backend: no sign-in, so only what is already installed can be started. The nickname of the last
/// sign-in is kept on disk to play under it.
/// </summary>
public static class OfflineMode
{
    public const string DialogTitle = "Офлайн-режим";
    public const string ContinueText = "Продолжить в офлайн режиме";
    public const string RetryText = "Повторить подключение";

    public const string DialogMessage =
        "Не удалось подключиться к серверу, поэтому лаунчер запущен в офлайн-режиме.\n\n" +
        "Можно запускать уже установленные сборки. Недоступны: серверы и онлайн, обновления сборок и лаунчера, скины.";

    public static bool IsActive { get; private set; }

    public static event Action? Changed;

    public static void Enter(PlayerProfileProvider profiles)
    {
        Set(true);
        profiles.SetProfile(new PlayerProfile(LastPlayer.Load() ?? PlayerProfile.Default.Nickname));
    }

    public static void Leave() => Set(false);

    private static void Set(bool active)
    {
        if (IsActive == active) return;
        IsActive = active;
        Changed?.Invoke();
    }

    /// <summary>Tells the player the launcher is offline and asks what to do.</summary>
    /// <returns>true to continue offline, false to try the connection again.</returns>
    public static Task<bool> OfferAsync(PopupService popups) =>
        popups.ShowChoiceDialogAsync(DialogTitle, DialogMessage, ContinueText, RetryText);
}

/// <summary>The nickname of the last successful sign-in, for playing offline.</summary>
public static class LastPlayer
{
    private static string FilePath => Path.Combine(AppStaticData.LauncherBasePath, "last-player.txt");

    public static void Save(string nickname)
    {
        try
        {
            Directory.CreateDirectory(AppStaticData.LauncherBasePath);
            File.WriteAllText(FilePath, nickname);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Nickname not remembered: {e.Message}");
        }
    }

    public static string? Load()
    {
        try
        {
            var nickname = File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : "";
            return AccountRules.ValidateNickname(nickname).IsValid ? nickname : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
