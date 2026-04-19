using System;
using System.Security.Cryptography;
using System.Text;

namespace FPSGame.Utils
{
    /// <summary>
    /// 加密工具类，提供SHA256哈希和AES加密功能
    /// </summary>
    public static class Crypto
    {
        private static readonly string AES_KEY = "FPSGame2024SecretKey1234567890AB"; // 32字节
        private static readonly string AES_IV = "1234567890123456"; // 16字节

        /// <summary>
        /// SHA256哈希加密（用于密码传输）
        /// </summary>
        public static string SHA256Hash(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha256.ComputeHash(bytes);

                StringBuilder result = new StringBuilder();
                foreach (byte b in hash)
                {
                    result.Append(b.ToString("x2"));
                }
                return result.ToString();
            }
        }

        /// <summary>
        /// AES加密（用于本地敏感数据存储）
        /// </summary>
        public static string AESEncrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(AES_KEY);
                    aes.IV = Encoding.UTF8.GetBytes(AES_IV);
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                    return Convert.ToBase64String(encryptedBytes);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"AES加密失败: {e.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// AES解密
        /// </summary>
        public static string AESDecrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;

            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(AES_KEY);
                    aes.IV = Encoding.UTF8.GetBytes(AES_IV);
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                    byte[] cipherBytes = Convert.FromBase64String(cipherText);
                    byte[] decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

                    return Encoding.UTF8.GetString(decryptedBytes);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"AES解密失败: {e.Message}");
                return string.Empty;
            }
        }
    }
}
