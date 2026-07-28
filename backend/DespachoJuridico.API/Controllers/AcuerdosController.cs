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
            .Where(a => !a.Visto && a.Expediente.UsuarioAsignadoId == usuarioIdActual)
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
            .Where(a => a.ExpedienteId == expedienteId)
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
                RegistradoManualmente = a.RegistradoManualmente
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
            RegistradoManualmente = acuerdo.RegistradoManualmente
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
            RegistradoManualmente = acuerdo.RegistradoManualmente
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

    private int ObtenerUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null ? int.Parse(claim) : 2;
    }
}