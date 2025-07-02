using AEDIII.Indexes;
using AEDIII.Interfaces;
using System;
using System.IO;

namespace AEDIII.Repositorio
{
    /// <summary>
    /// Classe responsável pelo CRUD de registros em arquivo binário (.db), 
    /// com suporte a índice B+ para operações de busca rápida.
    /// </summary>
    /// <typeparam name="T">Tipo de registro que implementa IRegistro.</typeparam>
    public class Arquivo<T> where T : IRegistro, new()
    {
        private const int TAM_CABECALHO = 12;
        private readonly FileStream arquivo;
        private readonly BinaryReader leitor;
        private readonly BinaryWriter escritor;
        private readonly BPlusTreeIndex _index;
        private readonly string nomeArquivo;
        private readonly ICryptoService _crypto;

        /// <summary>
        /// Construtor: inicializa diretórios, arquivo de dados e índice B+.
        /// Se for a primeira vez, escreve o cabeçalho com último ID = 0 e lista de deletados vazia.
        /// Reconstrói o índice se houver registros existentes.
        /// </summary>
        /// <param name="nomeArquivo">Nome base para os arquivos .db e .idx.</param>
        public Arquivo(string nomeArquivo, ICryptoService cryptoService)
        {
            _crypto = cryptoService;

            Directory.CreateDirectory("./dados");
            Directory.CreateDirectory($"./dados/{nomeArquivo}");
            var path = $"./dados/{nomeArquivo}/{nomeArquivo}.db";

            arquivo = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            leitor = new BinaryReader(arquivo);
            escritor = new BinaryWriter(arquivo);

            if (arquivo.Length < TAM_CABECALHO)
            {
                escritor.Seek(0, SeekOrigin.Begin);
                escritor.Write(0);       // último ID = 0
                escritor.Write(-1L);     // freelist vazia
                escritor.Flush();
            }

            _index = new BPlusTreeIndex(nomeArquivo);
            if (_index.IsEmpty() && arquivo.Length > TAM_CABECALHO)
                RebuildIndex();
        }

        /// <summary>
        /// Reconstrói o índice B+ a partir de todos os registros válidos no arquivo de dados.
        /// Varre o arquivo a partir do primeiro registro e insere cada chave no índice.
        /// </summary>
        private void RebuildIndex()
        {
            arquivo.Seek(TAM_CABECALHO, SeekOrigin.Begin);
            while (arquivo.Position < arquivo.Length)
            {
                long pos = arquivo.Position;
                byte lapide = leitor.ReadByte();           // espaço ou tombstone
                short tamanho = leitor.ReadInt16();        // tamanho do registro
                //byte[] dados = leitor.ReadBytes(tamanho);
                var cipher = leitor.ReadBytes(tamanho);

                if (lapide == (byte)' ')
                {
                    var dados = _crypto.Decrypt(cipher);
                    var obj = new T();
                    obj.FromByteArray(dados);
                    _index.Insert(obj.GetId(), pos);
                }
            }
        }

        /// <summary>
        /// Cria um novo registro no arquivo de dados e insere a chave no índice.
        /// Reutiliza espaço livre se existir; caso contrário, anexa ao final.
        /// </summary>
        /// <param name="obj">Objeto a ser persistido.</param>
        /// <returns>ID gerado para o registro.</returns>
        public int Create(T obj)
        {
            // Atualiza último ID no cabeçalho
            arquivo.Seek(0, SeekOrigin.Begin);
            int novoID = leitor.ReadInt32() + 1;
            arquivo.Seek(0, SeekOrigin.Begin);
            escritor.Write(novoID);
            obj.SetId(novoID);

            // Serializa objeto em bytes
            //byte[] dados = obj.ToByteArray();
            var plain = obj.ToByteArray();
            var cipher = _crypto.Encrypt(plain);
            long pos;

            // Tenta reutilizar espaço disponível
            var free = GetDeleted(cipher.Length);
            if (free < 0)
            {
                arquivo.Seek(arquivo.Length, SeekOrigin.Begin);
                pos = arquivo.Position;
                escritor.Write((byte)' ');
                escritor.Write((short)cipher.Length);
                escritor.Write(cipher);
            }
            else
            {
                arquivo.Seek(free, SeekOrigin.Begin);
                pos = free;
                escritor.Write((byte)' ');
                arquivo.Seek(2, SeekOrigin.Current);
                escritor.Write(cipher);
            }

            escritor.Flush();
            // Insere chave->offset no índice
            _index.Insert(obj.GetId(), pos);
            return obj.GetId();
        }

        /// <summary>
        /// Lê um registro pelo ID usando o índice para localizar seu offset.
        /// Retorna default(T) se não encontrado ou tombstone.
        /// </summary>
        /// <param name="id">ID do registro a ser lido.</param>
        /// <returns>Objeto lido ou default.</returns>
        public T Read(int id)
        {
            var pos = _index.Search(id);
            if (pos < 0) return default;

            arquivo.Seek(pos, SeekOrigin.Begin);
            var lapide = leitor.ReadByte();
            var tamanho = leitor.ReadInt16();
            var cipher = leitor.ReadBytes(tamanho);
            if (lapide != (byte)' ') return default;

            var dados = _crypto.Decrypt(cipher);
            var obj = new T();
            obj.FromByteArray(dados);
            return obj;
        }

