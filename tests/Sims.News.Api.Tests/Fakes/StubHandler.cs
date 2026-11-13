using System.Net;
using System.Text;

namespace Sims.News.Api.Tests.Fakes;

public class StubHandler : HttpMessageHandler
{
    private readonly string _content;

    public StubHandler(string content)
    {
        _content = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_content, Encoding.UTF8, "application/xml")
        });
    }
}
