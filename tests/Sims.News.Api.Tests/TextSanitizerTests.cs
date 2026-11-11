using NewsApi.Services;

namespace Sims.News.Api.Tests;

public class TextSanitizerTests
{
    [Theory]
    [InlineData("https://cert.at/x", true)]
    [InlineData("http://example.org", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,<script>", false)]
    [InlineData("/relativer/pfad", false)]
    [InlineData("", false)]
    public void IsSafeLink_erlaubt_nur_http_und_https(string link, bool erwartet)
    {
        Assert.Equal(erwartet, TextSanitizer.IsSafeLink(link));
    }

    [Fact]
    public void Clean_entfernt_Tags_dekodiert_und_faltet_Leerraum()
    {
        Assert.Equal("Hallo & Welt", TextSanitizer.Clean("<p>Hallo   &amp;\n\n<b>Welt</b></p>"));
    }

    [Fact]
    public void Clean_kuerzt_lange_Texte()
    {
        string result = TextSanitizer.Clean(new string('a', 300), 200);
        Assert.Equal(200, result.Length);
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void Clean_gibt_bei_null_leeren_Text_zurueck()
    {
        Assert.Equal(string.Empty, TextSanitizer.Clean(null));
    }
}
