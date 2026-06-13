using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificacionesController : ControllerBase
{
    private readonly AppDbContext _context;

    public NotificacionesController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/notificaciones
    // Alertas activas (no atendidas) con fecha límite próxima o vencida
    [HttpGet]
    public async Task<IActionResult> GetAlertas()
    {
        var usuarioId = ObtenerUsuarioId();
        var rol = User.FindFirst(ClaimTypes.Role)?.Value;

        var hoy = DateTime.UtcNow.Date;

        var query = _context.HistorialEtapas
            .Include(h => h.Expediente)
            .Include(h => h.EtapaCatalogo)
            .Where(h => h.FechaCompletada == null
                     && h.FechaLimite != null
                     && !h.Atendido);

        // El socio ve todas las alertas; el litigante solo las de sus expedientes asignados
        if (rol != nameof(RolUsuario.Socio))
        {
            query = query.Where(h => h.Expediente.UsuarioAsignadoId == usuarioId);
        }

        var etapas = await query.ToListAsync();

        var alertas = etapas
            .Select(h =>
            {
                var dias = (h.FechaLimite!.Value.Date - hoy).Days;
                return new AlertaResponse
                {
                    EtapaHistorialId = h.Id,
                    ExpedienteId = h.ExpedienteId,
                    NumeroExpediente = h.Expediente.NumeroExpediente,
                    EtapaNombre = h.EtapaCatalogo?.Nombre,
                    FechaLimite = h.FechaLimite.Value,
                    DiasRestantes = dias,
                    Vencida = dias < 0
                };
            })
            // Mostramos vencidas y próximas dentro de 15 días
            .Where(a => a.DiasRestantes <= 15)
            // Más urgentes primero: vencidas (más negativas) y luego las que vencen antes
            .OrderBy(a => a.DiasRestantes)
            .ToList();

        return Ok(alertas);
    }

    private int ObtenerUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null ? int.Parse(claim) : 0;
    }
}