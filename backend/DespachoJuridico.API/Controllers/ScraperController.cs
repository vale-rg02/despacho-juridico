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
    // POST /api/scraper/ejecutar?fecha=2026-06-15&dryRun=true — no escribe en BD ni envía correo
    [HttpPost("ejecutar")]
    public async Task<IActionResult> Ejecutar([FromQuery] DateOnly? fecha, [FromQuery] bool dryRun = false)
    {
        var resultado = await _scraper.EjecutarScrapingAsync(fecha, dryRun);
        return Ok(resultado);
    }

    // POST /api/scraper/ejecutar-rango?fechaInicio=2026-07-14&fechaFin=2026-07-29
    // POST /api/scraper/ejecutar-rango?...&dryRun=true — no escribe en BD ni envía correo
    // Procesa varios días hábiles en una sola llamada (para recuperar histórico
    // antes de activar el scraper diario). Limitado a 5 días por llamada y con
    // una pausa de 30s entre fechas para no sobrecargar ADISON.
    [HttpPost("ejecutar-rango")]
    public async Task<IActionResult> EjecutarRango(
        [FromQuery] string fechaInicio,
        [FromQuery] string fechaFin,
        [FromQuery] bool dryRun = false)
    {
        if (!DateOnly.TryParse(fechaInicio, out var inicio) ||
            !DateOnly.TryParse(fechaFin, out var fin))
            return BadRequest(new { mensaje = "Formato de fecha inválido. Usar YYYY-MM-DD" });

        if (fin < inicio)
            return BadRequest(new { mensaje = "fechaFin debe ser mayor o igual a fechaInicio" });

        // Límite de seguridad: máximo 5 días por llamada
        var diasTotal = (fin.ToDateTime(TimeOnly.MinValue) - inicio.ToDateTime(TimeOnly.MinValue)).Days + 1;
        if (diasTotal > 5)
            return BadRequest(new { mensaje = "Máximo 5 días por llamada para no sobrecargar ADISON" });

        // Calcular días hábiles en el rango (excluir sábados y domingos)
        var diasHabiles = new List<DateOnly>();
        var fecha = inicio;
        while (fecha <= fin)
        {
            if (fecha.DayOfWeek != DayOfWeek.Saturday && fecha.DayOfWeek != DayOfWeek.Sunday)
                diasHabiles.Add(fecha);
            fecha = fecha.AddDays(1);
        }

        var resultados = new List<object>();

        foreach (var dia in diasHabiles)
        {
            var resultado = await _scraper.EjecutarScrapingAsync(dia, dryRun);
            resultados.Add(new
            {
                fecha = dia.ToString("yyyy-MM-dd"),
                resultado.ExpedientesConsultados,
                resultado.AcuerdosDetectados,
                resultado.JuzgadosConError,
                resultado.MatchesForaneosEvaluados
            });

            // Pausa entre fechas para no sobrecargar ADISON
            if (dia < diasHabiles.Last())
                await Task.Delay(TimeSpan.FromSeconds(30));
        }

        return Ok(new
        {
            dryRun,
            diasProcesados = diasHabiles.Count,
            resultados
        });
    }
}