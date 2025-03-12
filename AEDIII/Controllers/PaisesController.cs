using AEDIII.Entidades;
using AEDIII.Repositorio;
using Microsoft.AspNetCore.Mvc;

namespace AEDIII.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PaisesController : ControllerBase
    {
        private readonly Arquivo<Pais> _arquivo;

        public PaisesController(Arquivo<Pais> arquivo)
        {
            _arquivo = arquivo;
        }

        [HttpPost]
        public IActionResult CriarPais([FromBody] Pais pais)
        {
            int id = _arquivo.Create(pais);
            return CreatedAtAction(nameof(ObterPais), new { id }, pais);
        }

        [HttpGet("{id}")]
        public IActionResult ObterPais(int id)
        {
            var pais = _arquivo.Read(id);
            if (pais == null) return NotFound();
            return Ok(pais);
        }

        [HttpDelete("{id}")]
        public IActionResult DeletarPais(int id)
        {
            bool deletado = _arquivo.Delete(id);
            if (!deletado) return NotFound();
            return NoContent();
        }


    }
}
