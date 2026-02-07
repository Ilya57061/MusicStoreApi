using Microsoft.AspNetCore.Mvc;
using MusicStore.Application;
using MusicStore.Application.Abstractions;

namespace MusicStore.Api.Controllers;

[ApiController]
[Route("api/songs")]
public sealed class SongsController : ControllerBase
{
    private readonly ISongCatalogService _catalog;

    public SongsController(ISongCatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    public IActionResult GetSongs(
        [FromQuery] string locale = "en-US",
        [FromQuery] ulong seed = 1,
        [FromQuery] double likesAverage = 3.7,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new SongQuery(locale, seed, likesAverage, page, pageSize);
        var items = _catalog.GetSongs(query);

        return Ok(new
        {
            page,
            pageSize,
            items
        });
    }

    [HttpGet("{sequenceIndex:long}")]
    public IActionResult GetSongDetails(
        long sequenceIndex,
        [FromQuery] string locale = "en-US",
        [FromQuery] ulong seed = 1,
        [FromQuery] double likesAverage = 3.7,
        [FromQuery] int page = 1)
    {
        var query = new SongQuery(locale, seed, likesAverage, page, 1);
        var details = _catalog.GetSongDetails(query, sequenceIndex);

        return Ok(details);
    }

    [HttpGet("{sequenceIndex:long}/cover")]
    public IActionResult GetCover(
        long sequenceIndex,
        [FromQuery] string locale = "en-US",
        [FromQuery] ulong seed = 1,
        [FromQuery] int page = 1)
    {
        var query = new SongQuery(locale, seed, 0, page, 1);
        var png = _catalog.GetCoverPng(query, sequenceIndex);

        return File(png, "image/png");
    }

    [HttpGet("{sequenceIndex:long}/preview.mp3")]
    public IActionResult GetPreview(
        long sequenceIndex,
        [FromQuery] string locale = "en-US",
        [FromQuery] ulong seed = 1,
        [FromQuery] int page = 1)
    {
        var query = new SongQuery(locale, seed, 0, page, 1);
        var mp3 = _catalog.GetPreviewMp3(query, sequenceIndex);

        return File(mp3, "audio/mpeg", enableRangeProcessing: true);
    }

    [HttpGet("export.zip")]
    public IActionResult ExportZip(
        [FromQuery] string locale = "en-US",
        [FromQuery] ulong seed = 1,
        [FromQuery] double likesAverage = 3.7,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new SongQuery(locale, seed, likesAverage, page, pageSize);
        var zip = _catalog.ExportMp3Zip(query, page, pageSize);

        return File(zip, "application/zip", $"songs-page-{page}.zip");
    }
}