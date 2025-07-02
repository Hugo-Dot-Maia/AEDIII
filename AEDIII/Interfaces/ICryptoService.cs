namespace AEDIII.Interfaces
{
    public interface ICryptoService
    {
        byte[] Encrypt(byte[] plainData);
        byte[] Decrypt(byte[] cipherData);
    }

}
