using ScanApi.Models;
using ScanApi.Services;

namespace Sims.Worker.Tests;

public class SbomParserTests
{
    private readonly CycloneDxParser parser = new();

    [Fact]
    public void ReportsNullComponentsAndFieldLimitsBeforeSaving()
    {
        var bom = new CycloneDxBom("CycloneDX", "1.5", [
            null,
            new(new string('x', 201), new string('1', 101), "pkg:" + new string('x', 497))
        ]);

        var (components, errors) = parser.Parse(bom);
        Assert.Null(components);
        Assert.Equal(4, errors.Count);
    }

    [Fact]
    public void TrimsFieldsBeforeCheckingLimitsAndDuplicates()
    {
        var item = new CycloneDxComponent(" " + new string('x', 200), new string('1', 100), "pkg:" + new string('x', 496));
        var bom = new CycloneDxBom("CycloneDX", "1.5", [item, item with { Purl = item.Purl!.ToUpperInvariant() + " " }]);

        var (components, errors) = parser.Parse(bom);
        Assert.Empty(errors);
        var component = Assert.Single(components!);
        Assert.Equal(item.Name!.Trim(), component.Name);
        Assert.Equal(item.Purl, component.Purl);
    }
}
