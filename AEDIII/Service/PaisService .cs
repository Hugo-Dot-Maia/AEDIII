using AEDIII.Entidades;
using AEDIII.Interfaces;
using AEDIII.PatternMatching;
using AEDIII.Repositorio;
using Microsoft.Extensions.Hosting;

namespace AEDIII.Service
{
    public class PaisService : IPaisService
    {
        private readonly Arquivo<Pais> _arquivo;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IPatternMatcher _matcher;

        public PaisService(Arquivo<Pais> arquivo,
                   IHostEnvironment hostEnvironment,
                   IPatternMatcher matcher)
        {
            _arquivo = arquivo;
            _hostEnvironment = hostEnvironment;
            _matcher = matcher;
        }

        public int CriarPais(Pais pais)
        {
            return _arquivo.Create(pais);
        }

        public Pais ObterPais(int id)
        {
            return _arquivo.Read(id);
        }

        public bool DeletarPais(int id)
        {
            return _arquivo.Delete(id);
        }

        public bool AtualizarPais(Pais pais)
        {
            return _arquivo.Update(pais);
        }

        public bool ImportarPaises()
        {
            string csvFilePath = 
                Path.Combine(_hostEnvironment.ContentRootPath, "dados", "FonteDeDados", "filtered_population_data_excel_tab.csv");

            if (!System.IO.File.Exists(csvFilePath))
            {
                return false;
            }


            try
            {
                // Lê todas as linhas e ignora a primeira linha (cabeçalho)
                var linhas = System.IO.File.ReadLines(csvFilePath)
                                           .Skip(1)  // Pula o cabeçalho
                                           .Take(200); // Pega as primeiras 200 linhas

                foreach (var linha in linhas)
                {
                    var colunas = linha.Split('\t', StringSplitOptions.RemoveEmptyEntries);

                    // Conversão dos dados. 
                    int rank = int.TryParse(colunas[0], out int r) ? r : 0;
                    string nome = colunas[1];
                    long populacao = long.TryParse(colunas[2].Replace(",", ""), out long pop) ? pop : 0;
                    float tamanho = float.TryParse(colunas[3].Replace(",", ""), out float tam) ? tam : 0;
                    float densidade = float.TryParse(colunas[4].Replace(",", ""), out float dens) ? dens : 0;

                    // Cria o objeto Pais. O ID será gerado automaticamente pelo repositório.
                    var pais = new Pais
                    {
                        Rank = rank,
                        Nome = nome,
                        Populacao = populacao,
                        Densidade = densidade,
                        Tamanho = tamanho,
                        UltimaAtualizacao = DateTime.UtcNow
                    };

                    _arquivo.Create(pais);

                }
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }

        public List<Pais> ObterPaises(List<int> ids)
        {
            List<Pais> paises = new List<Pais>();

            foreach (var id in ids)
            {
                var pais = _arquivo.Read(id);
                if (pais != null)
                    paises.Add(pais);
            }
            return paises;
        }

        public IEnumerable<Pais> SearchByNamePattern(string pattern)
        {
            var todos = _arquivo.ReadAll();
            return todos.Where(p => _matcher.Matches(p.Nome, pattern));
        }

    }
}
