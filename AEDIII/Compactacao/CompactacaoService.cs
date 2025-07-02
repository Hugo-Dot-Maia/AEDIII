using AEDIII.Interfaces;
using System.Diagnostics;
using System.Text;

namespace AEDIII.Compactacao
{
    /// <summary>
    /// Serviço para orquestrar compressão e descompressão via linha de comando ou API.
    /// </summary>
    public class CompactacaoService
    {
        private readonly HuffmanCompressor _huffman;
        private readonly LzwCompressor _lzw;
        private readonly ICryptoService _crypto;

        public CompactacaoService(HuffmanCompressor huffman, LzwCompressor lzw, ICryptoService crypto)
        {
            _huffman = huffman;
            _lzw = lzw;
            _crypto = crypto;
        }


        private void DecryptDbFile(string encryptedPath, string plainPath)
        {
            using var fsIn = new FileStream(encryptedPath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fsIn);
            using var fsOut = new FileStream(plainPath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fsOut);

            int lastId = br.ReadInt32();
            long freelist = br.ReadInt64();
            bw.Write(lastId);
            bw.Write(freelist);

            while (fsIn.Position < fsIn.Length)
            {
                byte lapide = br.ReadByte();
                short size = br.ReadInt16();
                byte[] cipher = br.ReadBytes(size);

                byte[] plain = _crypto.Decrypt(cipher);

                bw.Write(lapide);
                bw.Write((short)plain.Length);
                bw.Write(plain);
            }
        }

        private void EncryptFile(string inputPath, string outputPath)
        {
            // 1) Lê TODO o arquivo “plain” em memória
            byte[] plain = File.ReadAllBytes(inputPath);

            // 2) Aplica a criptografia composta (AES → XOR)
            byte[] cipher = _crypto.Encrypt(plain);

            // 3) Grava os bytes cifrados no disco
            File.WriteAllBytes(outputPath, cipher);
        }



        /// <summary>
        /// Executa compressão comparativa e retorna relatório.
        /// </summary>
        public void RunCompression(string dbPath, int version)
        {
            string tempPlain = Path.ChangeExtension(dbPath, ".plain.tmp");
            DecryptDbFile(dbPath, tempPlain);

            string huffPath = Path.ChangeExtension(dbPath, $"Huffman{version}.db");
            string lzwPath = Path.ChangeExtension(dbPath, $"LZW{version}.db");

            var sw = Stopwatch.StartNew();
            _huffman.Compress(tempPlain, huffPath);
            sw.Stop();
            Console.WriteLine($"Huffman: {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            _lzw.Compress(tempPlain, lzwPath);
            sw.Stop();
            Console.WriteLine($"LZW: {sw.ElapsedMilliseconds} ms");

            // 4) Cálculo das taxas baseado no tamanho do PLAIN
            long origSize = new FileInfo(tempPlain).Length;
            long huffSize = new FileInfo(huffPath).Length;
            long lzwSize = new FileInfo(lzwPath).Length;
            Console.WriteLine($"Taxa Huffman: {((origSize - huffSize) * 100.0 / origSize):F2}%");
            Console.WriteLine($"Taxa LZW:     {((origSize - lzwSize) * 100.0 / origSize):F2}%");

            // 5) opcional: apaga o arquivo plain
            File.Delete(tempPlain);

            // 6) criptografa os .db comprimidos se quiser
            EncryptFile(huffPath, huffPath + ".enc");
            EncryptFile(lzwPath, lzwPath + ".enc");
        }


        /// <summary>
        /// Executa descompressão comparativa e retorna relatório.
        /// </summary>
        public void RunDecompression(string dbPath, int version)
        {
            string huffPath = Path.ChangeExtension(dbPath, $"Huffman{version}.db");
            string lzwPath = Path.ChangeExtension(dbPath, $"LZW{version}.db");
            string outH = Path.ChangeExtension(dbPath, $"Huffman{version}_dec.db");
            string outL = Path.ChangeExtension(dbPath, $"LZW{version}_dec.db");

            // 1) Descompressão Huffman
            var sw = Stopwatch.StartNew();
            _huffman.Decompress(huffPath, outH);
            sw.Stop();
            Console.WriteLine($"Descompressão Huffman: {sw.ElapsedMilliseconds} ms");

            // 2) Descompressão LZW
            sw.Restart();
            _lzw.Decompress(lzwPath, outL);
            sw.Stop();
            Console.WriteLine($"Descompressão LZW:     {sw.ElapsedMilliseconds} ms");

            Console.WriteLine($"Arquivos descompactados gerados:");
            Console.WriteLine($"  - {outH}");
            Console.WriteLine($"  - {outL}");
        }
    }


}
