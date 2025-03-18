using AEDIII.Interfaces;

namespace AEDIII.Entidades
{
    public class Pais : IRegistro
    {
        public int Id { get; set; }
        public int Rank { get; set; }
        public string Nome { get; set; }
        public long Populacao { get; set; }
        public  float Densidade { get; set; }
        public float Tamanho { get; set; }
        public DateTime UltimaAtualizacao { get; set; }
        public List<string> CidadesPopulosas { get; set; } = new List<string>();

        public void SetId(int id) => Id = id;
        public int GetId() => Id;

        public byte[] ToByteArray()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(Id);
            bw.Write(Rank);
            bw.Write(Nome);
            bw.Write(Populacao);
            bw.Write(Densidade);
            bw.Write(Tamanho);
            bw.Write(UltimaAtualizacao.ToBinary());

            bw.Write(CidadesPopulosas.Count);
            foreach (var cidade in CidadesPopulosas)
                bw.Write(cidade);

            return ms.ToArray();
        }

        public void FromByteArray(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            Id = br.ReadInt32();
            Rank = br.ReadInt32();
            Nome = br.ReadString();
            Populacao = br.ReadInt64();
            Densidade = br.ReadSingle();
            Tamanho = br.ReadSingle();
            UltimaAtualizacao = DateTime.FromBinary(br.ReadInt64());

            int qtdCidades = br.ReadInt32();
            CidadesPopulosas.Clear();
            for (int i = 0; i < qtdCidades; i++)
                CidadesPopulosas.Add(br.ReadString());
        }


    }
}
