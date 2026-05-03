using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Archipelago.SaveSystem
{
    /// <summary>
    /// AES-256-CBC шифрование / дешифрование сохранений.
    /// Ключ деривируется из Hardware ID — нельзя перенести сейв на другой ПК.
    ///
    /// LIMITATION: SystemInfo.deviceUniqueIdentifier может меняться при
    /// смене железа или переустановке ОС. Предупреди игрока об этом.
    /// </summary>
    public static class SaveCrypto
    {
        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("ARCHIPELAGO_SALT_v1");

        /// <summary>
        /// Получить ключ. Вызывать ТОЛЬКО с main thread.
        /// Передавать в Encrypt/Decrypt как параметр.
        /// </summary>
        public static byte[] GetKey()
        {
            // THREAD: SystemInfo.deviceUniqueIdentifier — только main thread
            string hwId = SystemInfo.deviceUniqueIdentifier;
            using var pbkdf2 = new Rfc2898DeriveBytes(
                hwId, Salt, iterations: 10_000, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(32);
        }

        public static byte[] Encrypt(string plainText, byte[] key)
        {
            byte[] iv = GenerateIV();
            using var aes = CreateAes(key, iv);
            using var enc = aes.CreateEncryptor();
            byte[] plainBytes  = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = enc.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            byte[] result      = new byte[16 + cipherBytes.Length];
            Buffer.BlockCopy(iv,          0, result, 0,  16);
            Buffer.BlockCopy(cipherBytes, 0, result, 16, cipherBytes.Length);
            return result;
        }

        public static string Decrypt(byte[] cipherData, byte[] key)
        {
            if (cipherData == null || cipherData.Length < 17)
                throw new ArgumentException("Invalid cipher data.");
            byte[] iv         = new byte[16];
            byte[] cipherOnly = new byte[cipherData.Length - 16];
            Buffer.BlockCopy(cipherData, 0,  iv,         0, 16);
            Buffer.BlockCopy(cipherData, 16, cipherOnly, 0, cipherOnly.Length);
            using var aes = CreateAes(key, iv);
            using var dec = aes.CreateDecryptor();
            byte[] plainBytes = dec.TransformFinalBlock(cipherOnly, 0, cipherOnly.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }

        private static byte[] GenerateIV()
        {
            byte[] iv = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(iv);
            return iv;
        }

        private static Aes CreateAes(byte[] key, byte[] iv)
        {
            var aes     = Aes.Create();
            aes.KeySize = 256;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key     = key;
            aes.IV      = iv;
            return aes;
        }
    }
}
