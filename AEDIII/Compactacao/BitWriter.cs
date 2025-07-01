using System.Text;

namespace AEDIII.Compactacao
{
    public class BitWriter : IDisposable
    {
        private readonly BinaryWriter _bw;
        private uint _buffer = 0;
        private int _bitCount = 0;

        public BitWriter(Stream output)
        {
            _bw = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        }

        public void WriteBits(uint bits, int count)
        {
            _buffer = (_buffer << count) | (bits & ((1u << count) - 1));
            _bitCount += count;

            while (_bitCount >= 8)
            {
                _bitCount -= 8;
                byte b = (byte)(_buffer >> _bitCount);
                _bw.Write(b);
                _buffer &= (uint)((1 << _bitCount) - 1);
            }
        }

        public void Flush()
        {
            if (_bitCount > 0)
            {
                byte b = (byte)(_buffer << (8 - _bitCount));
                _bw.Write(b);
                _buffer = 0;
                _bitCount = 0;
            }
        }

        public void Dispose()
        {
            // só fecha o BinaryWriter, não fecha o FileStream
            _bw.Dispose();
        }
    }


}
