namespace AEDIII.Compactacao
{
    /// <summary>
    /// Compressor baseado no algoritmo LZW.
    /// </summary>
    public class LzwCompressor : ICompressor
    {
        public void Compress(string inputPath, string outputPath)
        {
            byte[] data = File.ReadAllBytes(inputPath);
            var dict = new Dictionary<string, int>();
            for (int i = 0; i < 256; i++)
                dict.Add(((char)i).ToString(), i);
            int nextCode = 256;

            string w = string.Empty;
            var codes = new List<int>();
            foreach (var b in data)
            {
                string wc = w + (char)b;
                if (dict.ContainsKey(wc))
                    w = wc;
                else
                {
                    codes.Add(dict[w]);
                    dict[wc] = nextCode++;
                    w = ((char)b).ToString();
                }
            }
            if (!string.IsNullOrEmpty(w))
                codes.Add(dict[w]);

            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);
            bw.Write(codes.Count);
            foreach (var code in codes)
                bw.Write(code);
        }

        public void Decompress(string inputPath, string outputPath)
        {
            using var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();
            var codes = new List<int>(count);
            for (int i = 0; i < count; i++)
                codes.Add(br.ReadInt32());

            var dict = new Dictionary<int, string>();
            for (int i = 0; i < 256; i++)
                dict[i] = ((char)i).ToString();
            int nextCode = 256;

            string w = dict[codes[0]];
            var result = new List<byte>(w.Select(c => (byte)c));

            for (int i = 1; i < codes.Count; i++)
            {
                int k = codes[i];
                string entry;
                if (dict.ContainsKey(k))
                    entry = dict[k];
                else if (k == nextCode)
                    entry = w + w[0];
                else
                    throw new InvalidOperationException($"Código inválido: {k}");

                result.AddRange(entry.Select(c => (byte)c));
                dict[nextCode++] = w + entry[0];
                w = entry;
            }

            File.WriteAllBytes(outputPath, result.ToArray());
        }
    }
}
