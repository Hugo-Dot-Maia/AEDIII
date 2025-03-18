namespace AEDIII.DTO
{
    public class AtualizarPaisDto
    {
        public int Rank { get; set; }
        public string Nome { get; set; }
        public long Populacao { get; set; }
        public float Densidade { get; set; }
        public float Tamanho { get; set; }
        public List<string> CidadesPopulosas { get; set; } = new List<string>();
    }
}
