using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExpedientesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ExpedientesController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/expedientes?estado=Abierto&busqueda=673
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? estado, [FromQuery] string? busqueda)
    {
        var query = _context.Expedientes
            .Include(e => e.Banco)
            .Include(e => e.UsuarioAsignado)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado) && Enum.TryParse<EstadoExpediente>(estado, true, out var estadoEnum))
        {
            query = query.Where(e => e.Estado == estadoEnum);
        }

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            query = query.Where(e =>
                e.NumeroExpediente.Contains(busqueda) ||
                e.ParteDemandada.Contains(busqueda));
        }

        var expedientes = await query
            .OrderByDescending(e => e.ActualizadoEn)
            .Select(e => MapToResponse(e))
            .ToListAsync();

        return Ok(expedientes);
    }

    // GET /api/expedientes/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var expediente = await _context.Expedientes
            .Include(e => e.Banco)
            .Include(e => e.UsuarioAsignado)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (expediente == null)
            return NotFound(new { mensaje = "Expediente no encontrado" });

        return Ok(MapToResponse(expediente));
    }

    // POST /api/expedientes
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ExpedienteCreateRequest request)
    {
        var usuarioId = ObtenerUsuarioId();

        var expediente = new Expediente
        {
            NumeroExpediente = request.NumeroExpediente,
            ParteDemandada = request.ParteDemandada,
            BancoId = request.BancoId,
            Juzgado = request.Juzgado,
            Materia = request.Materia,
            TipoJuicio = request.TipoJuicio,
            Prioridad = request.Prioridad,
            Estado = EstadoExpediente.Abierto,
            UsuarioAsignadoId = request.UsuarioAsignadoId,
            ExpedienteRelacionadoId = request.ExpedienteRelacionadoId,
            Notas = request.Notas,
            CreadoPorId = usuarioId,
            CreadoEn = DateTime.UtcNow,
            ActualizadoEn = DateTime.UtcNow
        };

        _context.Expedientes.Add(expediente);
        await _context.SaveChangesAsync();

        await RegistrarBitacora(expediente.Id, usuarioId, "crear",
            $"Expediente {expediente.NumeroExpediente} creado");

        // Recargar con relaciones para la respuesta
        await _context.Entry(expediente).Reference(e => e.Banco).LoadAsync();
        await _context.Entry(expediente).Reference(e => e.UsuarioAsignado).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = expediente.Id }, MapToResponse(expediente));
    }

    // PUT /api/expedientes/5
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ExpedienteUpdateRequest request)
    {
        var expediente = await _context.Expedientes.FindAsync(id);
        if (expediente == null)
            return NotFound(new { mensaje = "Expediente no encontrado" });

        var usuarioId = ObtenerUsuarioId();
        var cambios = new List<string>();

        if (expediente.NumeroExpediente != request.NumeroExpediente)
            cambios.Add($"Número: '{expediente.NumeroExpediente}' → '{request.NumeroExpediente}'");
        if (expediente.ParteDemandada != request.ParteDemandada)
            cambios.Add($"Parte demandada: '{expediente.ParteDemandada}' → '{request.ParteDemandada}'");
        if (expediente.Juzgado != request.Juzgado)
            cambios.Add($"Juzgado: '{expediente.Juzgado}' → '{request.Juzgado}'");

        expediente.NumeroExpediente = request.NumeroExpediente;
        expediente.ParteDemandada = request.ParteDemandada;
        expediente.BancoId = request.BancoId;
        expediente.Juzgado = request.Juzgado;
        expediente.Materia = request.Materia;
        expediente.TipoJuicio = request.TipoJuicio;
        expediente.UsuarioAsignadoId = request.UsuarioAsignadoId;
        expediente.ExpedienteRelacionadoId = request.ExpedienteRelacionadoId;
        expediente.Notas = request.Notas;
        expediente.ActualizadoEn = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        if (cambios.Count > 0)
        {
            await RegistrarBitacora(expediente.Id, usuarioId, "editar", string.Join("; ", cambios));
        }

        await _context.Entry(expediente).Reference(e => e.Banco).LoadAsync();
        await _context.Entry(expediente).Reference(e => e.UsuarioAsignado).LoadAsync();

        return Ok(MapToResponse(expediente));
    }

    // PATCH /api/expedientes/5/estado
    [HttpPatch("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoRequest request)
    {
        var expediente = await _context.Expedientes.FindAsync(id);
        if (expediente == null)
            return NotFound(new { mensaje = "Expediente no encontrado" });

        var estadoAnterior = expediente.Estado;
        expediente.Estado = request.Estado;
        expediente.ActualizadoEn = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var usuarioId = ObtenerUsuarioId();
        await RegistrarBitacora(expediente.Id, usuarioId, "cambiar_estado",
            $"Estado: '{estadoAnterior}' → '{request.Estado}'");

        return Ok(new { mensaje = "Estado actualizado", estado = expediente.Estado.ToString() });
    }

    // PATCH /api/expedientes/5/prioridad
    [HttpPatch("{id}/prioridad")]
    public async Task<IActionResult> CambiarPrioridad(int id, [FromBody] CambiarPrioridadRequest request)
    {
        var expediente = await _context.Expedientes.FindAsync(id);
        if (expediente == null)
            return NotFound(new { mensaje = "Expediente no encontrado" });

        var prioridadAnterior = expediente.Prioridad;
        expediente.Prioridad = request.Prioridad;
        expediente.ActualizadoEn = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var usuarioId = ObtenerUsuarioId();
        await RegistrarBitacora(expediente.Id, usuarioId, "cambiar_prioridad",
            $"Prioridad: '{prioridadAnterior}' → '{request.Prioridad}'");

        return Ok(new { mensaje = "Prioridad actualizada", prioridad = expediente.Prioridad.ToString() });
    }

    // GET /api/expedientes/5/bitacora
    [HttpGet("{id}/bitacora")]
    public async Task<IActionResult> GetBitacora(int id)
    {
        var existe = await _context.Expedientes.AnyAsync(e => e.Id == id);
        if (!existe)
            return NotFound(new { mensaje = "Expediente no encontrado" });

        var bitacora = await _context.BitacoraCambios
            .Include(b => b.Usuario)
            .Where(b => b.ExpedienteId == id)
            .OrderByDescending(b => b.Fecha)
            .Select(b => new BitacoraResponse
            {
                Id = b.Id,
                Accion = b.Accion,
                Detalle = b.Detalle,
                Fecha = b.Fecha,
                UsuarioNombre = b.Usuario.Nombre
            })
            .ToListAsync();

        return Ok(bitacora);
    }

    // ───────────── Helpers privados ─────────────

    private static ExpedienteResponse MapToResponse(Expediente e) => new()
    {
        Id = e.Id,
        NumeroExpediente = e.NumeroExpediente,
        ParteDemandada = e.ParteDemandada,
        Juzgado = e.Juzgado,
        Materia = e.Materia,
        TipoJuicio = e.TipoJuicio,
        Estado = e.Estado.ToString(),
        Prioridad = e.Prioridad.ToString(),
        Notas = e.Notas,
        CreadoEn = e.CreadoEn,
        ActualizadoEn = e.ActualizadoEn,
        BancoId = e.BancoId,
        BancoNombre = e.Banco?.Nombre,
        UsuarioAsignadoId = e.UsuarioAsignadoId,
        UsuarioAsignadoNombre = e.UsuarioAsignado?.Nombre,
        ExpedienteRelacionadoId = e.ExpedienteRelacionadoId
    };

    private async Task RegistrarBitacora(int expedienteId, int usuarioId, string accion, string detalle)
    {
        _context.BitacoraCambios.Add(new BitacoraCambio
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
        // Por ahora, mientras no esté la autenticación obligatoria en estas rutas,
        // usamos el usuario 2 (Carlos) como default.
        // Cuando agreguemos [Authorize], esto se reemplaza por el claim del token.
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null ? int.Parse(claim) : 2;
    }
}