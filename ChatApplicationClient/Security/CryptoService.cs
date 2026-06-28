using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ChatApplicationClient.Security
{
    internal class CryptoService : IDisposable
    {
        private readonly RSA _rsa = RSA.Create(2048);

        public string ExportPublicKey() => Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo());

        private static RSA ImportPublicKey(string base64)
        {
            var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(base64), out _);
            return rsa;
        }

        public static byte[] NewAesKey() => RandomNumberGenerator.GetBytes(32);

        public static (string cipherB64, string ivB64) AesEncrypt(byte[] data, byte[] key)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256; aes.Key = key;
            aes.GenerateIV();
            using var enc = aes.CreateEncryptor();
            byte[] cipher = enc.TransformFinalBlock(data, 0, data.Length);
            return (Convert.ToBase64String(cipher), Convert.ToBase64String(aes.IV));
        }

        public static byte[] AesDecrypt(string cipherB64, byte[] key, string ivB64)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256; aes.Key = key; aes.IV = Convert.FromBase64String(ivB64);
            using var dec = aes.CreateDecryptor();
            byte[] cipher = Convert.FromBase64String(cipherB64);
            return dec.TransformFinalBlock(cipher, 0, cipher.Length);
        }

        public static string WrapKey(byte[] aesKey, string recipientPublicKeyB64)
        {
            using var rsa = ImportPublicKey(recipientPublicKeyB64);
            return Convert.ToBase64String(rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256));
        }

        public byte[] UnwrapKey(string wrappedB64)
            => _rsa.Decrypt(Convert.FromBase64String(wrappedB64), RSAEncryptionPadding.OaepSHA256);

        public void Dispose() => _rsa.Dispose();
    }
}
