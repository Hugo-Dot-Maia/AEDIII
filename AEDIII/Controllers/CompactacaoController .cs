using AEDIII.Compactacao;
using Microsoft.AspNetCore.Mvc;

namespace AEDIII.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class CompactacaoController : ControllerBase
    {
        private readonly CompactacaoService _svc;
        public CompactacaoController(CompactacaoService svc) => _svc = svc;

        [HttpPost("compress")]
        public IActionResult Compress([FromQuery] int version)
        {
            string dbPath = Path.Combine(Environment.CurrentDirectory, "dados", "Pais", "Pais.db");
            _svc.RunCompression(dbPath, version); 
            return Ok($"Compressão versão {version} concluída.");
        }

        [HttpPost("decompress")]
        public IActionResult Decompress([FromQuery] int version)
        {
            string dbPath = Path.Combine(Environment.CurrentDirectory, "dados", "Pais", "Pais.db");
            _svc.RunDecompression(dbPath, version);
            return Ok($"Descompressão versão {version} concluída.");
        }
    }
}
