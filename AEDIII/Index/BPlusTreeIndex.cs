using System;
using System.Collections.Generic;
using System.IO;

namespace AEDIII.Indexes
{
    /// <summary>
    /// B+ Tree index on int keys (Id) mapping to data file offsets.
    /// Order m = 4 => max children = 4, max keys = 3.
    /// Implements insertion, search, and deletion with underflow merge.
    /// </summary>
    public class BPlusTreeIndex
    {
        private const int Order = 4;
        private const int MaxKeys = Order - 1; // 3 keys per node
        private const int MinKeys = 2; // ceil((4-1)/2)=2

        private FileStream file;
        private BinaryReader reader;
        private BinaryWriter writer;

        private long rootOffset;
        private long nextFreeOffset;
        private readonly string indexPath;

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

        private const int HeaderSize = sizeof(long) + sizeof(long);

        /// <summary>
        /// Search by key, returns data offset or -1 if not found
        /// </summary>
        public long Search(int key)
        {
            if (rootOffset < 0) return -1;
            long current = rootOffset;
            while (true)
            {
                Node node = ReadNode(current);
                if (node.IsLeaf)
                {
                    for (int i = 0; i < node.NumKeys; i++)
                        if (node.Keys[i] == key)
                            return node.Children[i];
                    return -1;
                }
                int idx = 0;
                while (idx < node.NumKeys && key >= node.Keys[idx]) idx++;
                current = node.Children[idx];
            }
        }

        /// <summary>
        /// Insert key->dataOffset
        /// </summary>
        public void Insert(int key, long dataOffset)
        {
            // Reject duplicate keys
            if (Search(key) >= 0)
                throw new InvalidOperationException($"Chave {key} já existe no índice.");


            if (rootOffset < 0)
            {
                Node leaf = new Node { IsLeaf = true, NumKeys = 1 };
                leaf.Keys[0] = key;
                leaf.Children[0] = dataOffset;
                leaf.NextLeaf = -1;
                rootOffset = AllocateNode();
                WriteNode(rootOffset, leaf);
                UpdateHeader();
                return;
            }
            var split = InsertRecursive(rootOffset, key, dataOffset);
            if (split != null)
            {
                Node newRoot = new Node { IsLeaf = false, NumKeys = 1 };
                newRoot.Keys[0] = split.PromotedKey;
                newRoot.Children[0] = rootOffset;
                newRoot.Children[1] = split.NewNodeOffset;
                long newRootOff = AllocateNode();
                WriteNode(newRootOff, newRoot);
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
        /// Delete a key from the index, merging underflowed leaf into sibling.
        /// </summary>
        public bool Delete(int key)
        {
            if (rootOffset < 0) return false;

            // stack for path
            var path = new List<(long offset, Node node, int idx)>();
            long current = rootOffset;
            Node node;
            int childIdx;
            // traverse to leaf
            while (true)
            {
                node = ReadNode(current);
                if (node.IsLeaf)
                {
                    path.Add((current, node, -1));
                    break;
                }
                childIdx = 0;
                while (childIdx < node.NumKeys && key >= node.Keys[childIdx]) childIdx++;
                path.Add((current, node, childIdx));
                current = node.Children[childIdx];
            }
            // remove key in leaf
            var (leafOff, leafNode, _) = path[path.Count - 1];
            int i;
            for (i = 0; i < leafNode.NumKeys; i++) if (leafNode.Keys[i] == key) break;
            if (i == leafNode.NumKeys) return false; // not found
            for (int j = i; j < leafNode.NumKeys - 1; j++)
            {
                leafNode.Keys[j] = leafNode.Keys[j + 1];
                leafNode.Children[j] = leafNode.Children[j + 1];
            }
            leafNode.NumKeys--;
            WriteNode(leafOff, leafNode);
            // underflow?
            if (leafNode.NumKeys >= MinKeys || path.Count == 1)
            {
                // done or root leaf
                return true;
            }
            // merge leaf with sibling
            var (parentOff, parentNode, idxInParent) = path[path.Count - 2];
            // prefer left sibling
            long siblingOff;
            Node sibling;
            bool isLeft = false;
            if (idxInParent > 0)
            {
                // left sibling
                siblingOff = parentNode.Children[idxInParent - 1];
                sibling = ReadNode(siblingOff);
                isLeft = true;
            }
            else
            {
                // right sibling
                siblingOff = parentNode.Children[idxInParent + 1];
                sibling = ReadNode(siblingOff);
            }
            // merge leaf into sibling
            Node dest = isLeft ? sibling : leafNode;
            Node src = isLeft ? leafNode : sibling;
            int destOldKeys = dest.NumKeys;
            // append src keys
            for (int k = 0; k < src.NumKeys; k++)
            {
                dest.Keys[destOldKeys + k] = src.Keys[k];
                dest.Children[destOldKeys + k] = src.Children[k];
            }
            dest.NumKeys += src.NumKeys;
            dest.NextLeaf = isLeft ? sibling.NextLeaf : leafNode.NextLeaf;
            // write dest
            long destOff = isLeft ? siblingOff : leafOff;
            WriteNode(destOff, dest);
            // remove pointer and key from parent
            int removeIdx = isLeft ? idxInParent : idxInParent + 1;
            for (int k = removeIdx - (isLeft ? 0 : 1); k < parentNode.NumKeys - 1; k++)
            {
                parentNode.Keys[k] = parentNode.Keys[k + 1];
                parentNode.Children[k + 1] = parentNode.Children[k + 2];
            }
            parentNode.NumKeys--;
            WriteNode(parentOff, parentNode);
            // adjust root if empty
            if (parentOff == rootOffset && parentNode.NumKeys == 0)
            {
                // new root is first child
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
