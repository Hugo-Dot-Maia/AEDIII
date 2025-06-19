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
            {
                // Se já existir, incrementa, senão, GetValueOrDefault(b) retorna 0, e somamos 1
                freqMap[b] = freqMap.GetValueOrDefault(b) + 1;
            }

            // 3. Cria nós folhas para cada símbolo presente e insere numa fila de prioridade
            //    - Cada nó armazena: Symbol (o byte), Frequency (quantas vezes apareceu) e ponteiros para filhos.
            var pq = new PriorityQueue<Node, int>();
            foreach (var kv in freqMap)
            {
                // Para cada par (byte, frequência), criamos um nó folha
                var folha = new Node
                {
                    Symbol = kv.Key,        // o byte real
                    Frequency = kv.Value,   // sua contagem
                    Left = null,
                    Right = null
                };
                // Enfileira na fila de prioridade, usando a frequência como chave de prioridade
                pq.Enqueue(folha, folha.Frequency);
            }

            // 4. Constrói a árvore de Huffman: retira duas menores frequências, combina em um nó interno,
            //    e reinsere até sobrar apenas a raiz.
            while (pq.Count > 1)
            {
                // a) Remover os dois nós de frequência mais baixa
                var left = pq.Dequeue();
                var right = pq.Dequeue();

                // b) Cria um novo nó interno cuja frequência é a soma das duas
                var parent = new Node
                {
                    Symbol = null,                  // nó interno não representa símbolo
                    Frequency = left.Frequency + right.Frequency,
                    Left = left,
                    Right = right
                };
                // c) Insere o nó interno de volta na fila, ordenado pela frequência combinada
                pq.Enqueue(parent, parent.Frequency);
            }

            // 5. A fila agora contém apenas um nó: a raiz da árvore de Huffman
            Node root = pq.Dequeue();

            // 6. Gera os códigos binários (sequências de '0' e '1') para cada símbolo, 
            //    percorrendo a árvore recursivamente
            var codes = new Dictionary<byte, string>();
            BuildCodes(root, "", codes);
            //    - BuildCodes constrói recursivamente as strings:
            //      prefix + "0" para ramo esquerdo, prefix + "1" para ramo direito.
            //    - Ao chegar em um nó folha (Symbol != null), grava codes[símbolo] = prefix.

            // 7. Abre o arquivo de saída para escrita binária
            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);

            // 8. ESCREVE O CABEÇALHO DO ARQUIVO COMPACTADO
            // 8.1. Número de símbolos distintos (Int32)
            bw.Write((int)codes.Count);

            // 8.2. Para cada par (símbolo, código-string), empacota o código em bytes:
            foreach (var kv in codes)
            {
                byte symbol = kv.Key;       // o byte que representa o símbolo
                string code = kv.Value;     // string de '0' e '1' para esse símbolo
                int bitLen = code.Length;   // quantos bits esse código tem
                int byteLen = (bitLen + 7) / 8; // quantos bytes serão necessários para armazenar esses bits

                // 8.2.1. Escreve o símbolo (1 byte)
                bw.Write(symbol);
                // 8.2.2. Escreve o comprimento do código em bits (1 byte)
                bw.Write((byte)bitLen);
                // 8.2.3. Escreve quantos bytes seguem para este código (1 byte)
                bw.Write((byte)byteLen);

                // 8.2.4. Converte a string de '0'/'1' em um array de bytes:
                //        Cada 8 bits formam um byte. Se bitLen não for múltiplo de 8, o último byte é "encaixado" à esquerda.
                byte[] codeBytes = new byte[byteLen];
                for (int i = 0; i < bitLen; i++)
                {
                    if (code[i] == '1')
                    {
                        int byteIndex = i / 8;            // índice de qual byte no array
                        int bitIndex = 7 - (i % 8);      // posição do bit dentro desse byte (da esquerda para a direita)
                        codeBytes[byteIndex] |= (byte)(1 << bitIndex);
                    }
                }
                // 8.2.5. Escreve o array de bytes que codifica o código huffman para esse símbolo
                bw.Write(codeBytes);
            }

            // 9. Calcula e grava quantos bits totais o fluxo de dados usará
            //    Esse valor (Int32) dirá, na descompressão, quantos bits de payload devemos ler.
            int totalDataBits = data.Sum(b => codes[b].Length);
            bw.Write(totalDataBits);

            // 10. CODIFICA OS DADOS “ON-THE-FLY”: percorre cada byte original e escreve seu código em bits
            uint bitBuffer = 0;  // buffer temporário de até 32 bits
            int bitCount = 0;    // quantos bits estão atualmente no buffer

            foreach (var b in data)
            {
                // 10.1. Recupera a string de bits para esse byte
                string code = codes[b];

                // 10.2. Para cada caractere '0'/'1' nessa string:
                foreach (char c in code)
                {
                    // Shift do buffer 1 bit à esquerda e insere o próximo bit no LSB
                    bitBuffer = (bitBuffer << 1) | (uint)(c == '1' ? 1 : 0);
                    bitCount++;

                    // Se já acumulamos 8 bits (= 1 byte), gravamos esse byte
                    if (bitCount == 8)
                    {
                        bw.Write((byte)bitBuffer);
                        bitBuffer = 0;
                        bitCount = 0;
                    }
                }
            }

            // 10.3. Se ainda sobrar algum bit (bitCount < 8), deslocamo-lo para as posições mais altas
            //       dentro de um byte (com zeros à direita) e gravamos esse último byte parcial.
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
