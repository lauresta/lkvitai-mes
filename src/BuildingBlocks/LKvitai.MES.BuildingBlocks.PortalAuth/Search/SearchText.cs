using System.Globalization;
using System.Text;

namespace LKvitai.MES.BuildingBlocks.Search;

/// <summary>
/// Diacritics- and case-insensitive text matching for the universal search, so "tiekeju" finds
/// "Tiekėjų". Used for in-memory matching (menu labels); database searches keep their collation.
/// </summary>
public static class SearchText
{
    /// <summary>Lower-cases and strips combining marks: "Tiekėjų Susiejimai" → "tiekeju susiejimai".</summary>
    public static string Fold(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>True when <paramref name="text"/> contains <paramref name="query"/>, ignoring case and diacritics.</summary>
    public static bool Contains(string? text, string? query)
    {
        var needle = Fold(query).Trim();
        return needle.Length > 0 && Fold(text).Contains(needle, StringComparison.Ordinal);
    }

    /// <summary>
    /// Relevance of <paramref name="query"/> in <paramref name="text"/>: 1 exact, 0.9 prefix,
    /// 0.75 start of a later word, 0.5 anywhere, 0 no match. Case and diacritics are ignored.
    /// </summary>
    public static double Score(string? text, string? query)
    {
        var needle = Fold(query).Trim();
        if (needle.Length == 0) return 0;

        var haystack = Fold(text).Trim();
        var index = haystack.IndexOf(needle, StringComparison.Ordinal);
        if (index < 0) return 0;
        if (haystack.Length == needle.Length) return 1;
        if (index == 0) return 0.9;

        while (index >= 0)
        {
            if (!char.IsLetterOrDigit(haystack[index - 1])) return 0.75;
            index = haystack.IndexOf(needle, index + 1, StringComparison.Ordinal);
        }

        return 0.5;
    }
}
