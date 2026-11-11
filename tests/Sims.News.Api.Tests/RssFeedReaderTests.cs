using System.Xml;
using NewsApi.Services;
using Sims.News.Api.Tests.Fakes;

namespace Sims.News.Api.Tests;

public class RssFeedReaderTests
{
    private const string Rss = """
        <?xml version="1.0" encoding="utf-8"?>
        <rss version="2.0"><channel>
          <title>CERT Test</title><link>https://cert.test</link><description>x</description>
          <item><title>RSS-Meldung</title><link>https://cert.test/1</link><pubDate>Tue, 10 Nov 2026 08:00:00 GMT</pubDate></item>
          <item><title></title><link>https://cert.test/ohne-titel</link></item>
        </channel></rss>
        """;

    private const string Atom = """
        <?xml version="1.0" encoding="utf-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom">
          <title>Atom Test</title><id>urn:test</id><updated>2026-11-10T08:00:00Z</updated>
          <entry><title>Atom-Meldung</title><id>urn:1</id><link href="https://atom.test/1"/><updated>2026-11-10T09:00:00Z</updated></entry>
        </feed>
        """;

    private const string MitDoctype = """
        <?xml version="1.0"?>
        <!DOCTYPE rss [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
        <rss version="2.0"><channel><title>&xxe;</title></channel></rss>
        """;

    private static RssFeedReader Reader(string xml) => new(new HttpClient(new StubHandler(xml)));

    [Fact]
    public async Task Liest_RSS_und_laesst_Eintraege_ohne_Titel_weg()
    {
        var items = await Reader(Rss).ReadAsync("https://cert.test/rss", CancellationToken.None);

        Assert.Single(items);
        Assert.Equal("RSS-Meldung", items[0].Title);
        Assert.Equal("CERT Test", items[0].Source);
    }

    [Fact]
    public async Task Liest_Atom_und_nimmt_LastUpdated_als_Ersatzdatum()
    {
        var items = await Reader(Atom).ReadAsync("https://atom.test/feed", CancellationToken.None);

        Assert.Single(items);
        Assert.Equal("https://atom.test/1", items[0].Link);
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero), items[0].PublishedAT);
    }

    [Fact]
    public async Task Lehnt_Dokument_mit_DOCTYPE_ab()
    {
        await Assert.ThrowsAsync<XmlException>(() =>
            Reader(MitDoctype).ReadAsync("https://boese.test/rss", CancellationToken.None));
    }
}
