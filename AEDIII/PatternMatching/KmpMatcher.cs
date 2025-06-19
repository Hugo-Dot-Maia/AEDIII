using AEDIII.Interfaces;

namespace AEDIII.PatternMatching
{
    /// <summary>
    /// Implementação do algoritmo Knuth-Morris-Pratt (KMP) para casamento de padrões.
    /// Complexidade O(n + m), onde n = texto, m = padrão.
    /// </summary>
    public class KmpMatcher : IPatternMatcher
    {
        /// <summary>
        /// Verifica se 'pattern' ocorre dentro de 'text'.
        /// </summary>
        public bool Matches(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return true; // padrão vazio casa em qualquer posição
            if (string.IsNullOrEmpty(text))
                return false;

            int[] lps = BuildLps(pattern);
            int i = 0, j = 0;

            while (i < text.Length)
            {
                if (text[i] == pattern[j])
                {
                    i++; j++;
                    if (j == pattern.Length)
                        return true; // encontrou o padrão
                }
                else if (j > 0)
                {
                    j = lps[j - 1];
                }
                else
                {
                    i++;
                }
            }
            return false;
        }

        /// <summary>
        /// Constrói o array LPS (longest proper prefix which is also suffix)
        /// para uso no KMP.
        /// </summary>
        private int[] BuildLps(string pattern)
        {
            int m = pattern.Length;
            int[] lps = new int[m];
            int len = 0; // comprimento do lps anterior
            int i = 1;

            lps[0] = 0;
            while (i < m)
            {
                if (pattern[i] == pattern[len])
                {
                    len++;
                    lps[i] = len;
                    i++;
                }
                else if (len > 0)
                {
                    len = lps[len - 1];
                }
                else
                {
                    lps[i] = 0;
                    i++;
                }
            }
            return lps;
        }
    }
}
