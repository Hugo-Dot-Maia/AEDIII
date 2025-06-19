using System;
using System.Collections.Generic;
using System.IO;

namespace AEDIII.Indexes
{
    /// <summary>
    /// Índice B+ para chaves inteiras (ID) mapeando para offsets no arquivo de dados.
    /// Ordem m = 4 => cada nó interno pode ter até 4 ponteiros a filhos e até 3 chaves.
    /// Suporta operações de busca, inserção (com split) e exclusão de chaves em folhas com fusão.
    /// </summary>
    public class BPlusTreeIndex
    {
        private const int Order = 4;                 // Ordem da árvore: número máximo de filhos
        private const int MaxKeys = Order - 1;       // Número máximo de chaves em um nó (3)
        private const int MinKeys = 2;               // Número mínimo de chaves em um nó (ceil((m-1)/2))

        // Streams para leitura/escrita do arquivo de índice (.idx)
        private FileStream file;
        private BinaryReader reader;
        private BinaryWriter writer;

        // Offsets no arquivo .idx
        private long rootOffset;       // Posição da raiz da árvore no arquivo
        private long nextFreeOffset;   // Próximo offset livre para alocar novo nó
        private readonly string indexPath;

        public bool IsEmpty() => rootOffset < 0;
        public BPlusTreeIndex(string indexName)
        {
            indexPath = Path.Combine("./dados/", indexName + ".idx");
            Directory.CreateDirectory("./dados");

            file = new FileStream(indexPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            reader = new BinaryReader(file);
            writer = new BinaryWriter(file);

            if (file.Length == 0)
            {
                rootOffset = -1;
                nextFreeOffset = HeaderSize;
                file.Seek(0, SeekOrigin.Begin);
                writer.Write(rootOffset);
                writer.Write(nextFreeOffset);
                writer.Flush();
            }
            else
            {
                file.Seek(0, SeekOrigin.Begin);
                rootOffset = reader.ReadInt64();
                nextFreeOffset = reader.ReadInt64();
            }
        }

        /// <summary>
        /// Busca um offset de dados pela chave. Retorna -1 se não encontrada.
        /// Percorre a árvore da raiz até a folha.
        /// </summary>
        public long Search(int key)
        {
            // Se raíz = -1 => árvore vazia
            if (rootOffset < 0) return -1;
            long current = rootOffset;

            // Percorre até encontrar nó folha
            while (true)
            {
                Node node = ReadNode(current);

                if (node.IsLeaf)
                {
                    // Se for folha, varre todas as chaves na folha em busca da key
                    for (int i = 0; i < node.NumKeys; i++)
                    {
                        if (node.Keys[i] == key)
                            return node.Children[i]; // Retorna offset do registro
                    }
                    return -1; // Não encontrou na folha
                }

                // Caso nó interno, determina qual filho seguir: encontra primeiro índice i onde key < Keys[i]
                int idx = 0;
                while (idx < node.NumKeys && key >= node.Keys[idx]) idx++;
                // Atualiza current para o offset do filho apropriado
                current = node.Children[idx];
            }
        }

        /// <summary>
        /// Insere uma nova chave (key) com seu offset de dados (dataOffset).
        /// Rejeita duplicatas, faz split de nós quando necessário, e pode promover um novo root.
        /// </summary>
        public void Insert(int key, long dataOffset)
        {
            // Rejeita chaves duplicadas: se já existe, lança exceção
            if (Search(key) >= 0)
                throw new InvalidOperationException($"Chave {key} já existe no índice.");

            // Se árvore vazia, cria uma única folha como raiz
            if (rootOffset < 0)
            {
                Node leaf = new Node { IsLeaf = true, NumKeys = 1 };
                leaf.Keys[0] = key;
                leaf.Children[0] = dataOffset; // Em folhas, Children armazena offsets de dados
                leaf.NextLeaf = -1;           // Ponteiro para próxima folha (não há outra)

                // Aloca espaço para esse nó e grava no arquivo
                rootOffset = AllocateNode();
                WriteNode(rootOffset, leaf);
                UpdateHeader();
                return;
            }

            // Caso não vazio, insere recursivamente, podendo retornar SplitResult se precisar dividir
            SplitResult split = InsertRecursive(rootOffset, key, dataOffset);

            // Se houve split na raiz, cria novo nó raiz interno
            if (split != null)
            {
                Node newRoot = new Node { IsLeaf = false, NumKeys = 1 };
                newRoot.Keys[0] = split.PromotedKey;
                newRoot.Children[0] = rootOffset;
                newRoot.Children[1] = split.NewNodeOffset;

                long newRootOff = AllocateNode();
                WriteNode(newRootOff, newRoot);

                // Atualiza o rootOffset e grava no cabeçalho
                rootOffset = newRootOff;
                UpdateHeader();
            }
        }

        private SplitResult InsertRecursive(long offset, int key, long dataOffset)
        {
            Node node = ReadNode(offset);
            if (node.IsLeaf)
            {
                var keys = new List<int>(node.NumKeys + 1);
                var vals = new List<long>(node.NumKeys + 1);
                for (int i = 0; i < node.NumKeys; i++) { keys.Add(node.Keys[i]); vals.Add(node.Children[i]); }
                int pos = keys.FindIndex(k => key < k);
                if (pos < 0)
                { keys.Add(key); vals.Add(dataOffset); }
                else
                { keys.Insert(pos, key); vals.Insert(pos, dataOffset); }

                if (keys.Count <= MaxKeys)
                {
                    // write back leaf
                    node.NumKeys = keys.Count;
                    for (int i = 0; i < keys.Count; i++) { node.Keys[i] = keys[i]; node.Children[i] = vals[i]; }
                    WriteNode(offset, node);
                    return null;
                }
                // split leaf
                int mid = keys.Count / 2;
                Node right = new Node { IsLeaf = true, NumKeys = keys.Count - mid };
                for (int i = 0; i < right.NumKeys; i++) { right.Keys[i] = keys[mid + i]; right.Children[i] = vals[mid + i]; }
                right.NextLeaf = node.NextLeaf;

                node.NumKeys = mid;
                for (int i = 0; i < mid; i++) { node.Keys[i] = keys[i]; node.Children[i] = vals[i]; }
                node.NextLeaf = AllocateNode();
                long rightOff = node.NextLeaf;
                WriteNode(offset, node);
                WriteNode(rightOff, right);
                return new SplitResult { PromotedKey = right.Keys[0], NewNodeOffset = rightOff };
            }
            else
            {
                int idx = 0;
                while (idx < node.NumKeys && key >= node.Keys[idx]) idx++;
                var childSplit = InsertRecursive(node.Children[idx], key, dataOffset);
                if (childSplit == null) return null;

                var ikeys = new List<int>(node.NumKeys + 1);
                var childs = new List<long>(node.NumKeys + 2);
                for (int i = 0; i < node.NumKeys; i++) ikeys.Add(node.Keys[i]);
                for (int i = 0; i <= node.NumKeys; i++) childs.Add(node.Children[i]);

                ikeys.Insert(idx, childSplit.PromotedKey);
                childs.Insert(idx + 1, childSplit.NewNodeOffset);

                if (ikeys.Count <= MaxKeys)
                {
                    node.NumKeys = ikeys.Count;
                    for (int i = 0; i < ikeys.Count; i++) { node.Keys[i] = ikeys[i]; node.Children[i] = childs[i]; }
                    node.Children[ikeys.Count] = childs[ikeys.Count];
                    WriteNode(offset, node);
                    return null;
                }
                // split internal
                int mid2 = ikeys.Count / 2;
                int promote = ikeys[mid2];
                Node right2 = new Node { IsLeaf = false, NumKeys = ikeys.Count - mid2 - 1 };
                for (int i = 0; i < right2.NumKeys; i++) right2.Keys[i] = ikeys[mid2 + 1 + i];
                for (int i = 0; i <= right2.NumKeys; i++) right2.Children[i] = childs[mid2 + 1 + i];

                node.NumKeys = mid2;
                for (int i = 0; i < mid2; i++) { node.Keys[i] = ikeys[i]; node.Children[i] = childs[i]; }
                node.Children[mid2] = childs[mid2];

                long rightOff2 = AllocateNode();
                WriteNode(offset, node);
                WriteNode(rightOff2, right2);

                return new SplitResult { PromotedKey = promote, NewNodeOffset = rightOff2 };
            }
        }

        /// <summary>
        /// Exclui uma chave do índice. Se ocorrer underflow em folha, faz fusão com irmão.
        /// </summary>
        public bool Delete(int key)
        {
            // 1) Se árvore vazia, nada a remover
            if (rootOffset < 0) return false;

            // 2) Pilha para armazenar o caminho (offset, nó, índice no pai)
            var path = new List<(long offset, Node node, int idx)>();
            long current = rootOffset;

            // 3) Percorre até a folha para encontrar a chave
            while (true)
            {
                Node node = ReadNode(current);
                if (node.IsLeaf)
                {
                    path.Add((current, node, -1)); // -1 indica que não há índice no pai para folha
                    break;
                }
                int childIdx = 0;
                while (childIdx < node.NumKeys && key >= node.Keys[childIdx]) childIdx++;
                path.Add((current, node, childIdx));
                current = node.Children[childIdx];
            }

            // 4) No nó folha, tenta remover a chave
            var (leafOff, leafNode, _) = path[path.Count - 1];
            int i;
            for (i = 0; i < leafNode.NumKeys; i++)
                if (leafNode.Keys[i] == key) break;
            if (i == leafNode.NumKeys) return false; // não encontrou

            // 5) Desloca as chaves/ponteiros seguintes para “cobrir” o espaço
            for (int j = i; j < leafNode.NumKeys - 1; j++)
            {
                leafNode.Keys[j] = leafNode.Keys[j + 1];
                leafNode.Children[j] = leafNode.Children[j + 1];
            }
            leafNode.NumKeys--;
            WriteNode(leafOff, leafNode);

            // 6) Se não ocorreu underflow (NumKeys >= MinKeys) ou se é a única folha (raiz), fim
            if (leafNode.NumKeys >= MinKeys || path.Count == 1)
                return true;

            // 7) Em caso de underflow, faz fusão da folha com um irmão adjacente
            var (parentOff, parentNode, idxInParent) = path[path.Count - 2];

            // 7.1) Determina irmão: prefere irmão à esquerda, se existir
            long siblingOff;
            Node sibling;
            bool isLeft = false;
            if (idxInParent > 0)
            {
                // irmão à esquerda
                siblingOff = parentNode.Children[idxInParent - 1];
                sibling = ReadNode(siblingOff);
                isLeft = true;
            }
            else
            {
                // irmão à direita
                siblingOff = parentNode.Children[idxInParent + 1];
                sibling = ReadNode(siblingOff);
            }

            // 7.2) Função de fusão: une chaves/ponteiros de leafNode e sibling
            Node dest = isLeft ? sibling : leafNode; // nó que receberá todas as chaves
            Node src = isLeft ? leafNode : sibling;  // nó que será “esvaziado"
            int destKeys = dest.NumKeys;

            for (int k = 0; k < src.NumKeys; k++)
            {
                dest.Keys[destKeys + k] = src.Keys[k];
                dest.Children[destKeys + k] = src.Children[k];
            }
            dest.NumKeys += src.NumKeys;

            // Ajusta ponteiro NextLeaf para manter encadeamento
            dest.NextLeaf = isLeft ? sibling.NextLeaf : leafNode.NextLeaf;

            // Grava o nó destino já mesclado
            long destOff = isLeft ? siblingOff : leafOff;
            WriteNode(destOff, dest);

            // 7.3) Remove a chave correspondente do nó pai e desloca filhos subsequentes
            int removeIdx = isLeft ? idxInParent : idxInParent + 1;
            for (int k = removeIdx - (isLeft ? 0 : 1); k < parentNode.NumKeys - 1; k++)
            {
                parentNode.Keys[k] = parentNode.Keys[k + 1];
                parentNode.Children[k + 1] = parentNode.Children[k + 2];
            }
            parentNode.NumKeys--;
            WriteNode(parentOff, parentNode);

            // 7.4) Se o nó pai se tornar vazio e for raiz, promove único filho
            if (parentOff == rootOffset && parentNode.NumKeys == 0)
            {
                rootOffset = parentNode.Children[0];
                UpdateHeader();
            }

            return true;
        }

        // Close file handles
        public void Close()
        {
            writer.Close(); reader.Close(); file.Close();
        }

        private void UpdateHeader()
        {
            file.Seek(0, SeekOrigin.Begin);
            writer.Write(rootOffset);
            writer.Write(nextFreeOffset);
            writer.Flush();
        }

        private long AllocateNode()
        {
            long off = nextFreeOffset;
            nextFreeOffset += GetNodeSize();
            UpdateHeader();
            return off;
        }

        private int GetNodeSize() => 1 + sizeof(int) + (MaxKeys * sizeof(int)) + (Order * sizeof(long)) + sizeof(long);

        private Node ReadNode(long offset)
        {
            file.Seek(offset, SeekOrigin.Begin);
            Node n = new Node();
            n.IsLeaf = reader.ReadByte() == 1;
            n.NumKeys = reader.ReadInt32();
            for (int i = 0; i < MaxKeys; i++) n.Keys[i] = reader.ReadInt32();
            for (int i = 0; i < Order; i++) n.Children[i] = reader.ReadInt64();
            n.NextLeaf = reader.ReadInt64();
            return n;
        }

        private void WriteNode(long offset, Node node)
        {
            file.Seek(offset, SeekOrigin.Begin);
            writer.Write(node.IsLeaf ? (byte)1 : (byte)0);
            writer.Write(node.NumKeys);
            for (int i = 0; i < MaxKeys; i++) writer.Write(node.Keys[i]);
            for (int i = 0; i < Order; i++) writer.Write(node.Children[i]);
            writer.Write(node.NextLeaf);
            writer.Flush();
        }

        private class Node
        {
            public bool IsLeaf;
            public int NumKeys;
            public int[] Keys = new int[MaxKeys];
            public long[] Children = new long[Order];
            public long NextLeaf;
        }

        private class SplitResult
        {
            public int PromotedKey;
            public long NewNodeOffset;
        }
    }
}
