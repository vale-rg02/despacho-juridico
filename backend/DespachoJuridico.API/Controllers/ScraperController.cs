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
    // POST /api/scraper/ejecutar?idsUnidad=173,300 — acota el escaneo a esos juzgados solamente
    [HttpPost("ejecutar")]
    public async Task<IActionResult> Ejecutar([FromQuery] DateOnly? fecha, [FromQuery] bool dryRun = false, [FromQuery] string? idsUnidad = null)
    {
        var resultado = await _scraper.EjecutarScrapingAsync(fecha, dryRun, ParseIdsUnidad(idsUnidad));
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
        [FromQuery] bool dryRun = false,
        [FromQuery] string? idsUnidad = null)
    {
        var idsUnidadSet = ParseIdsUnidad(idsUnidad);
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
            var resultado = await _scraper.EjecutarScrapingAsync(dia, dryRun, idsUnidadSet);
            resultados.Add(new
            {
                fecha = dia.ToString("yyyy-MM-dd"),
                resultado.ExpedientesConsultados,
                resultado.AcuerdosDetectados,
                resultado.JuzgadosConError,
                resultado.MatchesForaneosEvaluados,
                resultado.MatchesHermosilloEvaluados
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

    private static HashSet<int>? ParseIdsUnidad(string? idsUnidad)
    {
        if (string.IsNullOrWhiteSpace(idsUnidad)) return null;

        return idsUnidad
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToHashSet();
    }
}