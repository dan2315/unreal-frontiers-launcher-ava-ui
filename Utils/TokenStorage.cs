using System;
using System.IO;
using System.Security.Cryptography;

namespace UnrealFrontiersLauncher
{
    public class TokenStorage
    {
        private static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UnrealFrontiers");
        private static readonly string TokenFilePath = Path.Combine(AppDataPath, "tokens.dat");
        private static readonly string KeyFilePath = Path.Combine(AppDataPath, "encryption_key.dat");
        private static readonly int KeySize = 16;

        static TokenStorage()
        {
            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }
        }

        private static byte[] LoadOrGenerateEncryptionKey()
        {
            if (File.Exists(KeyFilePath))
            {
                return File.ReadAllBytes(KeyFilePath);
            }
            else
            {
                byte[] key = new byte[KeySize];
                RandomNumberGenerator.Fill(key);

                File.WriteAllBytes(KeyFilePath, key);
                File.SetAttributes(KeyFilePath, FileAttributes.Hidden);

                return key;
            }
        }

        public static void SaveTokens(string accessToken, string refreshToken)
        {
            var tokens = $"{accessToken}|{refreshToken}";
            var encryptedTokens = Encrypt(tokens);

            File.WriteAllBytes(TokenFilePath, encryptedTokens);
        }

        public static (string accessToken, string refreshToken) LoadTokens()
        {
            if (!File.Exists(TokenFilePath))
                throw new FileNotFoundException("Token file not found.");

            var encryptedTokens = File.ReadAllBytes(TokenFilePath);
            var decryptedTokens = Decrypt(encryptedTokens);
            var tokens = decryptedTokens.Split('|');

            return (tokens[0], tokens[1]);
        }

        public static void DeleteTokens()
        {
            if (File.Exists(TokenFilePath))
            {
                File.Delete(TokenFilePath);
            }
        }

        private static byte[] Encrypt(string plainText)
        {
            byte[] key = LoadOrGenerateEncryptionKey(); // Load or generate user-specific key
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();
                byte[] iv = aes.IV;
                using (var encryptor = aes.CreateEncryptor(aes.Key, iv))
                using (var ms = new MemoryStream())
                {
                    ms.Write(iv, 0, iv.Length);
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                    return ms.ToArray();
                }
            }
        }

        private static string Decrypt(byte[] cipherText)
        {
            byte[] key = LoadOrGenerateEncryptionKey(); // Load or generate user-specific key
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                byte[] iv = new byte[aes.BlockSize / 8];
                Array.Copy(cipherText, iv, iv.Length);
                using (var decryptor = aes.CreateDecryptor(aes.Key, iv))
                using (var ms = new MemoryStream(cipherText, iv.Length, cipherText.Length - iv.Length))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }
}
