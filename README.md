# Projeto AEDIII
Este projeto é uma API desenvolvida com ASP.NET Core que gerencia registros de países. O sistema utiliza um repositório baseado em arquivo binário (Pais.db) para armazenar os dados, além de suportar a importação de registros a partir de um arquivo CSV.

## Funcionalidades
* CRUD de Países:
  * Criar país (POST)
  * Obter país por ID (GET)
  * Atualizar país (PUT)
  * Deletar país (DELETE)
* Índice B+:
  * Índice no arquivo .idx paralelo ao .db para buscas O(logₘ N).
  * Tratamento de splits, merges e underflow em folhas.
  * Rejeita chaves duplicadas (ID único).
    
* Importação de Dados:
  * Importar os primeiros 50 registros do arquivo CSV (WolrdPopulationData.csv) para o repositório.
    
* Consulta Múltipla:
  * Obter uma lista de países a partir de uma lista de IDs via query string.

* Gestão de Cidades Populosas:
  * Cada país possui uma lista de cidades populosas e uma propriedade calculada que retorna a quantidade de cidades cadastradas.

* Controle de Última Atualização:
  * O campo UltimaAtualizacao é atualizado automaticamente sempre que um país é criado ou atualizado.

## Tecnologias Utilizadas
* .NET 6 / ASP.NET Core: Framework para desenvolvimento da API.
* C#: Linguagem de programação utilizada.
* FileStream & Binary Serialization: Repositório customizado baseado em arquivos binários.
* DTOs: Utilizado para transferir dados entre a API e os clientes.

## Estrutura do Projeto
 * AEDIII.Entidades:
    * Contém as classes de domínio, como Pais, que implementa a interface IRegistro para serialização/deserialização dos registros.

* AEDIII.Repositorio:
  * Implementa o repositório Arquivo<T> para manipulação dos dados em arquivo binário (Pais.db).

* AEDIII.Servicos:
  * Contém a lógica de negócio e operações para manipulação dos países, delegando as operações ao repositório.

* AEDIII.Controllers:
  * API Controllers que expõem os endpoints para criação, leitura, atualização, deleção e importação dos registros.

* AEDIII.DTOs:
  * Define os Data Transfer Objects para as operações de criação e atualização (por exemplo, CriarPaisDto e AtualizarPaisDto).

## Endpoints Disponíveis
* POST /paises
Cria um novo país.
Requisição: JSON com os campos: Rank, Nome, Populacao, Densidade, Tamanho e CidadesPopulosas (lista de strings).
Resposta: Retorna o país criado e o ID gerado automaticamente.

* GET /paises/{id}
Retorna os dados do país correspondente ao ID informado.

* PUT /paises/{id}
Atualiza os dados do país (exceto o ID, que é gerado automaticamente) e atualiza o campo UltimaAtualizacao.

* DELETE /paises/{id}
Remove o país do repositório.

* GET /paises/lista?ids=1,2,3
Retorna uma lista de países cujos IDs foram informados na query string.

* POST /paises/import
Lê o arquivo CSV localizado em dados/FonteDeDados/WolrdPopulationData.csv e importa os 50 primeiros registros para o repositório.

## Como Executar o Projeto
1. Pré-requisitos:

- .NET 6 SDK
- Editor/IDE (Visual Studio, VS Code, etc.)
2. Clonar o Repositório:

bash
Copiar
git clone https://github.com/seu-usuario/AEDIII.git
cd AEDIII

3. Executar a Aplicação:

No terminal, dentro do diretório do projeto:

bash
Copiar
dotnet run

## Considerações Importantes
* Repositório Baseado em Arquivo:
* O projeto utiliza um repositório customizado que grava os registros em um arquivo binário (Pais.db). Caso o formato dos registros seja alterado (como a inclusão de cidades populosas), os arquivos antigos podem ficar incompatíveis. Em ambiente de desenvolvimento, exclua o arquivo antigo para regenerá-lo com o novo formato.
