using System.Globalization;
using System.Text;

namespace STSAnaliza.Services;

/// <summary>
/// Wspólna normalizacja nazw zawodników (STS / Sportradar).
/// Jedno miejsce = brak duplikacji + spójne dopasowania.
/// </summary>
internal static class SportradarName
{
    /// <summary>
    /// Normalizacja do klucza słownikowego:
    /// - trim
    /// - usuń diakrytyki
    /// - uppercase invariant
    /// - zostaw tylko litery/cyfry i pojedyncze spacje (resztę traktuj jako separator)
    /// </summary>
    public static string NormalizeKey(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";

        s = s.Trim();

        // usuń diakrytyki
        var formD = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        var clean = sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();

        // litery/cyfry -> zostaw; reszta -> spacja; kompresuj spacje
        var outSb = new StringBuilder(clean.Length);
        var prevSpace = true;

        foreach (var ch in clean)
        {
            if (char.IsLetterOrDigit(ch))
            {
                outSb.Append(ch);
                prevSpace = false;
            }
            else
            {
                if (!prevSpace)
                {
                    outSb.Append(' ');
                    prevSpace = true;
                }
            }
        }

        return outSb.ToString().Trim();
    }

    /// <summary>
    /// Tokenizacja (dla dopasowań typu subset):
    /// - używa NormalizeKey()
    /// - dzieli po spacji
    /// - usuwa krótkie tokeny (domyślnie <=1)
    /// </summary>
    public static HashSet<string> Tokens(string? s, int minTokenLen = 2)
    {
        var key = NormalizeKey(s);
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(key))
            return tokens;

        foreach (var t in key.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (t.Length >= minTokenLen)
                tokens.Add(t);
        }

        return tokens;
    }
}