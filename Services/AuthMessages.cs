using System.Collections.Generic;
using System.Net;

namespace UnrealFrontiersLauncher;

/// <summary>
/// The texts a player sees for the errors of the auth endpoints. The backend sends {"error": "<English text>"} and sometimes a stable
/// "code"; the Russian wording lives here (the site keeps its own list in web/src/api/client.ts MESSAGES), so changing a text never
/// needs a backend deploy and old launchers keep working when the backend rewords an error.
/// Order: a known code, then the exact English text, then a generic text for the HTTP status.
/// </summary>
public static class AuthMessages
{
    public const string NoConnection = "Нет связи с сервером";
    public const string SpamHint = "Если письма нет, загляните в «Спам»: пока оно часто попадает туда.";
    public const string ResetSpamHint = "Если его нет, загляните в «Спам».";

    /// <summary>Shown when the browser did not come back after a Google/Yandex sign-in: the email may already belong to an account.</summary>
    public const string OAuthTimeoutHint =
        "Вход не завершился. Если у вас уже есть аккаунт с этим email — войдите паролем и привяжите Google/Яндекс в профиле на сайте.";

    // A new code from the backend goes here.
    private static readonly Dictionary<string, string> ByCode = new()
    {
        ["verification_pending"] = "Этот email уже зарегистрирован, но не подтверждён",
        ["session_expired"] = "Сессия входа истекла, начните вход заново",
        ["nickname_taken"] = "Этот ник уже занят. Выберите другой",
        ["nickname_required"] = "Укажите ник",
        ["already_registered"] = "Этот аккаунт уже зарегистрирован",
        ["invalid_nickname"] = "Ник не подходит: от 3 до 16 символов, только латинские буквы, цифры и подчёркивание.",
        ["weak_password"] = "Пароль слишком простой: нужны большая и маленькая буквы, цифра и минимум 8 символов.",
    };

    private static readonly Dictionary<string, string> ByText = new()
    {
        ["Invalid credentials"] = "Неверный ник, email или пароль",
        ["Please verify your email first"] = "Сначала подтвердите email — ссылка пришла в письме",
        ["Email or nickname already exists"] = "Такой email или ник уже занят",
        ["Error creating account"] = "Не удалось создать аккаунт. Проверьте email, ник и пароль",
        ["User not found"] = "Пользователь не найден",
        ["Email already registered, verification pending"] = "Этот email уже зарегистрирован, но не подтверждён",
        ["Please wait a minute before requesting another email"] = "Подождите минуту перед повторной отправкой письма",
        ["Email is already verified"] = "Email уже подтверждён — просто войдите",
        ["Invalid or expired token"] = "Сессия истекла, войдите снова",
        ["Authentication token required"] = "Нужно войти в аккаунт",
        ["Set a password before unlinking Discord"] = "Сначала задайте пароль: без него в аккаунт будет не войти",
        ["Invalid or expired reset token"] = "Ссылка для сброса пароля недействительна или устарела",
        ["Invalid or expired session"] = "Сессия входа истекла, начните вход заново",
        ["Nickname already taken"] = "Этот ник уже занят. Выберите другой",
        ["Missing required fields"] = "Заполните все поля",
        ["New password is required"] = "Введите новый пароль",
        ["Nickname is required"] = "Введите ник",
        ["Password must be at least 8 characters long"] = "Пароль должен быть не короче 8 символов",
        ["Refresh token is required"] = "Нужно войти в аккаунт",
        ["Session ID and nickname are required"] = "Не хватает данных для входа через Discord",
        ["Discord is not linked"] = "Discord не привязан к аккаунту",
        ["Failed to send verification email. Please try again later."] = "Не удалось отправить письмо. Попробуйте позже",
        ["Failed to send password reset email. Please try again later."] = "Не удалось отправить письмо. Попробуйте позже",
        ["Internal server error"] = "Ошибка сервера, попробуйте позже",
    };

    /// <param name="status">The HTTP status, or null when there was no answer at all.</param>
    public static string Describe(HttpStatusCode? status, string? body)
    {
        if (AuthResponses.ErrorCode(body) is { } code && ByCode.TryGetValue(code, out var byCode))
            return byCode;

        var text = AuthResponses.ErrorMessage(body).Trim();
        if (text.Length > 0 && ByText.TryGetValue(text, out var byText))
            return byText;

        return ForStatus(status);
    }

    public static string ForStatus(HttpStatusCode? status) => status switch
    {
        null or 0 => NoConnection,
        HttpStatusCode.Unauthorized => "Нужно войти в аккаунт",
        >= (HttpStatusCode)500 => "Ошибка сервера, попробуйте позже",
        _ => $"Ошибка {(int)status}"
    };
}
