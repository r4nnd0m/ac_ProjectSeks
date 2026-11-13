using Microsoft.AspNetCore.Mvc;
using NewsApi.Dtos;
using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("news")]
public class NewsController : ControllerBase
{
    private readonly NewsService _newsService;

    public NewsController(NewsService newsService)
    {
        _newsService = newsService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var items = await _newsService.GetAsync(ct);
        var response = items.Select(n => new NewsItemResponse(n.Title, n.Link, n.Source, n.PublishedAt));
        return Ok(response);
    }
}