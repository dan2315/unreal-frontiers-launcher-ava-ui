using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UnrealFrontiersLauncher
{
    public class TokenStorage
    {
        private static readonly string AppDataPath = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppStaticData.DataFolderName)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", AppStaticData.DataFolderName);

        private static readonly string TokenFilePath = Path.Combine(AppDataPath, "tokens.dat");
        private static readonly string KeyFilePath = Path.Combine(AppDataPath, "encryption_key.dat");
        private const int KeySize = 32;
        private const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        public TokenStorage()
        {
            Directory.CreateDirectory(AppDataPath);
        }

        public void SaveTokens(string accessToken, string refreshToken)
        {
            var data = Encoding.UTF8.GetBytes($"{accessToken}|{refreshToken}");
            File.WriteAllBytes(TokenFilePath, Protect(data));
            RestrictToOwner(TokenFilePath);
        }

        public (string accessToken, string refreshToken) LoadTokens()
        {
            if (!File.Exists(TokenFilePath)) return ("", "");

            try
            {
                var data = Unprotect(File.ReadAllBytes(TokenFilePath));
                var tokens = Encoding.UTF8.GetString(data).Split('|', 2);
                return tokens.Length == 2 ? (tokens[0], tokens[1]) : ("", "");
            }
            catch (Exception ex) when (ex is CryptographicException or IOException or ArgumentException)
            {
                Console.WriteLine($"Stored tokens are unreadable and will be discarded: {ex.Message}");
                if (ex is not IOException) DeleteTokens();
                return ("", "");
            }
        }

        public void DeleteTokens()
        {
            if (File.Exists(TokenFilePath))
            {
                File.Delete(TokenFilePath);
            }
        }

        private static byte[] Protect(byte[] data)
        {
            if (OperatingSystem.IsWindows())
            {
                return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            }

            using var aes = Aes.Create();
            aes.Key = LoadOrCreateKey();
            aes.GenerateIV();
            var cipher = aes.EncryptCbc(data, aes.IV);
            return [.. aes.IV, .. cipher];
        }

        private static byte[] Unprotect(byte[] data)
        {
            if (OperatingSystem.IsWindows())
            {
                return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
            }

            using var aes = Aes.Create();
            aes.Key = LoadOrCreateKey();
            var ivLength = aes.BlockSize / 8;
            return aes.DecryptCbc(data.AsSpan(ivLength), data.AsSpan(0, ivLength));
        }

        private static byte[] LoadOrCreateKey()
        {
            if (File.Exists(KeyFilePath))
            {
                return File.ReadAllBytes(KeyFilePath);
            }

            var key = RandomNumberGenerator.GetBytes(KeySize);
            File.WriteAllBytes(KeyFilePath, key);
            RestrictToOwner(KeyFilePath);
            return key;
        }

        private static void RestrictToOwner(string path)
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, OwnerReadWrite);
            }
        }
    }
}
