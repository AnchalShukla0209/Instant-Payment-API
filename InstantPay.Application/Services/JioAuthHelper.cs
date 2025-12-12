using Microsoft.AspNetCore.Http;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public static class JioAuthHelper
    {
        public static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
                sb.Append(chars[random.Next(chars.Length)]);
            return sb.ToString();
        }

        public static string EncryptAES(string plainData, string aesKey)
        {
            using (var aes = new AesManaged())
            {
                aes.Key = Encoding.UTF8.GetBytes(aesKey);
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainData);
                    byte[] encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    return Convert.ToBase64String(encrypted);
                }
            }
        }

        public static string EncryptRSA(string plainData, string publicKeyContent)
        {
            try
            {
                string publicKeyPem = publicKeyContent.Trim();

                // Add header/footer if missing
                if (!publicKeyPem.Contains("BEGIN PUBLIC KEY"))
                {
                    publicKeyPem =
                        "-----BEGIN PUBLIC KEY-----\n" +
                        publicKeyPem +
                        "\n-----END PUBLIC KEY-----";
                }

                PemReader pemReader = new PemReader(new StringReader(publicKeyPem));
                object keyObject = pemReader.ReadObject();

                if (keyObject == null)
                    throw new Exception("Could not parse public key from PEM text.");

                AsymmetricKeyParameter publicKeyParam;

                if (keyObject is AsymmetricKeyParameter)
                    publicKeyParam = (AsymmetricKeyParameter)keyObject;
                else if (keyObject is Org.BouncyCastle.X509.X509Certificate cert)
                    publicKeyParam = cert.GetPublicKey();
                else
                    throw new Exception("Unsupported key type: " + keyObject.GetType().Name);

                RsaKeyParameters rsaKeyParams = (RsaKeyParameters)publicKeyParam;
                var rsaParams = DotNetUtilities.ToRSAParameters(rsaKeyParams);

                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(rsaParams);

                    byte[] data = Encoding.UTF8.GetBytes(plainData);
                    byte[] encrypted = rsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);

                    return Convert.ToBase64String(encrypted);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("RSA encryption failed: " + ex.Message, ex);
            }
        }



        //public static string EncryptRSA(string plainData, string pemFilePath)
        //{
        //    try
        //    {
        //        string publicKeyPem = File.ReadAllText(pemFilePath).Trim();

        //        // 🔧 If missing header/footer, add them
        //        if (!publicKeyPem.Contains("BEGIN PUBLIC KEY"))
        //        {
        //            publicKeyPem =
        //                "-----BEGIN PUBLIC KEY-----\n" +
        //                publicKeyPem +
        //                "\n-----END PUBLIC KEY-----";
        //        }

        //        PemReader pemReader = new PemReader(new StringReader(publicKeyPem));
        //        object keyObject = pemReader.ReadObject();

        //        if (keyObject == null)
        //            throw new Exception("Could not parse public key from PEM file (even after adding header/footer).");

        //        AsymmetricKeyParameter publicKeyParam;

        //        if (keyObject is AsymmetricKeyParameter)
        //        {
        //            publicKeyParam = (AsymmetricKeyParameter)keyObject;
        //        }
        //        else if (keyObject is Org.BouncyCastle.X509.X509Certificate cert)
        //        {
        //            publicKeyParam = cert.GetPublicKey();
        //        }
        //        else
        //        {
        //            throw new Exception("Unsupported key type: " + keyObject.GetType().Name);
        //        }

        //        RsaKeyParameters rsaKeyParams = (RsaKeyParameters)publicKeyParam;
        //        var rsaParams = DotNetUtilities.ToRSAParameters(rsaKeyParams);

        //        using (var rsa = new System.Security.Cryptography.RSACryptoServiceProvider())
        //        {
        //            rsa.ImportParameters(rsaParams);
        //            byte[] dataToEncrypt = Encoding.UTF8.GetBytes(plainData);
        //            byte[] encryptedBytes = rsa.Encrypt(dataToEncrypt, false); // PKCS#1 v1.5
        //            return Convert.ToBase64String(encryptedBytes);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new Exception("RSA encryption failed: " + ex.Message, ex);
        //    }
        //}
    }
}
