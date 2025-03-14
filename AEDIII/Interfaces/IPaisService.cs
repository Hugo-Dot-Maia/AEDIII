using AEDIII.Entidades;

namespace AEDIII.Interfaces
{
    public interface IPaisService
    {
        int CriarPais(Pais pais);
        Pais ObterPais(int id);
        bool DeletarPais(int id);
    }
}
