using System.Text.RegularExpressions;

namespace Worker.Enrichment;

internal static partial class CveIdentifier
{
    // 50 Zeichen entsprechen dem vorgesehenen CVE-Feld in B1.
    internal static bool IsValid(string? value) =>
        value is { Length: <= 50 } && Pattern().IsMatch(value);

    [GeneratedRegex(@"\ACVE-[0-9]{4}-[0-9]{4,}\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
