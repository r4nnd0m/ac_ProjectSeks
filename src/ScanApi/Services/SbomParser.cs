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
            errors.Add("Es muss mindestens eine Komponente im SBOM vorhanden sein.");

        if (bom.Components is { Count: > 5000 })
            errors.Add("Es sind höchstens 5000 Komponenten erlaubt.");

        var validComponents = new List<Component>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (bom.Components is not null)
        {
            foreach (var item in bom.Components)
            {
                var valid = true;
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    errors.Add("Eine Komponente hat keinen Namen.");
                    valid = false;
                }
                if (string.IsNullOrWhiteSpace(item.Version))
                {
                    errors.Add("Eine Komponente hat keine Version.");
                    valid = false;
                }
                if (string.IsNullOrWhiteSpace(item.Purl) || !item.Purl.Trim().StartsWith("pkg:", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Komponente '{item.Name}' hat keine gültige PURL.");
                    valid = false;
                }

                if (!valid || !seen.Add(item.Purl!.Trim()))
                    continue;

                validComponents.Add(new Component
                {
                    Name = item.Name!.Trim(),
                    Version = item.Version!.Trim(),
                    Purl = item.Purl!.Trim()
                });
            }
        }

        return errors.Count == 0 ? (validComponents, errors) : (null, errors);
    }
}
