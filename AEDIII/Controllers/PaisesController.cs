using AEDIII.DTO;
using AEDIII.Entidades;
using AEDIII.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AEDIII.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PaisesController : ControllerBase
    {
        private readonly IPaisService _paisService;

        public PaisesController(IPaisService paisService)
        {
            _paisService = paisService;
        }

        [HttpPost]
        public IActionResult CriarPais([FromBody] CriarPaisDto criarPaisDto)
        {
            Pais pais = new()
            {
                Rank = criarPaisDto.Rank,
                Nome = criarPaisDto.Nome,
                Populacao = criarPaisDto.Populacao,
                Densidade = criarPaisDto.Densidade,
                Tamanho = criarPaisDto.Tamanho,
                UltimaAtualizacao = DateTime.UtcNow,
            };

            int id = _paisService.CriarPais(pais);
            return CreatedAtAction(nameof(ObterPais), new { id }, pais);
        }

        [HttpGet("{id}")]
        public IActionResult ObterPais(int id)
        {
            var pais = _paisService.ObterPais(id);
            if (pais == null)
                return NotFound();
            return Ok(pais);
        }

        [HttpDelete("{id}")]
        public IActionResult DeletarPais(int id)
        {
            bool deletado = _paisService.DeletarPais(id);
            if (!deletado)
                return NotFound();
            return NoContent();
        }

        [HttpPut("{id}")]
        public IActionResult AtualizarPais(int id, [FromBody] AtualizarPaisDto paisDto)
        {
            Pais paisExistente = _paisService.ObterPais(id);

            if (paisExistente == null)
                return NotFound();
            
            paisExistente.Nome = paisDto.Nome;
            paisExistente.Densidade = paisDto.Densidade;
            paisExistente.Populacao = paisDto.Populacao;
            paisExistente.Tamanho = paisDto.Tamanho;
            paisExistente.Rank = paisDto.Rank;
            paisExistente.UltimaAtualizacao = DateTime.UtcNow;

            bool atualizado = _paisService.AtualizarPais(paisExistente);
            if (atualizado)
                return NoContent();

            return BadRequest("Erro ao atualizar o país");
        }

        [HttpPost("import")]
        public IActionResult ImportarPaises()
        {
            bool resultado = _paisService.ImportarPaises();
            return NoContent();
        }
    }
}
