using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DespachoJuridico.API.Controllers;

// DJ-102: antes AccesoAdmin a nivel de clase -- ahora solo cualquier usuario
// autenticado (cada endpoint admin de abajo re-declara AccesoAdmin explícitamente,
// para no abrir por accidente ninguno de los 4 ya existentes). Los 2 endpoints
// nuevos (actualizar-mis-expedientes / mi-estado-actualizacion) son los únicos
// pensados para cualquier litigante.
[ApiController]
[Route("api/scraper")]
[Authorize]
public class ScraperController : ControllerBase
{
    private readonly ScraperAcuerdosService _scraper;
    private readonly AppDbContext _context;
    private readonly FrasesGenericasCandidatasService _frasesGenericas;
    private readonly ReporteAuditoriaScraperService _reporteAuditoria;
    private readonly ILogger<ScraperController> _logger;

    // DJ-102: cooldown fijo entre usos del botón "Actualizar expedientes".
    private static readonly TimeSpan CooldownActualizacionManual = TimeSpan.FromMinutes(15);

    // Umbral para tratar un ScraperEnProgreso=true como obsoleto (el proceso murió a
    // medio de una corrida, ej. redeploy de Railway) en vez de bloquear al usuario
    // para siempre -- una corrida manual acotada a sus propios juzgados nunca debería
    // acercarse a esto.
    private static readonly TimeSpan UmbralProgresoObsoleto = TimeSpan.FromMinutes(10);

    public ScraperController(
        ScraperAcuerdosService scraper, AppDbContext context, FrasesGenericasCandidatasService frasesGenericas,
        ReporteAuditoriaScraperService reporteAuditoria, ILogger<ScraperController> logger)
    {
        _scraper = scraper;
        _context = context;
        _frasesGenericas = frasesGenericas;
        _reporteAuditoria = reporteAuditoria;
        _logger = logger;
    }

    private int ObtenerUsuarioId() =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    // GET /api/scraper/registros
    // GET /api/scraper/registros?fecha=2026-08-20
    // Todo lo que hay en AcuerdosScrapeados detectado ese día (hora de Hermosillo),
    // visible y oculto — para diagnosticar sin depender de acceso directo a la BD.
    [HttpGet("registros")]
    [Authorize(Policy = "AccesoAdmin")]
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
    [Authorize(Policy = "AccesoAdmin")]
    public async Task<IActionResult> Ejecutar([FromQuery] DateOnly? fecha, [FromQuery] bool dryRun = false, [FromQuery] string? idsUnidad = null, [FromQuery] bool notificar = true)
    {
        var resultado = await _scraper.EjecutarScrapingConCandadoAsync(fecha, dryRun, ParseIdsUnidad(idsUnidad), notificar);
        return Ok(resultado);
    }

    // POST /api/scraper/ejecutar-rango?fechaInicio=2026-07-14&fechaFin=2026-07-29
    // POST /api/scraper/ejecutar-rango?...&dryRun=true — no escribe en BD ni envía correo
    // Procesa varios días hábiles en una sola llamada (para recuperar histórico
    // antes de activar el scraper diario). Limitado a 5 días por llamada y con
    // una pausa de 30s entre fechas para no sobrecargar ADISON.
    [HttpPost("ejecutar-rango")]
    [Authorize(Policy = "AccesoAdmin")]
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
            var resultado = await _scraper.EjecutarScrapingConCandadoAsync(dia, dryRun, idsUnidadSet);
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
    // POST /api/scraper/reevaluar-ocultos?dryRun=false&notificar=false — aplica los cambios sin enviar correo
    // Vuelve a correr PartesCoinciden y el criterio de Media (DJ-122) con el umbral/
    // algoritmo ACTUALES sobre los acuerdos ya guardados con Confianza=Baja y
    // Oculto=true (falsos negativos reales de cuando el criterio era más estricto,
    // o de antes de que DJ-122 se desplegara). Los que ahora sí coinciden por nombre
    // se desocultan a Confianza=Alta; los que solo coinciden por banco se desocultan
    // a Confianza=Media; desde el 26 de octubre de 2026 también cubre Hermosillo-exacto
    // sin ningún nombre que verificar (ver comentario en ReevaluarOcultosAsync). Por
    // defecto dryRun=true: solo lista qué se desocultaría, sin tocar la BD ni enviar
    // correos. notificar=true por defecto (mismo criterio que Promover) -- en false
    // no envía correo, por si se prefiere revisar antes de avisar al litigante.
    [HttpPost("reevaluar-ocultos")]
    [Authorize(Policy = "AccesoAdmin")]
    public async Task<IActionResult> ReevaluarOcultos([FromQuery] bool dryRun = true, [FromQuery] bool notificar = true)
    {
        var resultado = await _scraper.ReevaluarOcultosAsync(dryRun, notificar);
        return Ok(resultado);
    }

