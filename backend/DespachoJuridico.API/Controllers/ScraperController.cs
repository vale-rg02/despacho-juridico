using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/scraper")]
[Authorize(Policy = "AccesoAdmin")]
public class ScraperController : ControllerBase
{
    private readonly ScraperAcuerdosService _scraper;
    private readonly AppDbContext _context;

    public ScraperController(ScraperAcuerdosService scraper, AppDbContext context)
    {
        _scraper = scraper;
        _context = context;
    }

    // GET /api/scraper/registros
    // GET /api/scraper/registros?fecha=2026-08-20
    // Todo lo que hay en AcuerdosScrapeados detectado ese día (hora de Hermosillo),
    // visible y oculto — para diagnosticar sin depender de acceso directo a la BD.
    [HttpGet("registros")]
    public async Task<IActionResult> Registros([FromQuery] DateOnly? fecha = null)
    {
        var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var fechaConsulta = fecha ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaHoraria));

        var inicioLocal = DateTime.SpecifyKind(fechaConsulta.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var finLocal = DateTime.SpecifyKind(fechaConsulta.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(inicioLocal, zonaHoraria);
        var finUtc = TimeZoneInfo.ConvertTimeToUtc(finLocal, zonaHoraria);

        var registros = await _context.AcuerdosScrapeados
            .Include(a => a.Expediente).ThenInclude(e => e.UsuarioAsignado)
            .Where(a => a.FechaDetectado >= inicioUtc && a.FechaDetectado < finUtc)
            .OrderBy(a => a.FechaDetectado)
            .Select(a => new RegistroScraperDiaResponse
            {
                Id = a.Id,
                NumeroExpediente = a.NumeroExpediente,
                NombreJuzgado = a.NombreJuzgado,
                Asignado = a.Expediente.UsuarioAsignado != null ? a.Expediente.UsuarioAsignado.Nombre : null,
                FechaAcuerdo = a.FechaAcuerdo,
                FechaDetectado = a.FechaDetectado,
                Confianza = a.Confianza,
                Oculto = a.Oculto,
                NotificacionEnviada = a.NotificacionEnviada,
                Partes = a.Partes,
                ParteDemandada = a.Expediente.ParteDemandada,
                RegistradoManualmente = a.RegistradoManualmente,
                DescartadoManualmente = a.DescartadoManualmente
            })
            .ToListAsync();

        return Ok(new
        {
            fecha = fechaConsulta.ToString("yyyy-MM-dd"),
            totalRegistros = registros.Count,
            registros
        });
    }

    // POST /api/scraper/ejecutar
    // POST /api/scraper/ejecutar?fecha=2026-06-15
    // POST /api/scraper/ejecutar?fecha=2026-06-15&dryRun=true — no escribe en BD ni envía correo
    // POST /api/scraper/ejecutar?idsUnidad=173,300 — acota el escaneo a esos juzgados solamente
    // POST /api/scraper/ejecutar?fecha=2026-06-15&notificar=false — guarda los acuerdos
    //   (con su Confianza/Oculto reales) pero no envía correo; pensado para backfills de
    //   fechas atrasadas donde el registro histórico correcto no debe generar un correo
    //   "hoy" avisando de algo de hace semanas.
    [HttpPost("ejecutar")]
    public async Task<IActionResult> Ejecutar([FromQuery] DateOnly? fecha, [FromQuery] bool dryRun = false, [FromQuery] string? idsUnidad = null, [FromQuery] bool notificar = true)
    {
        var resultado = await _scraper.EjecutarScrapingAsync(fecha, dryRun, ParseIdsUnidad(idsUnidad), notificar);
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

    // POST /api/scraper/reevaluar-ocultos
    // POST /api/scraper/reevaluar-ocultos?dryRun=false — aplica los cambios y envía las notificaciones pendientes
    // Vuelve a correr PartesCoinciden con el umbral/algoritmo ACTUALES sobre los
    // acuerdos ya guardados con Confianza=Baja y Oculto=true (falsos negativos
    // reales de cuando el criterio era más estricto). Los que ahora sí coinciden
    // se desocultan, pasan a Confianza=Alta y se notifican. Por defecto dryRun=true:
    // solo lista qué se desocultaría, sin tocar la BD ni enviar correos.
    [HttpPost("reevaluar-ocultos")]
    public async Task<IActionResult> ReevaluarOcultos([FromQuery] bool dryRun = true)
    {
        var resultado = await _scraper.ReevaluarOcultosAsync(dryRun);
        return Ok(resultado);
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