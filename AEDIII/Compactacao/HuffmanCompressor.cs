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
                var parent = new Node
                {
                    Symbol = null,
                    Frequency = left.Frequency + right.Frequency,
                    Left = left,
                    Right = right
                };
                pq.Enqueue(parent, parent.Frequency);
            }
            Node root = pq.Dequeue();

            var codes = new Dictionary<byte, string>();
            BuildCodes(root, "", codes);

            var bitList = new List<bool>(data.Length * 8);
            foreach (var b in data)
                foreach (char bit in codes[b])
                    bitList.Add(bit == '1');

            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);

            bw.Write(codes.Count);
            foreach (var kv in codes)
            {
                bw.Write(kv.Key);
                bw.Write(kv.Value);
            }
            bw.Write(bitList.Count);

            int idx = 0;
            while (idx < bitList.Count)
            {
                byte b = 0;
                for (int i = 0; i < 8 && idx < bitList.Count; i++, idx++)
                    if (bitList[idx]) b |= (byte)(1 << (7 - i));
                bw.Write(b);
            }
        }

        public void Decompress(string inputPath, string outputPath)
        {
            using var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);

            int symbolCount = br.ReadInt32();
            var codes = new Dictionary<string, byte>(symbolCount);
            for (int i = 0; i < symbolCount; i++)
            {
                byte symbol = br.ReadByte();
                string code = br.ReadString();
                codes[code] = symbol;
            }

            int totalBits = br.ReadInt32();
            var bitList = new List<bool>(totalBits);
            while (bitList.Count < totalBits)
            {
                byte b = br.ReadByte();
                for (int i = 0; i < 8 && bitList.Count < totalBits; i++)
                    bitList.Add(((b >> (7 - i)) & 1) == 1);
            }

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
