using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/scraper")]
[Authorize(Policy = "AccesoAdmin")]
public class ScraperController : ControllerBase
{
    private readonly ScraperAcuerdosService _scraper;

    public ScraperController(ScraperAcuerdosService scraper)
    {
        _scraper = scraper;
    }

    // POST /api/scraper/ejecutar
    // POST /api/scraper/ejecutar?fecha=2026-06-15
    [HttpPost("ejecutar")]
    public async Task<IActionResult> Ejecutar([FromQuery] DateOnly? fecha)
    {
        var resultado = await _scraper.EjecutarScrapingAsync(fecha);
        return Ok(resultado);
    }
}