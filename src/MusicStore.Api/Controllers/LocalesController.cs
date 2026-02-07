using Microsoft.AspNetCore.Mvc;
using MusicStore.Application.Abstractions;

namespace MusicStore.Api.Controllers;

[ApiController]
[Route("api/locales")]
public sealed class LocalesController : ControllerBase
{
    private readonly ILocaleDataProvider _locales;

    public LocalesController(ILocaleDataProvider locales)
    {
        _locales = locales;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(_locales.GetSupportedLocales());
    }
}
