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

        var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var hoy = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaHoraria).Date;

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

    // GET /api/notificaciones/panel?usuarioId=2
    [HttpGet("panel")]
    public async Task<IActionResult> GetPanel([FromQuery] int? usuarioId)
    {
        var usuarioIdActual = ObtenerUsuarioId();
        var esSocioPrincipal = usuarioIdActual == 1;

        // Solo el Socio Principal puede ver notificaciones de otros usuarios.
        // usuarioId=0 significa "todos" (sin filtro), igual que en ExpedientesController.GetAll.
        // filtroUsuarioId == null => no se aplica filtro por usuario.
        int? filtroUsuarioId;
        if (esSocioPrincipal)
        {
            filtroUsuarioId = (usuarioId.HasValue && usuarioId.Value == 0)
                ? null
                : (usuarioId ?? usuarioIdActual);
        }
        else
        {
            filtroUsuarioId = usuarioIdActual;
        }

        var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var hoy = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaHoraria).Date;

        // ── Expedientes urgentes ──────────────────────────────────────────
        var expedientesUrgentesQuery = _context.Expedientes
            .Include(e => e.UsuarioAsignado)
            .Where(e => e.Prioridad == Prioridad.Urgente
                     && e.Estado == EstadoExpediente.Abierto);

        if (filtroUsuarioId.HasValue)
            expedientesUrgentesQuery = expedientesUrgentesQuery
                .Where(e => e.UsuarioAsignadoId == filtroUsuarioId.Value);

        var expedientesUrgentes = await expedientesUrgentesQuery
            .OrderByDescending(e => e.ActualizadoEn)
            .Select(e => new
            {
                e.Id,
                e.NumeroExpediente,
                e.ParteDemandada,
                e.Juzgado,
                e.Materia,
                UsuarioAsignadoNombre = e.UsuarioAsignado != null ? e.UsuarioAsignado.Nombre : null
            })
            .ToListAsync();

        // ── Etapas base (no completadas, con fecha límite, no atendidas) ──
        var etapasQuery = _context.HistorialEtapas
            .Include(h => h.Expediente)
                .ThenInclude(e => e.UsuarioAsignado)
            .Include(h => h.EtapaCatalogo)
            .Where(h => h.FechaCompletada == null
                     && h.FechaLimite != null);

        if (filtroUsuarioId.HasValue)
            etapasQuery = etapasQuery
                .Where(h => h.Expediente.UsuarioAsignadoId == filtroUsuarioId.Value);

        var todasEtapas = await etapasQuery.ToListAsync();

        // ── Próximas (no atendidas, vencen en los próximos 15 días) ──────
        var proximas = todasEtapas
            .Where(h => !h.Atendido)
            .Select(h => new
            {
                h.Id,
                h.ExpedienteId,
                NumeroExpediente = h.Expediente.NumeroExpediente,
                ParteDemandada = h.Expediente.ParteDemandada,
                Prioridad = h.Expediente.Prioridad.ToString(),
                UsuarioAsignadoNombre = h.Expediente.UsuarioAsignado?.Nombre,
                EtapaNombre = h.EtapaCatalogo?.Nombre,
                FechaLimite = h.FechaLimite!.Value,
                DiasRestantes = (h.FechaLimite!.Value.Date - hoy).Days
            })
            .Where(a => a.DiasRestantes >= 0 && a.DiasRestantes <= 15)
            .OrderBy(a => a.DiasRestantes)
            .ToList();

        // ── Vencidas (no atendidas, fecha límite ya pasó) ─────────────────
        var vencidas = todasEtapas
            .Where(h => !h.Atendido)
            .Select(h => new
            {
                h.Id,
                h.ExpedienteId,
                NumeroExpediente = h.Expediente.NumeroExpediente,
                ParteDemandada = h.Expediente.ParteDemandada,
                Prioridad = h.Expediente.Prioridad.ToString(),
                UsuarioAsignadoNombre = h.Expediente.UsuarioAsignado?.Nombre,
                EtapaNombre = h.EtapaCatalogo?.Nombre,
                FechaLimite = h.FechaLimite!.Value,
                DiasVencida = (hoy - h.FechaLimite!.Value.Date).Days
            })
            .Where(a => a.DiasVencida > 0)
            .OrderByDescending(a => a.DiasVencida)
            .ToList();

        // ── Atendidas (historial indefinido) ─────────────────────────────
        var atendidasQuery = _context.HistorialEtapas
            .Include(h => h.Expediente)
                .ThenInclude(e => e.UsuarioAsignado)
            .Include(h => h.EtapaCatalogo)
            .Where(h => h.Atendido
                     && h.FechaLimite != null);

        if (filtroUsuarioId.HasValue)
            atendidasQuery = atendidasQuery
                .Where(h => h.Expediente.UsuarioAsignadoId == filtroUsuarioId.Value);

        var atendidas = await atendidasQuery
            .OrderByDescending(h => h.FechaLimite)
            .Take(50) // Limitar a las 50 más recientes para no sobrecargar
            .Select(h => new
            {
                h.Id,
                h.ExpedienteId,
                NumeroExpediente = h.Expediente.NumeroExpediente,
                ParteDemandada = h.Expediente.ParteDemandada,
                UsuarioAsignadoNombre = h.Expediente.UsuarioAsignado != null
                    ? h.Expediente.UsuarioAsignado.Nombre : null,
                EtapaNombre = h.EtapaCatalogo != null ? h.EtapaCatalogo.Nombre : null,
                FechaLimite = h.FechaLimite!.Value,
            })
            .ToListAsync();

        return Ok(new
        {
            expedientesUrgentes,
            proximas,
            vencidas,
            atendidas
        });
    }

    // GET /api/notificaciones/usuarios-disponibles
    [HttpGet("usuarios-disponibles")]
    public async Task<IActionResult> GetUsuariosDisponibles()
    {
        var usuarioIdActual = ObtenerUsuarioId();
        if (usuarioIdActual != 1)
            return Forbid();

        var usuarios = await _context.Usuarios
            .Where(u => u.Activo && !u.EsCuentaSoporte)
            .OrderBy(u => u.Nombre)
            .Select(u => new { u.Id, u.Nombre })
            .ToListAsync();

        return Ok(usuarios);
    }

    private int ObtenerUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null ? int.Parse(claim) : 0;
    }
}