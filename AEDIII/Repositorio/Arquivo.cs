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

        /// <summary>
        /// Construtor: inicializa diretórios, arquivo de dados e índice B+.
        /// Se for a primeira vez, escreve o cabeçalho com último ID = 0 e lista de deletados vazia.
        /// Reconstrói o índice se houver registros existentes.
        /// </summary>
        /// <param name="nomeArquivo">Nome base para os arquivos .db e .idx.</param>
        public Arquivo(string nomeArquivo)
        {
            // Garante existência dos diretórios de dados
            Directory.CreateDirectory("./dados");
            Directory.CreateDirectory($"./dados/{nomeArquivo}");
            this.nomeArquivo = $"./dados/{nomeArquivo}/{nomeArquivo}.db";

            // Abre ou cria o arquivo de dados
            arquivo = new FileStream(
                this.nomeArquivo,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite
            );
            leitor = new BinaryReader(arquivo);
            escritor = new BinaryWriter(arquivo);

            // Se arquivo novo ou menor que o cabeçalho, inicializa header
            if (arquivo.Length < TAM_CABECALHO)
            {
                escritor.Seek(0, SeekOrigin.Begin);
                escritor.Write(0);       // último ID usado = 0
                escritor.Write(-1L);     // lista de deletados = vazio
                escritor.Flush();
            }

            // Inicializa ou carrega índice B+ (arquivo .idx paralelo)
            _index = new BPlusTreeIndex(nomeArquivo);

            // Reconstrói índice se estiver vazio mas houver registros no .db
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
                byte[] dados = leitor.ReadBytes(tamanho);

                if (lapide == (byte)' ')
                {
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
            byte[] dados = obj.ToByteArray();
            long pos;

            // Tenta reutilizar espaço disponível
            long endereco = GetDeleted(dados.Length);
            if (endereco < 0)
            {
                // Sem espaço livre: anexa ao final
                arquivo.Seek(arquivo.Length, SeekOrigin.Begin);
                pos = arquivo.Position;
                escritor.Write((byte)' ');
                escritor.Write((short)dados.Length);
                escritor.Write(dados);
            }
            else
            {
                // Reutiliza posição tombstone
                arquivo.Seek(endereco, SeekOrigin.Begin);
                pos = endereco;
                escritor.Write((byte)' ');
                arquivo.Seek(2, SeekOrigin.Current);
                escritor.Write(dados);
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

        /// <summary>
        /// Exclui um registro marcando tombstone, atualiza lista de deletados e remove do índice.
        /// </summary>
        /// <param name="id">ID do registro a ser excluído.</param>
        /// <returns>True se excluído, false se não encontrado.</returns>
        public bool Delete(int id)
        {
            long pos = _index.Search(id);
            if (pos < 0)
                return false;

            // Marca lápide no arquivo de dados
            arquivo.Seek(pos, SeekOrigin.Begin);
            escritor.Write((byte)'*');

            // Lê tamanho para adicionar à lista de deletados
            arquivo.Seek(pos + 1, SeekOrigin.Begin);
            short tamanho = leitor.ReadInt16();
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
            long pos = _index.Search(novoObj.GetId());
            if (pos < 0)
                return false;

            arquivo.Seek(pos, SeekOrigin.Begin);
            byte lapide = leitor.ReadByte();
            short tamanho = leitor.ReadInt16();
            byte[] novosDados = novoObj.ToByteArray();

            if (novosDados.Length <= tamanho)
            {
                // Sobrescreve no mesmo espaço
                arquivo.Seek(pos + 3, SeekOrigin.Begin);
                escritor.Write(novosDados);
                escritor.Flush();
                return true;
            }

            // Tombstone no antigo e adiciona ao freed list
            arquivo.Seek(pos, SeekOrigin.Begin);
            escritor.Write((byte)'*');
            AddDeleted(tamanho, pos);

            // Grava novo registro (reutiliza espaço ou no fim)
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
            // Atualiza índice: remove e insere no novo offset
            _index.Delete(novoObj.GetId());
            _index.Insert(novoObj.GetId(), novoPos);
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
            long endereco = leitor.ReadInt64();
            if (endereco == -1)
            {
                // Primeira entrada na freelist
                arquivo.Seek(4, SeekOrigin.Begin);
                escritor.Write(enderecoEspaco);
                arquivo.Seek(enderecoEspaco + 3, SeekOrigin.Begin);
                escritor.Write(-1L);
            }
            else
            {
                // Insere no início ou local adequado
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

        /// <summary>
        /// Busca um espaço livre adequado ao tamanho necessário e o remove da freelist.
        /// </summary>
        /// <param name="tamanhoNecessario">Tamanho mínimo em bytes exigido.</param>
        /// <returns>Offset do espaço livre ou -1 se não houver.</returns>
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
                    // Remove da freelist e retorna offset
                    arquivo.Seek(4, SeekOrigin.Begin);
                    escritor.Write(proximo);
                    return endereco;
                }
                endereco = proximo;
            }
            return -1;
        }

        public IEnumerable<T> ReadAll()
        {
            var lista = new List<T>();
            // posiciona após o cabeçalho
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
