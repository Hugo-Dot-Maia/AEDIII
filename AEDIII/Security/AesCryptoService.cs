using AEDIII.Interfaces;
using System;
using System.IO;
using System.Security.Cryptography;

namespace AEDIII.Security
{
    public class AesCryptoService : ICryptoService
    {
        // 16 bytes de zero em Base64 → "AAAAAAAAAAAAAAAAAAAAAA=="
        private static readonly byte[] Key = Convert.FromBase64String(
            "AAAAAAAAAAAAAAAAAAAAAA=="
        );
        // mesmo IV de 16 bytes de zero
        private static readonly byte[] IV = Convert.FromBase64String(
            "AAAAAAAAAAAAAAAAAAAAAA=="
        );

        public byte[] Encrypt(byte[] plainData)
        {
            using var aes = Aes.Create();
            aes.Key = Key;    // 128-bit
            aes.IV = IV;     // 128-bit
            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(plainData, 0, plainData.Length);
            cs.FlushFinalBlock();
            return ms.ToArray();
        }

        public byte[] Decrypt(byte[] cipherData)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;
            using var ms = new MemoryStream(cipherData);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var br = new BinaryReader(cs);
            return br.ReadBytes(cipherData.Length);
        }
    }
}
