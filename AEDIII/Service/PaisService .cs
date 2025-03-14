using AEDIII.Entidades;
using AEDIII.Interfaces;
using AEDIII.Repositorio;

namespace AEDIII.Service
{
    public class PaisService : IPaisService
    {
        private readonly Arquivo<Pais> _arquivo;

        public PaisService(Arquivo<Pais> arquivo)
        {
            _arquivo = arquivo;
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


    }
}
