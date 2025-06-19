using System.Text;

namespace AEDIII.Compactacao
{
    /// <summary>
    /// Compressor baseado no algoritmo de Huffman.
    /// </summary>
    public class HuffmanCompressor : ICompressor
    {
        private class Node : IComparable<Node>
        {
            public byte? Symbol { get; set; }
            public int Frequency { get; set; }
            public Node Left { get; set; }
            public Node Right { get; set; }
            public int CompareTo(Node other) => Frequency - other.Frequency;
        }
        public void Compress(string inputPath, string outputPath)
        {
            byte[] data = File.ReadAllBytes(inputPath); 
            var freqMap = new Dictionary<byte, int>();
            foreach (var b in data)
                freqMap[b] = freqMap.GetValueOrDefault(b) + 1;

            var pq = new PriorityQueue<Node, int>();
            foreach (var kv in freqMap)
                pq.Enqueue(new Node { Symbol = kv.Key, Frequency = kv.Value }, kv.Value);
            while (pq.Count > 1)
            {
                var left = pq.Dequeue();
                var right = pq.Dequeue();
                pq.Enqueue(new Node
                {
                    Symbol = null,
                    Frequency = left.Frequency + right.Frequency,
                    Left = left,
                    Right = right
                }, left.Frequency + right.Frequency);
            }
            Node root = pq.Dequeue();

            var codes = new Dictionary<byte, string>();
            BuildCodes(root, "", codes);

            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);

            bw.Write((int)codes.Count);

            foreach (var kv in codes)
            {
                byte symbol = kv.Key;
                string code = kv.Value;
                int bitLen = code.Length;
                int byteLen = (bitLen + 7) / 8;

                bw.Write(symbol);
                bw.Write((byte)bitLen);

                byte[] codeBytes = new byte[byteLen];
                for (int i = 0; i < bitLen; i++)
                {
                    if (code[i] == '1')
                    {
                        int byteIndex = i / 8;
                        int bitIndex = 7 - (i % 8);
                        codeBytes[byteIndex] |= (byte)(1 << bitIndex);
                    }
                }
                bw.Write((byte)byteLen);
                bw.Write(codeBytes);
            }

            int totalDataBits = data.Sum(b => codes[b].Length);
            bw.Write(totalDataBits);

            uint bitBuffer = 0;
            int bitCount = 0;

            foreach (var b in data)
            {
                string code = codes[b];
                foreach (char c in code)
                {
                    bitBuffer = (bitBuffer << 1) | (uint)(c == '1' ? 1 : 0);
                    bitCount++;

                    if (bitCount == 8)
                    {
                        bw.Write((byte)bitBuffer);
                        bitBuffer = 0;
                        bitCount = 0;
                    }
                }
            }
            if (bitCount > 0)
            {
                bitBuffer <<= (8 - bitCount);
                bw.Write((byte)bitBuffer);
            }
        }

        public void Decompress(string inputPath, string outputPath)
        {
            using var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);

            // 1) Lê quantidade de símbolos
            int symbolCount = br.ReadInt32();
            var codes = new Dictionary<string, byte>(symbolCount);

            // 2) Lê cada entrada do dicionário: símbolo, tamanho em bits, comprimento em bytes e bytes empacotados
            for (int i = 0; i < symbolCount; i++)
            {
                byte symbol = br.ReadByte();
                byte bitLen = br.ReadByte();
                byte byteLen = br.ReadByte();
                byte[] codeBytes = br.ReadBytes(byteLen);

                // Desempacota o código em uma string de '0'/'1'
                var sbCode = new StringBuilder(bitLen);
                for (int bitIndex = 0; bitIndex < bitLen; bitIndex++)
                {
                    int byteIndex = bitIndex / 8;
                    int shift = 7 - (bitIndex % 8);
                    bool bit = ((codeBytes[byteIndex] >> shift) & 1) == 1;
                    sbCode.Append(bit ? '1' : '0');
                }

                codes[sbCode.ToString()] = symbol;
            }

            // 3) Lê o total de bits de dados
            int totalDataBits = br.ReadInt32();
            int dataBytes = (totalDataBits + 7) / 8;

            // 4) Reconstrói o fluxo de bits do arquivo compactado
            var bitList = new List<bool>(totalDataBits);
            for (int i = 0; i < dataBytes; i++)
            {
                byte b = br.ReadByte();
                for (int j = 0; j < 8 && bitList.Count < totalDataBits; j++)
                {
                    bool bit = ((b >> (7 - j)) & 1) == 1;
                    bitList.Add(bit);
                }
            }

            // 5) Decodifica o fluxo de bits usando o dicionário
            var result = new List<byte>();
            var sb = new StringBuilder();
            foreach (bool bit in bitList)
            {
                sb.Append(bit ? '1' : '0');
                if (codes.TryGetValue(sb.ToString(), out byte sym))
                {
                    result.Add(sym);
                    sb.Clear();
                }
            }

            // 6) Grava os bytes descompactados
            File.WriteAllBytes(outputPath, result.ToArray());
        }
        private void BuildCodes(Node node, string prefix, Dictionary<byte, string> codes)
        {
            if (node == null) return;
            if (node.Symbol.HasValue)
                codes[node.Symbol.Value] = prefix.Length > 0 ? prefix : "0";
            else
            {
                BuildCodes(node.Left, prefix + "0", codes);
                BuildCodes(node.Right, prefix + "1", codes);
            }
        }
    }

}
