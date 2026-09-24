using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/acuerdos")]
[Authorize]
public class AcuerdosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAccesoExpedientesService _acceso;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly ILogger<AcuerdosController> _logger;

    public AcuerdosController(AppDbContext context, IAccesoExpedientesService acceso, IEmailService emailService, IConfiguration config, ILogger<AcuerdosController> logger)
    {
        _context = context;
        _acceso = acceso;
        _emailService = emailService;
        _config = config;
        _logger = logger;
    }

    // GET /api/acuerdos/no-vistos
    [HttpGet("no-vistos")]
    public async Task<IActionResult> GetNoVistos()
    {
        var usuarioIdActual = ObtenerUsuarioId();

        var noVistos = await _context.AcuerdosScrapeados
            .Where(a => !a.Visto && !a.Oculto && a.Expediente.UsuarioAsignadoId == usuarioIdActual)
            .OrderByDescending(a => a.FechaAcuerdo)
            .Select(a => new
            {
                a.Id,
                a.ExpedienteId,
                a.NumeroExpediente,
                a.NombreJuzgado,
                a.Sintesis,
                a.FechaAcuerdo
            })
            .ToListAsync();

        return Ok(noVistos);
    }

    // GET /api/acuerdos/{expedienteId}
    [HttpGet("{expedienteId:int}")]
    public async Task<IActionResult> GetByExpediente(int expedienteId)
    {
        var usuarioIdActual = ObtenerUsuarioId();
        var expediente = await _context.Expedientes.FindAsync(expedienteId);
        if (expediente == null || !await _acceso.TieneAccesoAsync(usuarioIdActual, expediente.UsuarioAsignadoId, expedienteId))
            return NotFound(new { mensaje = "Expediente no encontrado" });

        var acuerdos = await _context.AcuerdosScrapeados
            .Where(a => a.ExpedienteId == expedienteId && !a.Oculto)
            .OrderByDescending(a => a.FechaAcuerdo)
            .Select(a => new AcuerdoResponse
            {
                Id = a.Id,
                NumeroExpediente = a.NumeroExpediente,
                NombreJuzgado = a.NombreJuzgado,
                Partes = a.Partes,
                Sintesis = a.Sintesis,
                FechaAcuerdo = a.FechaAcuerdo,
                FechaDetectado = a.FechaDetectado,
                NotificacionEnviada = a.NotificacionEnviada,
                Visto = a.Visto,
                EsExhorto = a.EsExhorto,
                CiudadDestino = a.CiudadDestino,
                RegistradoManualmente = a.RegistradoManualmente,
                Confianza = a.Confianza,
                TipoAsunto = a.TipoAsunto
            })
            .ToListAsync();

        return Ok(acuerdos);
    }

    // POST /api/acuerdos/{expedienteId}/manual
    // DJ-108: registro manual de un acuerdo (exhorto o normal) que el scraper
    // no detectó -- respaldo para que el litigante nunca dependa 100% del
    // scraper. Acotado a expedientes propios (titular o colaborador, activos)
    // -- más estricto que el resto de acciones sobre acuerdos (TieneAccesoAsync,
    // que deja ver/operar cualquier expediente no-soporte): aquí se reusa el
    // mismo filtro de DJ-102 (AplicarFiltroExpedientesPropios) porque escribir
    // un registro nuevo en un expediente ajeno no debería depender de la regla
    // "litigantes trabajan en conjunto" pensada para lectura compartida.
    [HttpPost("{expedienteId}/manual")]
    public async Task<IActionResult> RegistrarManual(int expedienteId, [FromBody] RegistrarAcuerdoManualRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!request.EsExhorto && string.IsNullOrWhiteSpace(request.TipoAsunto))
            return BadRequest(new { mensaje = "El tipo de trámite es obligatorio para un acuerdo que no es exhorto" });

        var usuarioIdActual = ObtenerUsuarioId();
        var expediente = await _context.Expedientes
            .Include(e => e.UsuarioAsignado)
            .Include(e => e.Accesos).ThenInclude(a => a.Usuario)
            .FirstOrDefaultAsync(e => e.Id == expedienteId);

        var tieneAccesoPropio = expediente != null && await ScraperAcuerdosService
            .AplicarFiltroExpedientesPropios(_context.Expedientes.Where(e => e.Id == expedienteId), usuarioIdActual)
            .AnyAsync();

        if (!tieneAccesoPropio)
            return NotFound(new { mensaje = "Expediente no encontrado" });

        var acuerdo = new AcuerdoScrapeado
        {
            ExpedienteId = expedienteId,
            NumeroExpediente = expediente!.NumeroExpediente,
            IdUnidad = 0,
            NombreJuzgado = request.NombreJuzgado ?? string.Empty,
            Partes = expediente.ParteDemandada,
            Sintesis = request.Sintesis,
            FechaAcuerdo = request.FechaAcuerdo,
            FechaDetectado = DateTime.UtcNow,
            NotificacionEnviada = true,
            EsExhorto = request.EsExhorto,
            CiudadDestino = request.CiudadDestino,
            TipoAsunto = request.EsExhorto ? "Exhorto (manual)" : request.TipoAsunto,
            RegistradoManualmente = true,
            Visto = false
        };

        _context.AcuerdosScrapeados.Add(acuerdo);
        await _context.SaveChangesAsync();

        try
        {
            await NotificarColaboradoresAsync(expediente, acuerdo, usuarioIdActual);
        }
        catch (Exception ex)
        {
            // Un fallo de correo no debe tumbar un registro ya guardado -- el
            // litigante que lo capturó sigue viéndolo de inmediato en la página.
            _logger.LogError(ex, "No se pudo notificar a colaboradores del acuerdo manual {AcuerdoId}", acuerdo.Id);
        }

        return Ok(new AcuerdoResponse
        {
            Id = acuerdo.Id,
            NumeroExpediente = acuerdo.NumeroExpediente,
            NombreJuzgado = acuerdo.NombreJuzgado,
            Partes = acuerdo.Partes,
            Sintesis = acuerdo.Sintesis,
            FechaAcuerdo = acuerdo.FechaAcuerdo,
            FechaDetectado = acuerdo.FechaDetectado,
            NotificacionEnviada = acuerdo.NotificacionEnviada,
            Visto = acuerdo.Visto,
            EsExhorto = acuerdo.EsExhorto,
            CiudadDestino = acuerdo.CiudadDestino,
            RegistradoManualmente = acuerdo.RegistradoManualmente,
            Confianza = acuerdo.Confianza,
            TipoAsunto = acuerdo.TipoAsunto
        });
    }

    // DJ-108: avisa a los demás colaboradores del expediente (titular +
    // ExpedienteAccesos) que un acuerdo se registró a mano -- nunca al propio
    // usuario que lo capturó, porque ya lo sabe. A diferencia de
    // ScraperAcuerdosService.EnviarNotificacionAsync (que solo le manda al
    // titular), aquí puede haber varios destinatarios.
    private async Task NotificarColaboradoresAsync(Expediente expediente, AcuerdoScrapeado acuerdo, int usuarioIdActual)
    {
        var destinatarios = new Dictionary<int, (string Nombre, string Email)>();

        if (expediente.UsuarioAsignado != null && expediente.UsuarioAsignado.Id != usuarioIdActual)
            destinatarios[expediente.UsuarioAsignado.Id] = (expediente.UsuarioAsignado.Nombre, expediente.UsuarioAsignado.Email);

        foreach (var acceso in expediente.Accesos)
        {
            if (acceso.Usuario.Id != usuarioIdActual)
                destinatarios[acceso.Usuario.Id] = (acceso.Usuario.Nombre, acceso.Usuario.Email);
        }

        if (destinatarios.Count == 0) return;

        var actor = await _context.Usuarios.FindAsync(usuarioIdActual);
        var frontendBaseUrl = _config.GetValue<string>("Frontend:BaseUrl") ?? "https://app.acedoehijos.com";
        var url = System.Net.WebUtility.HtmlEncode(ScraperAcuerdosService.ConstruirUrlAcuerdo(frontendBaseUrl, expediente.Id));
        var asunto = $"Nuevo acuerdo registrado manualmente — Exp. {expediente.NumeroExpediente}";
        var actorNombreEnc = System.Net.WebUtility.HtmlEncode(actor?.Nombre ?? "Un colaborador");
        var numeroExpedienteEnc = System.Net.WebUtility.HtmlEncode(expediente.NumeroExpediente);
        var sintesisEnc = System.Net.WebUtility.HtmlEncode(acuerdo.Sintesis);

        var cuerpo = $@"
<p>{actorNombreEnc} registró manualmente un acuerdo en el expediente <strong>{numeroExpedienteEnc}</strong> (no detectado por el scraper).</p>
<p><strong>Fecha del acuerdo:</strong> {acuerdo.FechaAcuerdo:dd/MM/yyyy}</p>
<p><strong>Síntesis:</strong> {sintesisEnc}</p>
<p><a href=""{url}"">Ver el acuerdo</a></p>";

        foreach (var (nombre, email) in destinatarios.Values)
            await _emailService.EnviarAsync(email, nombre, asunto, cuerpo);
    }

    // DELETE /api/acuerdos/{id} — solo registros manuales; los del scraper son intocables
    [HttpDelete("{id}")]
    public async Task<IActionResult> EliminarManual(int id)
    {
        var usuarioIdActual = ObtenerUsuarioId();
        var acuerdo = await _context.AcuerdosScrapeados
            .Include(a => a.Expediente)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (acuerdo == null || !await _acceso.TieneAccesoAsync(usuarioIdActual, acuerdo.Expediente.UsuarioAsignadoId, acuerdo.ExpedienteId))
            return NotFound(new { mensaje = "Acuerdo no encontrado" });

        if (!acuerdo.RegistradoManualmente)
            return BadRequest(new { mensaje = "Solo se pueden eliminar registros manuales" });

        _context.AcuerdosScrapeados.Remove(acuerdo);
        await _context.SaveChangesAsync();

        return Ok(new { mensaje = "Registro eliminado correctamente" });
    }

    // PATCH /api/acuerdos/{id}/destino
    // Captura manual de la ciudad/estado destino de un acuerdo marcado como exhorto
    // (ADISON no expone ese dato en la lista pública)
    [HttpPatch("{id}/destino")]
    public async Task<IActionResult> ActualizarDestino(int id, [FromBody] ActualizarDestinoExhortoRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var usuarioIdActual = ObtenerUsuarioId();
        var acuerdo = await _context.AcuerdosScrapeados
            .Include(a => a.Expediente)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (acuerdo == null || !await _acceso.TieneAccesoAsync(usuarioIdActual, acuerdo.Expediente.UsuarioAsignadoId, acuerdo.ExpedienteId))
            return NotFound(new { mensaje = "Acuerdo no encontrado" });

        if (!acuerdo.EsExhorto)
            return BadRequest(new { mensaje = "Este acuerdo no está marcado como exhorto" });

        acuerdo.CiudadDestino = request.CiudadDestino;
        await _context.SaveChangesAsync();

        return Ok(new AcuerdoResponse
        {
            Id = acuerdo.Id,
            NumeroExpediente = acuerdo.NumeroExpediente,
            NombreJuzgado = acuerdo.NombreJuzgado,
            Partes = acuerdo.Partes,
            Sintesis = acuerdo.Sintesis,
            FechaAcuerdo = acuerdo.FechaAcuerdo,
            FechaDetectado = acuerdo.FechaDetectado,
            NotificacionEnviada = acuerdo.NotificacionEnviada,
            Visto = acuerdo.Visto,
            EsExhorto = acuerdo.EsExhorto,
            CiudadDestino = acuerdo.CiudadDestino,
            RegistradoManualmente = acuerdo.RegistradoManualmente,
            Confianza = acuerdo.Confianza
        });
    }

    // PATCH /api/acuerdos/{id}/visto
    [HttpPatch("{id}/visto")]
    public async Task<IActionResult> MarcarVisto(int id)
    {
        var usuarioIdActual = ObtenerUsuarioId();
        var acuerdo = await _context.AcuerdosScrapeados
            .Include(a => a.Expediente)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (acuerdo == null || !await _acceso.TieneAccesoAsync(usuarioIdActual, acuerdo.Expediente.UsuarioAsignadoId, acuerdo.ExpedienteId))
            return NotFound(new { mensaje = "Acuerdo no encontrado" });

        acuerdo.Visto = true;
        await _context.SaveChangesAsync();

        return Ok(new { mensaje = "Acuerdo marcado como visto" });
    }

    // PATCH /api/acuerdos/{id}/descartar — DJ-99
    // El litigante marca un acuerdo visible como no relevante para su caso (ej. un
    // falso positivo de Alta confianza, como los ya vistos en 150/2023, 368/2026,
    // 423/2025). Mismo permiso que el resto de acciones sobre un acuerdo puntual
    // (TieneAccesoAsync) — no se restringe a nivel admin como el panel de
    // ScraperController, porque esto solo cambia lo que ve ese litigante sobre ESE
    // expediente, no un catálogo compartido entre todos (ver AccesoExpedientesService).
    [HttpPatch("{id}/descartar")]
    public async Task<IActionResult> Descartar(int id)
    {
        var usuarioIdActual = ObtenerUsuarioId();
        var acuerdo = await _context.AcuerdosScrapeados
            .Include(a => a.Expediente)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (acuerdo == null || !await _acceso.TieneAccesoAsync(usuarioIdActual, acuerdo.Expediente.UsuarioAsignadoId, acuerdo.ExpedienteId))
            return NotFound(new { mensaje = "Acuerdo no encontrado" });

        if (acuerdo.Oculto)
            return BadRequest(new { mensaje = "Este acuerdo ya está oculto" });

        acuerdo.Oculto = true;
        acuerdo.DescartadoManualmente = true;
        await _context.SaveChangesAsync();

        await RegistrarBitacora(acuerdo.ExpedienteId, usuarioIdActual, "acuerdo_descartado",
            $"Acuerdo del {acuerdo.NombreJuzgado} ({acuerdo.FechaAcuerdo:yyyy-MM-dd}) descartado: \"{acuerdo.Sintesis}\"");

        return Ok(new { mensaje = "Acuerdo descartado correctamente" });
    }

    // PATCH /api/acuerdos/{id}/confirmar — DJ-122
    // El litigante confirma que un acuerdo "Media" (Confianza=Media, sugerido
    // porque coincide el banco pero no el nombre del demandado, o porque el
    // número coincide con más de un expediente suyo) sí le pertenece. Sube a
    // Alta confianza — no se reenvía el correo, ya se avisó al detectarlo como
    // sugerencia (ver EnviarNotificacionAsync).
    [HttpPatch("{id}/confirmar")]
    public async Task<IActionResult> Confirmar(int id)
    {
        var usuarioIdActual = ObtenerUsuarioId();
        var acuerdo = await _context.AcuerdosScrapeados
            .Include(a => a.Expediente)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (acuerdo == null || !await _acceso.TieneAccesoAsync(usuarioIdActual, acuerdo.Expediente.UsuarioAsignadoId, acuerdo.ExpedienteId))
            return NotFound(new { mensaje = "Acuerdo no encontrado" });

        if (acuerdo.Confianza != "Media")
            return BadRequest(new { mensaje = "Solo se pueden confirmar acuerdos sugeridos" });

        acuerdo.Confianza = "Alta";
        await _context.SaveChangesAsync();

        await RegistrarBitacora(acuerdo.ExpedienteId, usuarioIdActual, "acuerdo_confirmado",
            $"Acuerdo del {acuerdo.NombreJuzgado} ({acuerdo.FechaAcuerdo:yyyy-MM-dd}) confirmado: \"{acuerdo.Sintesis}\"");

        return Ok(new { mensaje = "Acuerdo confirmado correctamente" });
    }

    private async Task RegistrarBitacora(int expedienteId, int usuarioId, string accion, string detalle)
    {
        _context.BitacoraCambios.Add(new Models.BitacoraCambio
        {
            ExpedienteId = expedienteId,
            UsuarioId = usuarioId,
            Accion = accion,
            Detalle = detalle,
            Fecha = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    private int ObtenerUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null ? int.Parse(claim) : 2;
    }
}