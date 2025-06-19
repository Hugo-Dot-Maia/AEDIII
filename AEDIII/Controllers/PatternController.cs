using AEDIII.Entidades;
using AEDIII.Interfaces;
using AEDIII.Service;  
using Microsoft.AspNetCore.Mvc;

namespace AEDIII.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PatternController : ControllerBase
    {
        private readonly IPaisService _paisService;
        private readonly IPatternMatcher _matcher;

        public PatternController(IPaisService paisService, IPatternMatcher matcher)
        {
            _paisService = paisService;
            _matcher = matcher;
        }

        /// <summary>
        /// Busca todos os países cujo Nome contém o padrão informado, usando KMP.
        /// </summary>
        /// <param name="pattern">Substring a ser buscada em Nome (case-sensitive).</param>
        [HttpGet("search-nome")]
        public IActionResult SearchByNome([FromQuery] string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return BadRequest("O parâmetro 'pattern' é obrigatório.");

            var resultado = _paisService
                .SearchByNamePattern(pattern)
                .ToList();

            if (!resultado.Any())
                return NotFound($"Nenhum país encontrado contendo '{pattern}' no nome.");

            return Ok(resultado);
        }
    }
}
