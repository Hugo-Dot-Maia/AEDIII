namespace AEDIII.Compactacao
{
    public class BitReader : IDisposable
    {
        private readonly BinaryReader _br;
        private uint _buffer = 0;
        private int _bitCount = 0;

        public BitReader(Stream input)
        {
            _br = new BinaryReader(input);
        }

        public uint ReadBits(int count)
        {
            while (_bitCount < count)
            {
                byte next = _br.ReadByte();
                _buffer = (_buffer << 8) | next;
                _bitCount += 8;
            }

            int shift = _bitCount - count;
            uint mask = (uint)((1 << count) - 1);
            uint result = (_buffer >> shift) & mask;
            _bitCount -= count;
            _buffer &= (uint)((1 << _bitCount) - 1);
            return result;
        }

        public void Dispose() => _br.Dispose();
    }


}