    // POST /api/scraper/frases-genericas/vista-previa
    // POST /api/scraper/frases-genericas/vista-previa?dryRun=false — fuerza el envío real del correo ahora mismo
    // POST /api/scraper/frases-genericas/vista-previa?umbral=4 — prueba otro umbral sin tocar la configuración
    // DJ-133 (implementa el diseño de Parte 2 de DJ-131): calcula candidatos a
    // frase genérica nueva -- NUNCA cambia Oculto ni Confianza de ningún
    // acuerdo, solo lee. Por defecto dryRun=true: arma el reporte sin enviar
    // correo, mismo patrón que reevaluar-ocultos.
    // GET /api/scraper/bitacora-dia?fecha=2026-10-14
    // DJ-139: embudo del día -- por juzgado y total. Contrato de lectura para
    // DJ-140 (reporte diario de auditoría). Solo lectura, sin frontend, sin
    // tocar /registros.
    [HttpGet("bitacora-dia")]
    [Authorize(Policy = "AccesoAdmin")]
    public async Task<IActionResult> BitacoraDia([FromQuery] DateOnly? fecha = null)
    {
        var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var fechaConsulta = fecha ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaHoraria));

        var resumen = await _scraper.ObtenerResumenDiaAsync(fechaConsulta);
        return Ok(resumen);
    }

    [HttpPost("frases-genericas/vista-previa")]
    [Authorize(Policy = "AccesoAdmin")]
    public async Task<IActionResult> VistaPreviaFrasesGenericas([FromQuery] bool dryRun = true, [FromQuery] int? umbral = null)
    {
        var reporte = await _frasesGenericas.GenerarReporteAsync(umbral, HttpContext.RequestAborted);
        if (!dryRun)
            await _frasesGenericas.EnviarSiHayCandidatosAsync(reporte);

        return Ok(reporte);
    }

    // POST /api/scraper/frases-genericas/revisiones
    // DJ-137: registra que una persona ya revisó un candidato a frase genérica --
    // "Descartado" (nombre real, se excluye del reporte de ahí en adelante) o
    // "Aprobado" (jerga -- rastro informativo; el cambio real a
    // FrasesGenericasConocidas sigue siendo un commit revisado aparte, esto no lo
    // reemplaza). El candidato SÍ contiene texto real, por eso vive en una tabla
    // solo-admin y nunca en código fuente (ver docs/propuesta-revision-frases-genericas.md).
    [HttpPost("frases-genericas/revisiones")]
    [Authorize(Policy = "AccesoAdmin")]
    public async Task<IActionResult> RegistrarRevisionCandidato([FromBody] RevisionCandidatoRequest request)
    {
        var error = ValidarRevisionCandidato(request);
        if (error != null) return BadRequest(new { mensaje = error });

        var normalizado = FrasesGenericasCandidatasService.NormalizarCandidato(request.Candidato);

        // Un candidato no puede estar aprobado y descartado a la vez -- si ya se
        // marcó "Aprobado" en FrasesGenericasConocidas (código), no tiene sentido
        // registrarlo también como "Descartado" aquí.
        if (request.Decision == "Descartado"
            && ScraperAcuerdosService.FrasesGenericasConocidas.Select(FrasesGenericasCandidatasService.NormalizarCandidato).Contains(normalizado))
        {
            return BadRequest(new { mensaje = "Este candidato ya está en FrasesGenericasConocidas -- no se puede descartar lo que ya está aprobado." });
        }

        if (await _context.RevisionesCandidatoFraseGenerica.AnyAsync(r => r.CandidatoNormalizado == normalizado))
            return BadRequest(new { mensaje = "Este candidato ya tiene una revisión registrada." });

        var revision = new RevisionCandidatoFraseGenerica
        {
            CandidatoNormalizado = normalizado,
            Decision = request.Decision,
            Motivo = request.Motivo,
            RevisadoPorId = ObtenerUsuarioId()
        };

        _context.RevisionesCandidatoFraseGenerica.Add(revision);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // El chequeo de arriba (AnyAsync) no es atómico con este guardado -- si
            // dos solicitudes para el mismo candidato llegan casi al mismo tiempo,
            // ambas pueden pasarlo antes de que cualquiera guarde. El índice único
            // de la BD es la garantía real; esto solo lo convierte en un 400 legible
            // en vez de un error crudo.
            return BadRequest(new { mensaje = "Este candidato ya tiene una revisión registrada." });
        }

        return Ok(new { revision.Id });
    }

    // GET /api/scraper/frases-genericas/revisiones
    // Solo-admin -- la respuesta nunca incluye el candidato en texto plano, solo
    // metadatos (igual criterio que el resto de este archivo con texto de Partes).
    [HttpGet("frases-genericas/revisiones")]
    [Authorize(Policy = "AccesoAdmin")]
    public async Task<IActionResult> ListarRevisionesCandidato()
    {
        var revisiones = await _context.RevisionesCandidatoFraseGenerica
            .Include(r => r.RevisadoPor)
            .OrderByDescending(r => r.RevisadoEn)
            .Select(r => new RevisionCandidatoResponse
            {
                Id = r.Id,
                Decision = r.Decision,
                RevisadoEn = r.RevisadoEn,
                RevisadoPorNombre = r.RevisadoPor.Nombre
            })
            .ToListAsync();

        return Ok(revisiones);
    }

    internal static string? ValidarRevisionCandidato(RevisionCandidatoRequest req)
    {
        if (req.Decision != "Aprobado" && req.Decision != "Descartado")
            return "Decision debe ser Aprobado o Descartado.";
        if (string.IsNullOrWhiteSpace(req.Candidato))
            return "Candidato es requerido.";
        if (req.Candidato.Length > 300)
            return "Candidato debe tener 300 caracteres o menos.";
        if (req.Motivo != null && req.Motivo.Length > 200)
            return "Motivo debe tener 200 caracteres o menos.";
        return null;
    }

    // GET /api/scraper/auditoria-preview?fecha=2026-10-16
    // GET /api/scraper/auditoria-preview?fecha=2026-10-16&dryRun=false — avanza la marca de agua (no envía correo aquí)
    // DJ-140: arma el reporte diario de auditoría completo con datos reales,
    // sin enviar correo (dryRun=true por defecto, mismo patrón que
    // reevaluar-ocultos/frases-genericas). dryRun=false solo avanza la marca
    // de agua -- para enviar el correo real usar el ciclo automático
    // (Auditoria:Activo=true) una vez aprobada la vista previa.
    [HttpGet("auditoria-preview")]
    [Authorize(Policy = "AccesoAdmin")]
    public async Task<IActionResult> AuditoriaPreview([FromQuery] DateOnly? fecha = null, [FromQuery] bool dryRun = true)
    {
        var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var fechaConsulta = fecha ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaHoraria));

        var reporte = await _reporteAuditoria.GenerarReporteAsync(fechaConsulta, avanzarMarcaDeAgua: !dryRun, HttpContext.RequestAborted);
        return Ok(reporte);
    }

    // POST /api/scraper/hallazgos
    // DJ-140: registro manual de un hallazgo (S1/S2/S3) durante la revisión
    // diaria. Alimenta el conteo de días hábiles de este mismo reporte y el
    // registro diario de DJ-143 -- un solo registro para los dos. Sin frontend.
    [HttpPost("hallazgos")]
    [Authorize(Policy = "AccesoAdmin")]
    public async Task<IActionResult> RegistrarHallazgo([FromBody] HallazgoAuditoriaRequest request)
    {
        var error = ReporteAuditoriaScraperService.ValidarHallazgo(request);
        if (error != null) return BadRequest(new { mensaje = error });

        var hallazgo = new HallazgoAuditoria
        {
            Fecha = request.Fecha,
            Severidad = request.Severidad,
            Categoria = request.Categoria,
            Descripcion = request.Descripcion,
            NumeroExpediente = request.NumeroExpediente,
            NombreJuzgado = request.NombreJuzgado,
            RegistradoPorId = ObtenerUsuarioId()
        };

        _context.HallazgosAuditoria.Add(hallazgo);
        await _context.SaveChangesAsync();

        return Ok(new { hallazgo.Id });
    }

    // GET /api/scraper/hallazgos?fechaInicio=2026-10-15&fechaFin=2026-10-16
    // Sin filtro de fechas regresa todo el historial -- lo consume DJ-143
    // (ventana de observación) además de este mismo reporte. Sin frontend.
    [HttpGet("hallazgos")]
    [Authorize(Policy = "AccesoAdmin")]
    public async Task<IActionResult> ListarHallazgos([FromQuery] DateOnly? fechaInicio = null, [FromQuery] DateOnly? fechaFin = null)
    {
        var query = _context.HallazgosAuditoria.Include(h => h.RegistradoPor).AsQueryable();
        if (fechaInicio.HasValue) query = query.Where(h => h.Fecha >= fechaInicio.Value);
        if (fechaFin.HasValue) query = query.Where(h => h.Fecha <= fechaFin.Value);

        var hallazgos = await query
            .OrderBy(h => h.Fecha)
            .Select(h => new HallazgoAuditoriaResponse
            {
                Id = h.Id,
                Fecha = h.Fecha,
                Severidad = h.Severidad,
                Categoria = h.Categoria,
                Descripcion = h.Descripcion,
                NumeroExpediente = h.NumeroExpediente,
                NombreJuzgado = h.NombreJuzgado,
                RegistradoPorNombre = h.RegistradoPor.Nombre,
                CreadoEn = h.CreadoEn
            })
            .ToListAsync();

        return Ok(hallazgos);
    }

    // POST /api/scraper/colisiones-juzgado/{id}/reintentar
    // POST /api/scraper/colisiones-juzgado/{id}/reintentar?dryRun=false
    // DJ-140: recupera un hallazgo de la Cola 2b (BitacoraDescarteItemizado,
    // Razon=NUMERO_COINCIDE_JUZGADO_NO). Ese acuerdo nunca se guardó -- se
    // descartó antes de crear nada, y su texto (Partes, síntesis) nunca se
    // almacenó (DJ-139 nunca lo guarda) -- así que no hay nada que "restaurar"
    // como con un AcuerdoScrapeado. La única forma de recuperarlo es volver a
    // pedírselo a ADISON para el mismo juzgado y la misma fecha exacta de la
    // corrida original (se calcula a partir de CorridaId+IdUnidad, ya que
    // BitacoraDescarteItemizado no guarda la fecha consultada directamente).
    // Si nadie corrigió la causa real primero (el juzgado mal registrado en el
    // expediente, o un patrón de JuzgadoCoincide que le falte a ese juzgado),
    // esto simplemente lo vuelve a descartar igual -- comportamiento correcto,
    // no un error. dryRun=true por defecto, mismo patrón que /ejecutar.
    [HttpPost("colisiones-juzgado/{id}/reintentar")]
    [Authorize(Policy = "AccesoAdmin")]
    public async Task<IActionResult> ReintentarColisionJuzgado(int id, [FromQuery] bool dryRun = true)
    {
        var usuarioIdActual = ObtenerUsuarioId();
        var descarte = await _context.BitacoraDescarteItemizado.FindAsync(id);
        if (descarte == null) return NotFound(new { mensaje = "Descarte no encontrado" });
        if (descarte.Razon != "NUMERO_COINCIDE_JUZGADO_NO")
            return BadRequest(new { mensaje = "Este endpoint solo aplica a descartes de razón NUMERO_COINCIDE_JUZGADO_NO" });

        var corrida = await _context.BitacoraEvaluacionScraper
            .FirstOrDefaultAsync(b => b.CorridaId == descarte.CorridaId && b.IdUnidad == descarte.IdUnidad);
        if (corrida == null)
            return BadRequest(new { mensaje = "No se encontró la corrida original para calcular la fecha -- no se puede reintentar" });

        // Quién y cuándo -- a diferencia de Promover (BitacoraCambios, ligado a
        // un Expediente), esto no tiene un único expediente dueño (puede tocar
        // varios candidatos del mismo juzgado+fecha), así que queda en el log
        // de aplicación en vez de forzarlo en una tabla pensada para otra cosa.
        _logger.LogInformation(
            "Reintentar colisión de juzgado: descarte {DescarteId} (IdUnidad {IdUnidad}, fecha {Fecha}) solicitado por usuario {UsuarioId}, dryRun={DryRun}",
            id, descarte.IdUnidad, corrida.FechaConsultada, usuarioIdActual, dryRun);

        var resultado = await _scraper.EjecutarScrapingConCandadoAsync(
            corrida.FechaConsultada, dryRun, new HashSet<int> { descarte.IdUnidad });

        return Ok(resultado);
    }

    // POST /api/scraper/actualizar-mis-expedientes
    // DJ-102: dispara una corrida del scraper acotada a los expedientes activos
    // propios (titular o colaborador) de quien la pide. Responde de inmediato
    // (202) y corre en segundo plano -- nunca bloquea la petición HTTP. Sujeta a
    // cooldown fijo de 15 min y a no poder dispararse dos veces mientras la
    // anterior sigue en curso.
    [HttpPost("actualizar-mis-expedientes")]
    public async Task<IActionResult> ActualizarMisExpedientes()
    {
        var usuarioId = ObtenerUsuarioId();
        var usuario = await _context.Usuarios.FindAsync(usuarioId);
        if (usuario == null) return Unauthorized();

        var evaluacion = EvaluarCooldown(
            usuario.ScraperEnProgreso, usuario.ScraperIniciadoEn, usuario.UltimaConsultaScraperEn,
            DateTime.UtcNow, CooldownActualizacionManual, UmbralProgresoObsoleto);

        if (!evaluacion.PuedeIniciar)
            return Conflict(new { mensaje = evaluacion.Mensaje, cooldownRestanteSegundos = evaluacion.CooldownRestanteSegundos });

        usuario.ScraperEnProgreso = true;
        usuario.ScraperIniciadoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _ = Task.Run(() => _scraper.EjecutarScrapingManualAsync(usuarioId, CancellationToken.None));

        return Accepted(new { mensaje = "Actualización iniciada" });
    }

    // GET /api/scraper/mi-estado-actualizacion
    // DJ-102: para que el litigante vea si hay una actualización en curso, cuánto
    // falta de cooldown, y la fecha/hora real más reciente de sus datos (la corrida
    // automática global más reciente, o su propia corrida manual, la que sea más
    // nueva).
    [HttpGet("mi-estado-actualizacion")]
    public async Task<IActionResult> MiEstadoActualizacion()
    {
        var usuarioId = ObtenerUsuarioId();
        var usuario = await _context.Usuarios.FindAsync(usuarioId);
        if (usuario == null) return Unauthorized();

        var estadoGlobal = await _context.EstadosScraper.FindAsync(1);

        var evaluacion = EvaluarCooldown(
            usuario.ScraperEnProgreso, usuario.ScraperIniciadoEn, usuario.UltimaConsultaScraperEn,
            DateTime.UtcNow, CooldownActualizacionManual, UmbralProgresoObsoleto);

        DateTime? ultimaActualizacionEn = null;
        if (estadoGlobal?.UltimaCorridaCompletaEn.HasValue == true || usuario.UltimaConsultaScraperEn.HasValue)
        {
            ultimaActualizacionEn = new[] { estadoGlobal?.UltimaCorridaCompletaEn, usuario.UltimaConsultaScraperEn }
                .Where(f => f.HasValue)
                .Max();
        }

        return Ok(new EstadoActualizacionScraperResponse
        {
            EnProgreso = usuario.ScraperEnProgreso && !evaluacion.ProgresoObsoleto,
            UltimaActualizacionEn = ultimaActualizacionEn,
            CooldownRestanteSegundos = evaluacion.CooldownRestanteSegundos,
            PuedeActualizar = evaluacion.PuedeIniciar
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

    // DJ-102: pura y testeable sin BD/HTTP. Decide si el botón "Actualizar
    // expedientes" puede dispararse ahora mismo -- bloqueado si hay una corrida en
    // curso reciente (ScraperEnProgreso, salvo que ScraperIniciadoEn sea más viejo
    // que umbralProgresoObsoleto, señal de que el proceso murió a medio de una
    // corrida) o si no pasó el cooldown desde la última consulta manual.
    internal static (bool PuedeIniciar, bool ProgresoObsoleto, int CooldownRestanteSegundos, string? Mensaje) EvaluarCooldown(
        bool enProgreso, DateTime? iniciadoEn, DateTime? ultimaConsultaEn, DateTime ahora,
        TimeSpan cooldown, TimeSpan umbralProgresoObsoleto)
    {
        var progresoObsoleto = enProgreso && iniciadoEn.HasValue && (ahora - iniciadoEn.Value) > umbralProgresoObsoleto;

        if (enProgreso && !progresoObsoleto)
            return (false, progresoObsoleto, 0, "Ya hay una actualización en curso.");

        if (ultimaConsultaEn.HasValue)
        {
            var restante = cooldown - (ahora - ultimaConsultaEn.Value);
            if (restante > TimeSpan.Zero)
            {
                var segundos = (int)Math.Ceiling(restante.TotalSeconds);
                return (false, progresoObsoleto, segundos, $"Disponible de nuevo en {segundos} segundos.");
            }
        }

        return (true, progresoObsoleto, 0, null);
    }
}