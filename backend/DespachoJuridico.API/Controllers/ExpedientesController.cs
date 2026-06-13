using Microsoft.AspNetCore.Authorization;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DespachoJuridico.API.Services;


namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpedientesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ICalculadorFechasService _calculador;

    public ExpedientesController(AppDbContext context, ICalculadorFechasService calculador)
    {
        _context = context;
        _calculador = calculador;
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
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

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
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

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
            cambios.Add($"Juzgado: '{expediente.Juzgado ?? "—"}' → '{request.Juzgado ?? "—"}'");

        if (expediente.Materia != request.Materia)
            cambios.Add($"Materia: '{expediente.Materia ?? "—"}' → '{request.Materia ?? "—"}'");

        if (expediente.TipoJuicio != request.TipoJuicio)
            cambios.Add($"Tipo de juicio: '{expediente.TipoJuicio ?? "—"}' → '{request.TipoJuicio ?? "—"}'");

        if (expediente.BancoId != request.BancoId)
            cambios.Add($"Banco: '{expediente.BancoId?.ToString() ?? "—"}' → '{request.BancoId?.ToString() ?? "—"}'");

        if (expediente.UsuarioAsignadoId != request.UsuarioAsignadoId)
            cambios.Add($"Usuario asignado: '{expediente.UsuarioAsignadoId?.ToString() ?? "—"}' → '{request.UsuarioAsignadoId?.ToString() ?? "—"}'");

        if (expediente.ExpedienteRelacionadoId != request.ExpedienteRelacionadoId)
            cambios.Add($"Expediente relacionado: '{expediente.ExpedienteRelacionadoId?.ToString() ?? "—"}' → '{request.ExpedienteRelacionadoId?.ToString() ?? "—"}'");

        if (expediente.Notas != request.Notas)
            cambios.Add("Notas actualizadas");

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

    // GET /api/expedientes/5/etapas
    [HttpGet("{id}/etapas")]
    public async Task<IActionResult> GetEtapas(int id)
    {
        var existe = await _context.Expedientes.AnyAsync(e => e.Id == id);
        if (!existe)
            return NotFound(new { mensaje = "Expediente no encontrado" });

        var etapas = await _context.HistorialEtapas
            .Include(h => h.EtapaCatalogo)
            .Include(h => h.RegistradoPor)
            .Where(h => h.ExpedienteId == id)
            .OrderByDescending(h => h.FechaInicio)
            .Select(h => new EtapaHistorialResponse
            {
                Id = h.Id,
                EtapaCatalogoId = h.EtapaCatalogoId,
                EtapaNombre = h.EtapaCatalogo != null ? h.EtapaCatalogo.Nombre : null,
                FechaInicio = h.FechaInicio,
                FechaLimite = h.FechaLimite,
                FechaCompletada = h.FechaCompletada,
                Atendido = h.Atendido,
                Notas = h.Notas,
                RegistradoPorNombre = h.RegistradoPor.Nombre
            })
            .ToListAsync();

        return Ok(etapas);
    }

    // POST /api/expedientes/5/etapas
    [HttpPost("{id}/etapas")]
    public async Task<IActionResult> RegistrarEtapa(int id, [FromBody] RegistrarEtapaRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var expediente = await _context.Expedientes.FindAsync(id);
        if (expediente == null)
            return NotFound(new { mensaje = "Expediente no encontrado" });

        var etapaCatalogo = await _context.EtapasCatalogo.FindAsync(request.EtapaCatalogoId);
        if (etapaCatalogo == null)
            return BadRequest(new { mensaje = "La etapa del catálogo no existe" });

        // Si no viene fecha límite explícita, se calcula con el catálogo
        // Normalizamos la fecha de inicio a UTC (PostgreSQL lo exige para timestamptz)
        var fechaInicioUtc = DateTime.SpecifyKind(request.FechaInicio.Date, DateTimeKind.Utc);

        // Si no viene fecha límite explícita, se calcula con el catálogo
        var fechaLimiteCalculada = request.FechaLimite
            ?? _calculador.CalcularFechaLimite(fechaInicioUtc, etapaCatalogo.TerminoDias, etapaCatalogo.EsDiasHabiles);

        DateTime? fechaLimiteUtc = fechaLimiteCalculada.HasValue
            ? DateTime.SpecifyKind(fechaLimiteCalculada.Value.Date, DateTimeKind.Utc)
            : null;

        var usuarioId = ObtenerUsuarioId();

        var historial = new HistorialEtapa
        {
            ExpedienteId = id,
            EtapaCatalogoId = etapaCatalogo.Id,
            FechaInicio = fechaInicioUtc,
            FechaLimite = fechaLimiteUtc,
            Notas = request.Notas,
            RegistradoPorId = usuarioId
        };

        _context.HistorialEtapas.Add(historial);

        expediente.ActualizadoEn = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await RegistrarBitacora(id, usuarioId, "etapa_nueva",
        $"Etapa '{etapaCatalogo.Nombre}' iniciada el {fechaInicioUtc:yyyy-MM-dd}" +
        (fechaLimiteUtc != null ? $", fecha límite: {fechaLimiteUtc:yyyy-MM-dd}" : ""));

        await _context.Entry(historial).Reference(h => h.RegistradoPor).LoadAsync();

        var response = new EtapaHistorialResponse
        {
            Id = historial.Id,
            EtapaCatalogoId = historial.EtapaCatalogoId,
            EtapaNombre = etapaCatalogo.Nombre,
            FechaInicio = historial.FechaInicio,
            FechaLimite = historial.FechaLimite,
            FechaCompletada = historial.FechaCompletada,
            Atendido = historial.Atendido,
            Notas = historial.Notas,
            RegistradoPorNombre = historial.RegistradoPor.Nombre
        };

        return CreatedAtAction(nameof(GetEtapas), new { id }, response);
    }

    // PUT /api/expedientes/5/etapas/12
    [HttpPut("{id}/etapas/{etapaId}")]
    public async Task<IActionResult> CompletarEtapa(int id, int etapaId, [FromBody] CompletarEtapaRequest request)
    {
        var historial = await _context.HistorialEtapas
            .Include(h => h.EtapaCatalogo)
            .FirstOrDefaultAsync(h => h.Id == etapaId && h.ExpedienteId == id);

        if (historial == null)
            return NotFound(new { mensaje = "Etapa no encontrada en este expediente" });

        historial.FechaCompletada = request.FechaCompletada.HasValue
            ? DateTime.SpecifyKind(request.FechaCompletada.Value.Date, DateTimeKind.Utc)
            : DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var usuarioId = ObtenerUsuarioId();
        await RegistrarBitacora(id, usuarioId, "etapa_completada",
            $"Etapa '{historial.EtapaCatalogo?.Nombre}' marcada como completada");

        return Ok(new
        {
            mensaje = "Etapa marcada como completada",
            fechaCompletada = historial.FechaCompletada
        });
    }

    // PATCH /api/expedientes/5/etapas/12/atendido
    [HttpPatch("{id}/etapas/{etapaId}/atendido")]
    public async Task<IActionResult> MarcarEtapaAtendida(int id, int etapaId)
    {
        var historial = await _context.HistorialEtapas
            .Include(h => h.EtapaCatalogo)
            .FirstOrDefaultAsync(h => h.Id == etapaId && h.ExpedienteId == id);

        if (historial == null)
            return NotFound(new { mensaje = "Etapa no encontrada en este expediente" });

        historial.Atendido = true;
        await _context.SaveChangesAsync();

        var usuarioId = ObtenerUsuarioId();
        await RegistrarBitacora(id, usuarioId, "etapa_atendida",
            $"Alerta de '{historial.EtapaCatalogo?.Nombre}' marcada como atendida");

        return Ok(new { mensaje = "Alerta marcada como atendida" });
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