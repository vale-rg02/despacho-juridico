using DespachoJuridico.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/acuerdos")]
[Authorize]
public class AcuerdosController : ControllerBase
{
    private readonly AppDbContext _context;

    public AcuerdosController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/acuerdos/{expedienteId}
    [HttpGet("{expedienteId}")]
    public async Task<IActionResult> GetByExpediente(int expedienteId)
    {
        var acuerdos = await _context.AcuerdosScrapeados
            .Where(a => a.ExpedienteId == expedienteId)
            .OrderByDescending(a => a.FechaAcuerdo)
            .Select(a => new
            {
                a.Id,
                a.NumeroExpediente,
                a.NombreJuzgado,
                a.Partes,
                a.Sintesis,
                a.FechaAcuerdo,
                a.FechaDetectado,
                a.NotificacionEnviada,
                a.Visto
            })
            .ToListAsync();

        return Ok(acuerdos);
    }

    // PATCH /api/acuerdos/{id}/visto
    [HttpPatch("{id}/visto")]
    public async Task<IActionResult> MarcarVisto(int id)
    {
        var acuerdo = await _context.AcuerdosScrapeados.FindAsync(id);
        if (acuerdo == null)
            return NotFound(new { mensaje = "Acuerdo no encontrado" });

        acuerdo.Visto = true;
        await _context.SaveChangesAsync();

        return Ok(new { mensaje = "Acuerdo marcado como visto" });
    }
}