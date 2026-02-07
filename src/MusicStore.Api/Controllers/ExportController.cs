using Microsoft.AspNetCore.Mvc;
using MusicStore.Application;
using MusicStore.Application.Abstractions;

namespace MusicStore.Api.Controllers;

[ApiController]
[Route("api/export")]
public sealed class ExportController : ControllerBase
{
    private readonly ISongCatalogService _catalog;

    public ExportController(ISongCatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    public IActionResult ExportPage(
        [FromQuery] string locale = "en-US",
        [FromQuery] ulong seed = 1,
        [FromQuery] double likesAverage = 3.7,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new SongQuery(locale, seed, likesAverage, page, pageSize);
        var zip = _catalog.ExportMp3Zip(query, page, pageSize);
        var fileName = $"music-export-{locale}-seed-{seed}-page-{page}.zip";

        return File(zip, "application/zip", fileName);
    }
}
