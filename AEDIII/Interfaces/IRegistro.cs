namespace AEDIII.Interfaces
{
    public interface IRegistro
    {
        void SetId(int id);
        int GetId();
        byte[] ToByteArray();
        void FromByteArray(byte[] data);
    }
}
