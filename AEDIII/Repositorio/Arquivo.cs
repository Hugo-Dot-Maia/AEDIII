using AEDIII.Interfaces;
using System;
using System.IO;
using System.Reflection;


namespace AEDIII.Repositorio
{
    public class Arquivo<T> where T : IRegistro, new()
    {
        private const int TAM_CABECALHO = 12;
        private FileStream arquivo;
        private string nomeArquivo;
        private BinaryReader leitor;
        private BinaryWriter escritor;

        public Arquivo(string nomeArquivo)
        {
            Directory.CreateDirectory("./dados");
            Directory.CreateDirectory($"./dados/{nomeArquivo}");
            this.nomeArquivo = $"./dados/{nomeArquivo}/{nomeArquivo}.db";

            arquivo = new FileStream(this.nomeArquivo, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            leitor = new BinaryReader(arquivo);
            escritor = new BinaryWriter(arquivo);

            if (arquivo.Length < TAM_CABECALHO)
            {
                escritor.Seek(0, SeekOrigin.Begin);
                escritor.Write(0); // Último ID usado
                escritor.Write(-1L); // Lista de registros excluídos
                escritor.Flush();
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

            long endereco = GetDeleted(dados.Length);
            if (endereco == -1)
            {
                arquivo.Seek(arquivo.Length, SeekOrigin.Begin);
                escritor.Write((byte)' ');
                escritor.Write((short)dados.Length);
                escritor.Write(dados);
            }
            else
            {
                arquivo.Seek(endereco, SeekOrigin.Begin);
                escritor.Write((byte)' ');
                arquivo.Seek(2, SeekOrigin.Current);
                escritor.Write(dados);
            }
            escritor.Flush();
            return obj.GetId();
        }

        public T Read(int id)
        {
            arquivo.Seek(TAM_CABECALHO, SeekOrigin.Begin);
            while (arquivo.Position < arquivo.Length)
            {
                long posicao = arquivo.Position;
                byte lapide = leitor.ReadByte();
                short tamanho = leitor.ReadInt16();
                byte[] dados = leitor.ReadBytes(tamanho);

                if (lapide == ' ')
                {
                    T obj = new T();
                    obj.FromByteArray(dados);
                    if (obj.GetId() == id)
                        return obj;
                }
            }
            return default;
        }

        public bool Delete(int id)
        {
            arquivo.Seek(TAM_CABECALHO, SeekOrigin.Begin);
            while (arquivo.Position < arquivo.Length)
            {
                long posicao = arquivo.Position;
                byte lapide = leitor.ReadByte();
                short tamanho = leitor.ReadInt16();
                byte[] dados = leitor.ReadBytes(tamanho);

                if (lapide == ' ')
                {
                    T obj = new T();
                    obj.FromByteArray(dados);
                    if (obj.GetId() == id)
                    {
                        arquivo.Seek(posicao, SeekOrigin.Begin);
                        escritor.Write((byte)'*');
                        AddDeleted(tamanho, posicao);
                        escritor.Flush();
                        return true;
                    }
                }
            }
            return false;
        }

        public bool Update(T novoObj)
        {
            arquivo.Seek(TAM_CABECALHO, SeekOrigin.Begin);
            while (arquivo.Position < arquivo.Length)
            {
                long posicao = arquivo.Position;
                byte lapide = leitor.ReadByte();
                short tamanho = leitor.ReadInt16();
                byte[] dados = leitor.ReadBytes(tamanho);

                if (lapide == ' ')
                {
                    T obj = new T();
                    obj.FromByteArray(dados);
                    if (obj.GetId() == novoObj.GetId())
                    {
                        byte[] novosDados = novoObj.ToByteArray();
                        if (novosDados.Length <= tamanho)
                        {
                            arquivo.Seek(posicao + 3, SeekOrigin.Begin);
                            escritor.Write(novosDados);
                        }
                        else
                        {
                            arquivo.Seek(posicao, SeekOrigin.Begin);
                            escritor.Write((byte)'*');
                            AddDeleted(tamanho, posicao);
                            long novoEndereco = GetDeleted(novosDados.Length);
                            if (novoEndereco == -1)
                            {
                                arquivo.Seek(arquivo.Length, SeekOrigin.Begin);
                                escritor.Write((byte)' ');
                                escritor.Write((short)novosDados.Length);
                                escritor.Write(novosDados);
                            }
                            else
                            {
                                arquivo.Seek(novoEndereco, SeekOrigin.Begin);
                                escritor.Write((byte)' ');
                                arquivo.Seek(2, SeekOrigin.Current);
                                escritor.Write(novosDados);
                            }
                        }
                        escritor.Flush();
                        return true;
                    }
                }
            }
            return false;
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
            leitor.Close();
            escritor.Close();
            arquivo.Close();
        }
    }

}
