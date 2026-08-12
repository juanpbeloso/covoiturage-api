using System.Globalization;
using System.Text;

namespace SubiteAPI.Helpers;

public static class TextNormalize
{
    /// <summary>Quita diacríticos: "Junín" → "junin".</summary>
    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool ContainsFolded(string? haystack, string? needle)
    {
        var h = Fold(haystack);
        var n = Fold(needle);
        if (n.Length == 0) return true;
        return h.Contains(n, StringComparison.Ordinal);
    }

    public static bool StartsWithFolded(string? value, string? prefix)
    {
        var v = Fold(value);
        var p = Fold(prefix);
        if (p.Length == 0) return true;
        return v.StartsWith(p, StringComparison.Ordinal);
    }
}
