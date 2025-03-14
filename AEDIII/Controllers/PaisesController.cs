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
                UltimaAtualizacao = criarPaisDto.UltimaAtualizacao,
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
    }
}