        /// <summary>
        /// Exclui um registro marcando tombstone, atualiza lista de deletados e remove do índice.
        /// </summary>
        /// <param name="id">ID do registro a ser excluído.</param>
        /// <returns>True se excluído, false se não encontrado.</returns>
        public bool Delete(int id)
        {
            var pos = _index.Search(id);
            if (pos < 0) return false;

            arquivo.Seek(pos, SeekOrigin.Begin);
            escritor.Write((byte)'*');
            arquivo.Seek(pos + 1, SeekOrigin.Begin);
            var tamanho = leitor.ReadInt16();
            AddDeleted(tamanho, pos);

            escritor.Flush();
            _index.Delete(id);
            return true;
        }

        /// <summary>
        /// Atualiza um registro existente. Se o novo tamanho for maior, grava tombstone e recria o registro,
        /// ajustando o índice; se menor ou igual, sobrescreve inline.
        /// </summary>
        /// <param name="novoObj">Objeto com mesmo ID para atualização.</param>
        /// <returns>True se atualizado, false se ID não encontrado.</returns>
        public bool Update(T novoObj)
        {
            var pos = _index.Search(novoObj.GetId());
            if (pos < 0) return false;

            // marca tombstone e adiciona ao freelist
            arquivo.Seek(pos, SeekOrigin.Begin);
            escritor.Write((byte)'*');
            arquivo.Seek(pos + 1, SeekOrigin.Begin);
            var oldSize = leitor.ReadInt16();
            AddDeleted(oldSize, pos);

            // serializa e criptografa o novo
            var plain = novoObj.ToByteArray();
            var cipher = _crypto.Encrypt(plain);

            long newPos;
            var free = GetDeleted(cipher.Length);
            if (free < 0)
            {
                arquivo.Seek(arquivo.Length, SeekOrigin.Begin);
                newPos = arquivo.Position;
                escritor.Write((byte)' ');
                escritor.Write((short)cipher.Length);
                escritor.Write(cipher);
            }
            else
            {
                arquivo.Seek(free, SeekOrigin.Begin);
                newPos = free;
                escritor.Write((byte)' ');
                arquivo.Seek(2, SeekOrigin.Current);
                escritor.Write(cipher);
            }

            escritor.Flush();
            _index.Delete(novoObj.GetId());
            _index.Insert(novoObj.GetId(), newPos);
            return true;
        }

        /// <summary>
        /// Adiciona espaço de registro excluído à lista de espaços livres (freelist).
        /// </summary>
        /// <param name="tamanhoEspaco">Tamanho em bytes do espaço liberado.</param>
        /// <param name="enderecoEspaco">Offset inicial desse espaço no arquivo.</param>
        private void AddDeleted(int tamanhoEspaco, long enderecoEspaco)
        {
            arquivo.Seek(4, SeekOrigin.Begin);
            var head = leitor.ReadInt64();

            if (head == -1)
            {
                arquivo.Seek(4, SeekOrigin.Begin);
                escritor.Write(enderecoEspaco);
                arquivo.Seek(enderecoEspaco + 3, SeekOrigin.Begin);
                escritor.Write(-1L);
            }
            else
            {
                long prev = head;
                while (prev != -1)
                {
                    arquivo.Seek(prev + 1, SeekOrigin.Begin);
                    var size = leitor.ReadInt16();
                    var next = leitor.ReadInt64();
                    if (size > tamanhoEspaco)
                    {
                        arquivo.Seek(4, SeekOrigin.Begin);
                        escritor.Write(enderecoEspaco);
                        arquivo.Seek(enderecoEspaco + 3, SeekOrigin.Begin);
                        escritor.Write(prev);
                        break;
                    }
                    prev = next;
                }
            }

            escritor.Flush();
        }

        /// <summary>
        /// Busca um espaço livre adequado ao tamanho necessário e o remove da freelist.
        /// </summary>
        /// <param name="tamanhoNecessario">Tamanho mínimo em bytes exigido.</param>
        /// <returns>Offset do espaço livre ou -1 se não houver.</returns>
        private long GetDeleted(int tamanhoNecessario)
        {
            arquivo.Seek(4, SeekOrigin.Begin);
            var head = leitor.ReadInt64();
            var prev = head;

            while (prev != -1)
            {
                arquivo.Seek(prev + 1, SeekOrigin.Begin);
                var size = leitor.ReadInt16();
                var next = leitor.ReadInt64();
                if (size > tamanhoNecessario)
                {
                    arquivo.Seek(4, SeekOrigin.Begin);
                    escritor.Write(next);
                    return prev;
                }
                prev = next;
            }

            return -1;
        }

        public IEnumerable<T> ReadAll()
        {
            var lista = new List<T>();
            arquivo.Seek(TAM_CABECALHO, SeekOrigin.Begin);

            while (arquivo.Position < arquivo.Length)
            {
                var lap = leitor.ReadByte();
                var tam = leitor.ReadInt16();
                var cipher = leitor.ReadBytes(tam);

                if (lap == (byte)' ')
                {
                    var dados = _crypto.Decrypt(cipher);
                    var obj = new T();
                    obj.FromByteArray(dados);
                    lista.Add(obj);
                }
            }

            return lista;
        }
        /// <summary>
        /// Fecha streams de dados e índice, liberando recursos.
        /// </summary>
        public void Close()
        {
            _index.Close();
            leitor.Close();
            escritor.Close();
            arquivo.Close();
        }
    }
}
