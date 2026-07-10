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
    [HttpPost("ejecutar")]
    public async Task<IActionResult> Ejecutar()
    {
        await _scraper.EjecutarScrapingAsync();
        return Ok(new { mensaje = "Scraping ejecutado" });
    }
}