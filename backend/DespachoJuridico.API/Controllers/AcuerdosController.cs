using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
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

    public AcuerdosController(AppDbContext context, IAccesoExpedientesService acceso)
    {
        _context = context;
        _acceso = acceso;
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
                Confianza = a.Confianza
            })
            .ToListAsync();

        return Ok(acuerdos);
    }

    // POST /api/acuerdos/{expedienteId}/manual
    // Registro manual de un exhorto que el scraper no detectó
    [HttpPost("{expedienteId}/manual")]
    public async Task<IActionResult> RegistrarManual(int expedienteId, [FromBody] RegistrarExhortoManualRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var usuarioIdActual = ObtenerUsuarioId();
        var expediente = await _context.Expedientes.FindAsync(expedienteId);
        if (expediente == null || !await _acceso.TieneAccesoAsync(usuarioIdActual, expediente.UsuarioAsignadoId, expedienteId))
            return NotFound(new { mensaje = "Expediente no encontrado" });

        var acuerdo = new Models.AcuerdoScrapeado
        {
            ExpedienteId = expedienteId,
            NumeroExpediente = expediente.NumeroExpediente,
            IdUnidad = 0,
            NombreJuzgado = request.NombreJuzgado ?? string.Empty,
            Partes = expediente.ParteDemandada,
            Sintesis = request.Sintesis,
            FechaAcuerdo = request.FechaAcuerdo,
            FechaDetectado = DateTime.UtcNow,
            NotificacionEnviada = true,
            EsExhorto = true,
            CiudadDestino = request.CiudadDestino,
            TipoAsunto = "Exhorto (manual)",
            RegistradoManualmente = true,
            Visto = false
        };

        _context.AcuerdosScrapeados.Add(acuerdo);
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