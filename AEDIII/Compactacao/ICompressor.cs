namespace AEDIII.Compactacao
{
    /// <summary>
    /// Interface para compressores lossless.
    /// Define métodos de compressão e descompressão.
    /// </summary>
    public interface ICompressor
    {
        /// <summary>
        /// Realiza compressão do arquivo de entrada para o de saída.
        /// </summary>
        /// <param name="inputPath">Caminho do arquivo original.</param>
        /// <param name="outputPath">Caminho do arquivo compactado a ser criado.</param>
        void Compress(string inputPath, string outputPath);

        /// <summary>
        /// Realiza descompressão do arquivo de entrada para o de saída.
        /// </summary>
        /// <param name="inputPath">Caminho do arquivo compactado.</param>
        /// <param name="outputPath">Caminho do arquivo descompactado.</param>
        void Decompress(string inputPath, string outputPath);
    }
}
