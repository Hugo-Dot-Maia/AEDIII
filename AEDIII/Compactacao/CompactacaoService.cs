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

        public CompactacaoService(HuffmanCompressor huffman, LzwCompressor lzw)
        {
            _huffman = huffman;
            _lzw = lzw;
        }


        /// <summary>
        /// Executa compressão comparativa e retorna relatório.
        /// </summary>
        public void RunCompression(string dbPath, int version)
        {
            string huffPath = Path.ChangeExtension(dbPath, $"Huffman{version}.db");
            string lzwPath = Path.ChangeExtension(dbPath, $"LZW{version}.db");
            // Medir e executar Huffman
            var sw = Stopwatch.StartNew();
            _huffman.Compress(dbPath, huffPath);
            sw.Stop();
            Console.WriteLine($"Huffman: {sw.ElapsedMilliseconds} ms");
            // Medir e executar LZW
            sw.Restart();
            _lzw.Compress(dbPath, lzwPath);
            sw.Stop();
            Console.WriteLine($"LZW: {sw.ElapsedMilliseconds} ms");
            // Calcular taxas de compressão
            long origSize = new FileInfo(dbPath).Length;
            long huffSize = new FileInfo(huffPath).Length;
            long lzwSize = new FileInfo(lzwPath).Length;
            Console.WriteLine($"Taxa Huffman: {((origSize - huffSize) * 100.0 / origSize):F2}%");
            Console.WriteLine($"Taxa LZW:    {((origSize - lzwSize) * 100.0 / origSize):F2}%");
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
        }
    }


}
