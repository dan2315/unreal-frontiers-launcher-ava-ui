using System.Text.RegularExpressions;

namespace UnrealFrontiersLauncher;

/// <summary>The nickname rules, the same everywhere and checked by the server too: 3-16 Latin letters, digits and _.</summary>
public static class AccountRules
{
    private static readonly Regex NicknameChars = new("^[a-zA-Z0-9_]+$", RegexOptions.Compiled);

    public static (bool IsValid, string Message) ValidateNickname(string? nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return (false, "А как ты будешь играть без имени?");

        if (nickname.Length < 3)
            return (false, "Минимум 3 символа.");

        if (nickname.Length > 16)
            return (false, "Максимум 16 символов.");

        if (!NicknameChars.IsMatch(nickname))
            return (false, "Напрудил каких-то не тех буковок.");

        return (true, "");
    }
}
