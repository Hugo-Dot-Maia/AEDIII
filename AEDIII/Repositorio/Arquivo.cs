using AEDIII.Indexes;
using AEDIII.Interfaces;
using System;
using System.IO;

namespace AEDIII.Repositorio
{
    public class Arquivo<T> where T : IRegistro, new()
    {
        private const int TAM_CABECALHO = 12;
        private readonly FileStream arquivo;
        private readonly BinaryReader leitor;
        private readonly BinaryWriter escritor;
        private readonly BPlusTreeIndex _index;

        private readonly string nomeArquivo;

        public Arquivo(string nomeArquivo)
        {
            // Paths
            Directory.CreateDirectory("./dados");
            Directory.CreateDirectory($"./dados/{nomeArquivo}");
            this.nomeArquivo = $"./dados/{nomeArquivo}/{nomeArquivo}.db";

            // Open data file
            arquivo = new FileStream(this.nomeArquivo, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            leitor = new BinaryReader(arquivo);
            escritor = new BinaryWriter(arquivo);

            // Initialize header if first time
            if (arquivo.Length < TAM_CABECALHO)
            {
                escritor.Seek(0, SeekOrigin.Begin);
                escritor.Write(0);       // último ID usado
                escritor.Write(-1L);     // lista de áreas deletadas
                escritor.Flush();
            }

            // Initialize B+ index (creates .idx file)
            _index = new BPlusTreeIndex(nomeArquivo);

            // Rebuild index from data file if empty
            if (_index.Search(0) < 0 && arquivo.Length > TAM_CABECALHO)
                RebuildIndex();
        }

        // Rebuilds index by scanning all records
        private void RebuildIndex()
        {
            arquivo.Seek(TAM_CABECALHO, SeekOrigin.Begin);
            while (arquivo.Position < arquivo.Length)
            {
                long pos = arquivo.Position;
                byte lapide = leitor.ReadByte();
                short tamanho = leitor.ReadInt16();
                byte[] dados = leitor.ReadBytes(tamanho);

                if (lapide == (byte)' ')
                {
                    var obj = new T();
                    obj.FromByteArray(dados);
                    _index.Insert(obj.GetId(), pos);
                }
            }
        }

        public int Create(T obj)
        {
            arquivo.Seek(0, SeekOrigin.Begin);
            int novoID = leitor.ReadInt32() + 1;
            arquivo.Seek(0, SeekOrigin.Begin);
            escritor.Write(novoID);
            obj.SetId(novoID);

            byte[] dados = obj.ToByteArray();
            long pos;

            long endereco = GetDeleted(dados.Length);
            if (endereco < 0)
            {
                arquivo.Seek(arquivo.Length, SeekOrigin.Begin);
                pos = arquivo.Position;
                escritor.Write((byte)' ');
                escritor.Write((short)dados.Length);
                escritor.Write(dados);
            }
            else
            {
                arquivo.Seek(endereco, SeekOrigin.Begin);
                pos = endereco;
                escritor.Write((byte)' ');
                arquivo.Seek(2, SeekOrigin.Current);
                escritor.Write(dados);
            }

            escritor.Flush();
            _index.Insert(obj.GetId(), pos);
            return obj.GetId();
        }

        public T Read(int id)
        {
            long pos = _index.Search(id);
            if (pos < 0)
                return default;

            arquivo.Seek(pos, SeekOrigin.Begin);
            byte lapide = leitor.ReadByte();
            short tamanho = leitor.ReadInt16();
            byte[] dados = leitor.ReadBytes(tamanho);
            if (lapide != (byte)' ')
                return default;

            var obj = new T();
            obj.FromByteArray(dados);
            return obj;
        }

        public bool Delete(int id)
        {
            long pos = _index.Search(id);
            if (pos < 0)
                return false;

            arquivo.Seek(pos, SeekOrigin.Begin);
            escritor.Write((byte)'*');

            // read tamanho to add to deleted list
            arquivo.Seek(pos + 1, SeekOrigin.Begin);
            short tamanho = leitor.ReadInt16();
            AddDeleted(tamanho, pos);

            escritor.Flush();
            _index.Delete(id);
            return true;
        }

        public bool Update(T novoObj)
        {
            long pos = _index.Search(novoObj.GetId());
            if (pos < 0)
                return false;

            arquivo.Seek(pos, SeekOrigin.Begin);
            byte lapide = leitor.ReadByte();
            short tamanho = leitor.ReadInt16();
            byte[] novosDados = novoObj.ToByteArray();

            if (novosDados.Length <= tamanho)
            {
                arquivo.Seek(pos + 3, SeekOrigin.Begin);
                escritor.Write(novosDados);
                escritor.Flush();
                return true;
            }

            // Tombstone old
            arquivo.Seek(pos, SeekOrigin.Begin);
            escritor.Write((byte)'*');
            AddDeleted(tamanho, pos);

            // Write new record
            long novoPos;
            long endereco = GetDeleted(novosDados.Length);
            if (endereco < 0)
            {
                arquivo.Seek(arquivo.Length, SeekOrigin.Begin);
                novoPos = arquivo.Position;
                escritor.Write((byte)' ');
                escritor.Write((short)novosDados.Length);
                escritor.Write(novosDados);
            }
            else
            {
                arquivo.Seek(endereco, SeekOrigin.Begin);
                novoPos = endereco;
                escritor.Write((byte)' ');
                arquivo.Seek(2, SeekOrigin.Current);
                escritor.Write(novosDados);
            }

            escritor.Flush();
            _index.Delete(novoObj.GetId());
            _index.Insert(novoObj.GetId(), novoPos);
            return true;
        }

        private void AddDeleted(int tamanhoEspaco, long enderecoEspaco)
        {
            arquivo.Seek(4, SeekOrigin.Begin);
            long endereco = leitor.ReadInt64();
            if (endereco == -1)
            {
                arquivo.Seek(4, SeekOrigin.Begin);
                escritor.Write(enderecoEspaco);
                arquivo.Seek(enderecoEspaco + 3, SeekOrigin.Begin);
                escritor.Write(-1L);
            }
            else
            {
                while (endereco != -1)
                {
                    arquivo.Seek(endereco + 1, SeekOrigin.Begin);
                    int tamanho = leitor.ReadInt16();
                    long proximo = leitor.ReadInt64();
                    if (tamanho > tamanhoEspaco)
                    {
                        arquivo.Seek(4, SeekOrigin.Begin);
                        escritor.Write(enderecoEspaco);
                        arquivo.Seek(enderecoEspaco + 3, SeekOrigin.Begin);
                        escritor.Write(endereco);
                        break;
                    }
                    endereco = proximo;
                }
            }
            escritor.Flush();
        }

        private long GetDeleted(int tamanhoNecessario)
        {
            arquivo.Seek(4, SeekOrigin.Begin);
            long endereco = leitor.ReadInt64();
            while (endereco != -1)
            {
                arquivo.Seek(endereco + 1, SeekOrigin.Begin);
                int tamanho = leitor.ReadInt16();
                long proximo = leitor.ReadInt64();
                if (tamanho > tamanhoNecessario)
                {
                    arquivo.Seek(4, SeekOrigin.Begin);
                    escritor.Write(proximo);
                    return endereco;
                }
                endereco = proximo;
            }
            return -1;
        }

        public void Close()
        {
            _index.Close();
            leitor.Close();
            escritor.Close();
            arquivo.Close();
        }
    }
}
