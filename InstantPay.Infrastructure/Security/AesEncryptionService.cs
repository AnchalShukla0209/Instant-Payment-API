using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace InstantPay.Infrastructure.Security
{
    public class AesEncryptionService
    {
        private readonly string _key = "12345678901234567890123456789012";
        private readonly string _iv = "1234567890123456";

        public string Decrypt(string cipherText)
        {
            var keyBytes = Encoding.UTF8.GetBytes(_key);
            var ivBytes = Encoding.UTF8.GetBytes(_iv);
            var cipherBytes = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = ivBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }

        public string Encrypt(string plainText)
        {
            var keyBytes = Encoding.UTF8.GetBytes(_key);
            var ivBytes = Encoding.UTF8.GetBytes(_iv);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = ivBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(cipherBytes);
        }

        // OpenSSL-compatible encryption for FINO API
        public static string OpenSSLEncrypt(string plainText, string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));
            var iv = new byte[16];
            var plainBytes = Encoding.UTF8.GetBytes(plainText);

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = iv;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(cipherBytes);
        }

        // OpenSSL-compatible decryption for FINO API
        public static string OpenSSLDecrypt(string cipherText, string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));
            var iv = new byte[16];
            var cipherBytes = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = iv;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
