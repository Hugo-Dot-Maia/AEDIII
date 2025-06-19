namespace AEDIII.Interfaces
{
    /// <summary>
    /// Interface para casamento de padrões em texto.
    /// </summary>
    public interface IPatternMatcher
    {
        /// <summary>
        /// Verifica se o padrão aparece em algum lugar do texto.
        /// </summary>
        /// <param name="text">Texto onde buscar.</param>
        /// <param name="pattern">Padrão a ser buscado.</param>
        /// <returns>True se encontrar o padrão dentro do texto.</returns>
        bool Matches(string text, string pattern);
    }
}
