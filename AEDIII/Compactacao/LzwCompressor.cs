namespace AEDIII.Compactacao
{
    /// <summary>
    /// Compressor baseado no algoritmo LZW (Lempel–Ziv–Welch).
    /// </summary>
    public class LzwCompressor : ICompressor
    {
        /// <summary>
        /// Comprime o arquivo de entrada usando LZW e grava a sequência de códigos resultante.
        /// </summary>
        /// <param name="inputPath">Caminho do arquivo original a ser comprimido.</param>
        /// <param name="outputPath">Caminho onde será gravado o arquivo compactado.</param>
        public void Compress(string inputPath, string outputPath)
        {
            // 1. Lê todos os bytes do arquivo de entrada
            //    Cada byte será tratado como um “símbolo” inicial para o dicionário.
            byte[] data = File.ReadAllBytes(inputPath);

            // 2. Inicializa o dicionário com todas as combinações de um único byte (0–255)
            //    A chave é a string que representa o caractere, o valor é o código numérico.
            var dict = new Dictionary<string, int>();
            for (int i = 0; i < 256; i++)
            {
                // Converte o inteiro i em um char e depois em string de comprimento 1
                dict[((char)i).ToString()] = i;
            }

            // 3. nextCode marcará o próximo código livre a ser atribuído ao dicionário
            int nextCode = 256;

            // 4. Variável 'w' armazena o prefixo atual (inicialmente vazio)
            string w = string.Empty;

            // 5. Lista de códigos inteiros que resultarão da compressão
            var codes = new List<int>();

            // 6. Percorre cada byte do arquivo
            foreach (var b in data)
            {
                // 6.1. Concatena o prefixo 'w' com o próximo caractere (== (char)b)
                string wc = w + (char)b;

                // 6.2. Se essa sequência (wc) já existe no dicionário, ele estende o prefixo
                if (dict.ContainsKey(wc))
                {
                    // - Atualiza 'w' para esse novo prefixo, aguardando mais caracteres
                    w = wc;
                }
                else
                {
                    // 6.3. Caso contrário, grava o código de 'w' na lista de saída
                    codes.Add(dict[w]);

                    // 6.4. Adiciona a nova sequência (wc) ao dicionário com o próximo código livre
                    dict[wc] = nextCode++;

                    // 6.5. Define 'w' como o caractere atual (reinicia o prefixo)
                    w = ((char)b).ToString();
                }
            }

            // 7. Se sobrou algum prefixo 'w' não gravado o código é adicionado
            if (!string.IsNullOrEmpty(w))
            {
                codes.Add(dict[w]);
            }

            // 8. Agora que geramos a lista de códigos, vamos gravá-los num arquivo
            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            // 8.1. Primeiro, grava quantos códigos iremos escrever (Int32)
            bw.Write(codes.Count);

            // 8.2. Em seguida, escreve cada código como um Int32
            //      (Obs.: poderíamos empacotar mais eficientemente usando menos bits, 
            //       mas aqui simplificamos e usamos 32 bits por código.)
            foreach (var code in codes)
            {
                bw.Write(code);
            }
            // Fim da escrita: BinaryWriter e FileStream serão fechados automaticamente
        }

        /// <summary>
        /// Descomprime um arquivo gerado por LzwCompressor e recupera o conteúdo original.
        /// </summary>
        /// <param name="inputPath">Caminho do arquivo compactado.</param>
        /// <param name="outputPath">Caminho onde será gravado o arquivo descompactado.</param>
        public void Decompress(string inputPath, string outputPath)
        {
            // 1. Abre o arquivo compactado para leitura binária
            using var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            // 2. Lê quantos códigos existem (Int32)
            int count = br.ReadInt32();

            // 3. Carrega todos os códigos em uma lista
            var codes = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                // Cada código foi gravado como Int32
                codes.Add(br.ReadInt32());
            }

            // 4. Reconstrói o dicionário inicial (códigos 0–255)
            var dict = new Dictionary<int, string>();
            for (int i = 0; i < 256; i++)
            {
                dict[i] = ((char)i).ToString();
            }
            int nextCode = 256;

            // 5. O primeiro código corresponde a uma string única no dicionário
            string w = dict[codes[0]];
            // 5.1. Converte essa string em bytes e adiciona à lista de resultado
            var result = new List<byte>(w.Select(c => (byte)c));

            // 6. Percorre o restante dos códigos (a partir do segundo)
            for (int i = 1; i < codes.Count; i++)
            {
                int k = codes[i];
                string entry;

                // 6.1. Se o dicionário já contém esse código, recupera a string mapeada
                if (dict.ContainsKey(k))
                {
                    entry = dict[k];
                }
                // 6.2. Caso especial: se k == nextCode, a sequência é: w + primeira letra de w
                else if (k == nextCode)
                {
                    entry = w + w[0];
                }
                else
                {
                    // Se for código inválido, algo deu errado
                    throw new InvalidOperationException($"Código inválido: {k}");
                }

                // 6.3. Concatena todos os bytes de 'entry' no resultado
                result.AddRange(entry.Select(c => (byte)c));

                // 6.4. Adiciona nova entrada ao dicionário: concatena w + primeiro caractere de 'entry'
                dict[nextCode++] = w + entry[0];

                // 6.5. Atualiza 'w' para a string atual
                w = entry;
            }

            // 7. Grava todos os bytes reconstruídos no arquivo de saída
            File.WriteAllBytes(outputPath, result.ToArray());
            // Fim da escrita: BinaryWriter e FileStream fecham-se automaticamente
        }
    }
}
