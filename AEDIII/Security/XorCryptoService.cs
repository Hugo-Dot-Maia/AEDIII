using AEDIII.Interfaces;

namespace AEDIII.Security
{
    public class XorCryptoService : ICryptoService
    {
        private readonly byte _key = 0xAA;
        public byte[] Encrypt(byte[] plainData)
            => plainData.Select(b => (byte)(b ^ _key)).ToArray();
        public byte[] Decrypt(byte[] cipherData)
            => Encrypt(cipherData); 
    }
}
