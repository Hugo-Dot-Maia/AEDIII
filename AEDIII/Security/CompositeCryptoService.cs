using AEDIII.Interfaces;

namespace AEDIII.Security
{
    /// <summary>
    /// Chama dois ICryptoService em sequência:
    /// Encrypt: primeiro AES, depois XOR;
    /// Decrypt: primeiro XOR, depois AES.
    /// </summary>
    public class CompositeCryptoService : ICryptoService
    {
        private readonly ICryptoService _first;  // por exemplo, AES
        private readonly ICryptoService _second; // por exemplo, XOR

        public CompositeCryptoService(ICryptoService first, ICryptoService second)
        {
            _first = first;
            _second = second;
        }

        public byte[] Encrypt(byte[] plainData)
        {
            // 1) criptografa com o primeiro (AES)
            var step1 = _first.Encrypt(plainData);
            // 2) depois aplica o segundo (XOR)
            return _second.Encrypt(step1);
        }

        public byte[] Decrypt(byte[] cipherData)
        {
            // 1) desscriptografa com o segundo (XOR)
            var step1 = _second.Decrypt(cipherData);
            // 2) depois desscriptografa com o primeiro (AES)
            return _first.Decrypt(step1);
        }
    }
}
