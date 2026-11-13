using ScanApi.Models;
using Sims.Shared.Entities;

namespace ScanApi.Services;

public interface ISbomParser
{
    (List<Component>? Components, List<string> Errors) Parse(CycloneDxBom bom);
}

public class CycloneDxParser : ISbomParser
{
    public (List<Component>? Components, List<string> Errors) Parse(CycloneDxBom bom)
    {
        var errors = new List<string>();

        if (!string.Equals(bom.BomFormat, "CycloneDX", StringComparison.OrdinalIgnoreCase))
            errors.Add("BomFormat muss CycloneDX sein.");

        if (bom.Components is null || bom.Components.Count == 0)
        {
            errors.Add("Es muss mindestens eine Komponente im SBOM vorhanden sein.");
            return (null, errors);
        }

        if (bom.Components.Count > 5000)
        {
            errors.Add("Es sind höchstens 5000 Komponenten erlaubt.");
            return (null, errors);
        }

        var components = new List<Component>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in bom.Components)
        {
            var component = ParseComponent(item, errors);
            if (component is not null && seen.Add(component.Purl))
                components.Add(component);
        }

        return errors.Count == 0 ? (components, errors) : (null, errors);
    }

    private static Component? ParseComponent(CycloneDxComponent? item, List<string> errors)
    {
        if (item is null)
        {
            errors.Add("Ein Komponenteneintrag ist leer.");
            return null;
        }

        string name = item.Name?.Trim() ?? "";
        string version = item.Version?.Trim() ?? "";
        string purl = item.Purl?.Trim() ?? "";
        int previousErrorCount = errors.Count;

        if (name.Length == 0 || name.Length > Component.MaxNameLength)
            errors.Add($"Der Komponentenname muss 1 bis {Component.MaxNameLength} Zeichen haben.");
        if (version.Length == 0 || version.Length > Component.MaxVersionLength)
            errors.Add($"Die Paketversion muss 1 bis {Component.MaxVersionLength} Zeichen haben.");
        if (!purl.StartsWith("pkg:", StringComparison.OrdinalIgnoreCase) || purl.Length > Component.MaxPurlLength)
            errors.Add($"Die PURL muss mit 'pkg:' beginnen und darf höchstens {Component.MaxPurlLength} Zeichen haben.");

        if (errors.Count != previousErrorCount)
            return null;

        return new Component { Name = name, Version = version, Purl = purl };
    }
}
