using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/citas")]
[Authorize]
public class CitasController : ControllerBase
{
    private readonly AppDbContext _context;

    public CitasController(AppDbContext context)
    {
        _context = context;
    }

    private int ObtenerUsuarioId() =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    // GET /api/citas?mes=7&anio=2026
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? mes, [FromQuery] int? anio)
    {
        var usuarioId = ObtenerUsuarioId();
        var query = _context.Citas
            .Include(c => c.Expediente)
            .Where(c => c.UsuarioId == usuarioId);

        if (mes.HasValue && anio.HasValue)
        {
            query = query.Where(c =>
                c.FechaHora.Month == mes.Value &&
                c.FechaHora.Year == anio.Value);
        }

        var citas = await query
            .OrderBy(c => c.FechaHora)
            .Select(c => new CitaResponse
            {
                Id = c.Id,
                ExpedienteId = c.ExpedienteId,
                NumeroExpediente = c.Expediente != null ? c.Expediente.NumeroExpediente : null,
                Titulo = c.Titulo,
                Descripcion = c.Descripcion,
                FechaHora = c.FechaHora
            })
            .ToListAsync();

        return Ok(citas);
    }

    // POST /api/citas
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearCitaRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var usuarioId = ObtenerUsuarioId();

        var cita = new Cita
        {
            UsuarioId = usuarioId,
            ExpedienteId = request.ExpedienteId,
            Titulo = request.Titulo,
            Descripcion = request.Descripcion,
            FechaHora = DateTime.SpecifyKind(request.FechaHora, DateTimeKind.Utc),
            CreadoEn = DateTime.UtcNow
        };

        _context.Citas.Add(cita);
        await _context.SaveChangesAsync();

        var expediente = request.ExpedienteId.HasValue
            ? await _context.Expedientes.FindAsync(request.ExpedienteId.Value)
            : null;

        return Ok(new CitaResponse
        {
            Id = cita.Id,
            ExpedienteId = cita.ExpedienteId,
            NumeroExpediente = expediente?.NumeroExpediente,
            Titulo = cita.Titulo,
            Descripcion = cita.Descripcion,
            FechaHora = cita.FechaHora
        });
    }

    // PUT /api/citas/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] EditarCitaRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var usuarioId = ObtenerUsuarioId();
        var cita = await _context.Citas.FindAsync(id);

        if (cita == null) return NotFound(new { mensaje = "Cita no encontrada" });
        if (cita.UsuarioId != usuarioId) return Forbid();

        cita.Titulo = request.Titulo;
        cita.ExpedienteId = request.ExpedienteId;
        cita.Descripcion = request.Descripcion;
        cita.FechaHora = DateTime.SpecifyKind(request.FechaHora, DateTimeKind.Utc);

        await _context.SaveChangesAsync();

        var expediente = cita.ExpedienteId.HasValue
            ? await _context.Expedientes.FindAsync(cita.ExpedienteId.Value)
            : null;

        return Ok(new CitaResponse
        {
            Id = cita.Id,
            ExpedienteId = cita.ExpedienteId,
            NumeroExpediente = expediente?.NumeroExpediente,
            Titulo = cita.Titulo,
            Descripcion = cita.Descripcion,
            FechaHora = cita.FechaHora
        });
    }

    // DELETE /api/citas/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var usuarioId = ObtenerUsuarioId();
        var cita = await _context.Citas.FindAsync(id);

        if (cita == null) return NotFound(new { mensaje = "Cita no encontrada" });
        if (cita.UsuarioId != usuarioId) return Forbid();

        _context.Citas.Remove(cita);
        await _context.SaveChangesAsync();

        return Ok(new { mensaje = "Cita eliminada" });
    }
}
