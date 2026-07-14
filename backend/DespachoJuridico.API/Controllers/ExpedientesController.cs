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
    private readonly IEmailService _emailService;
    private readonly ILogger<ExpedientesController> _logger;

    public ExpedientesController(
        AppDbContext context,
        ICalculadorFechasService calculador,
        IEmailService emailService,
        ILogger<ExpedientesController> logger)
    {
        _context = context;
        _calculador = calculador;
        _emailService = emailService;
        _logger = logger;
    }


    // GET /api/expedientes?estado=Abierto&busqueda=673&usuarioId=2
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? estado,
        [FromQuery] string? busqueda,
        [FromQuery] int? usuarioId)
    {
        var usuarioIdActual = ObtenerUsuarioId();
        var nivelAcceso = User.FindFirst("NivelAcceso")?.Value;
        var esSocioPrincipal = usuarioIdActual == 1;

        var query = _context.Expedientes
            .Include(e => e.Banco)
            .Include(e => e.UsuarioAsignado)
            .AsQueryable();

        // Filtro por usuario según rol
        if (esSocioPrincipal)
        {
            // Socio Principal: si selecciona un usuario específico lo filtra,
            // si no, muestra solo los suyos por default
            var filtroUsuario = usuarioId ?? usuarioIdActual;
            if (usuarioId.HasValue && usuarioId.Value == 0)
            {
                // usuarioId=0 significa "todos" — no aplica filtro
            }
            else
            {
                query = query.Where(e => e.UsuarioAsignadoId == filtroUsuario);
            }
        }
        else
        {
            // Litigantes y Administradores Operativos: solo sus expedientes
            query = query.Where(e => e.UsuarioAsignadoId == usuarioIdActual);
        }

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

        if (expediente == null || !TieneAccesoAExpediente(expediente.UsuarioAsignadoId))
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

        await NotificarAsignacionAsync(expediente, usuarioId);

        return CreatedAtAction(nameof(GetById), new { id = expediente.Id }, MapToResponse(expediente));
    }

    // PUT /api/expedientes/5
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ExpedienteUpdateRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var expediente = await _context.Expedientes.FindAsync(id);
        if (expediente == null || !TieneAccesoAExpediente(expediente.UsuarioAsignadoId))
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

        var usuarioAsignadoCambio = expediente.UsuarioAsignadoId != request.UsuarioAsignadoId;
        if (usuarioAsignadoCambio)
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

        if (usuarioAsignadoCambio)
        {
            await NotificarAsignacionAsync(expediente, usuarioId);
        }

        return Ok(MapToResponse(expediente));
    }

    // PATCH /api/expedientes/5/estado
    [HttpPatch("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoRequest request)
    {
        var expediente = await _context.Expedientes.FindAsync(id);
        if (expediente == null || !TieneAccesoAExpediente(expediente.UsuarioAsignadoId))
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
        if (expediente == null || !TieneAccesoAExpediente(expediente.UsuarioAsignadoId))
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
        var expediente = await _context.Expedientes.FindAsync(id);
        if (expediente == null || !TieneAccesoAExpediente(expediente.UsuarioAsignadoId))
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
        var expedienteEtapas = await _context.Expedientes.FindAsync(id);
        if (expedienteEtapas == null || !TieneAccesoAExpediente(expedienteEtapas.UsuarioAsignadoId))
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
        if (expediente == null || !TieneAccesoAExpediente(expediente.UsuarioAsignadoId))
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
            .Include(h => h.Expediente)
            .FirstOrDefaultAsync(h => h.Id == etapaId && h.ExpedienteId == id);

        if (historial == null || !TieneAccesoAExpediente(historial.Expediente.UsuarioAsignadoId))
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

    // PATCH /api/expedientes/5/etapas/12
    [HttpPatch("{id}/etapas/{etapaId}")]
    public async Task<IActionResult> EditarEtapa(int id, int etapaId, [FromBody] EditarEtapaRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var historial = await _context.HistorialEtapas
            .Include(h => h.EtapaCatalogo)
            .Include(h => h.RegistradoPor)
            .Include(h => h.Expediente)
            .FirstOrDefaultAsync(h => h.Id == etapaId && h.ExpedienteId == id);

        if (historial == null || !TieneAccesoAExpediente(historial.Expediente.UsuarioAsignadoId))
            return NotFound(new { mensaje = "Etapa no encontrada en este expediente" });

        var etapaCatalogo = await _context.EtapasCatalogo.FindAsync(request.EtapaCatalogoId);
        if (etapaCatalogo == null)
            return BadRequest(new { mensaje = "La etapa del catálogo no existe" });

        var fechaInicioUtc = DateTime.SpecifyKind(request.FechaInicio.Date, DateTimeKind.Utc);
        DateTime? fechaLimiteUtc = request.FechaLimite.HasValue
            ? DateTime.SpecifyKind(request.FechaLimite.Value.Date, DateTimeKind.Utc)
            : null;

        var cambios = new List<string>();
        if (historial.EtapaCatalogoId != etapaCatalogo.Id)
            cambios.Add($"Etapa: '{historial.EtapaCatalogo?.Nombre ?? "—"}' → '{etapaCatalogo.Nombre}'");
        if (historial.FechaInicio != fechaInicioUtc)
            cambios.Add($"Fecha inicio: '{historial.FechaInicio:yyyy-MM-dd}' → '{fechaInicioUtc:yyyy-MM-dd}'");
        if (historial.FechaLimite != fechaLimiteUtc)
            cambios.Add($"Fecha límite: '{historial.FechaLimite?.ToString("yyyy-MM-dd") ?? "—"}' → '{fechaLimiteUtc?.ToString("yyyy-MM-dd") ?? "—"}'");
        if (historial.Notas != request.Notas)
            cambios.Add("Notas actualizadas");

        historial.EtapaCatalogoId = etapaCatalogo.Id;
        historial.FechaInicio = fechaInicioUtc;
        historial.FechaLimite = fechaLimiteUtc;
        historial.Notas = request.Notas;

        await _context.SaveChangesAsync();

        var usuarioId = ObtenerUsuarioId();
        if (cambios.Count > 0)
        {
            await RegistrarBitacora(id, usuarioId, "etapa_editada", string.Join("; ", cambios));
        }

        await _context.Entry(historial).Reference(h => h.EtapaCatalogo).LoadAsync();

        return Ok(new EtapaHistorialResponse
        {
            Id = historial.Id,
            EtapaCatalogoId = historial.EtapaCatalogoId,
            EtapaNombre = historial.EtapaCatalogo?.Nombre,
            FechaInicio = historial.FechaInicio,
            FechaLimite = historial.FechaLimite,
            FechaCompletada = historial.FechaCompletada,
            Atendido = historial.Atendido,
            Notas = historial.Notas,
            RegistradoPorNombre = historial.RegistradoPor.Nombre
        });
    }

    // DELETE /api/expedientes/5/etapas/12
    [HttpDelete("{id}/etapas/{etapaId}")]
    public async Task<IActionResult> EliminarEtapa(int id, int etapaId)
    {
        var historial = await _context.HistorialEtapas
            .Include(h => h.EtapaCatalogo)
            .Include(h => h.Expediente)
            .FirstOrDefaultAsync(h => h.Id == etapaId && h.ExpedienteId == id);

        if (historial == null || !TieneAccesoAExpediente(historial.Expediente.UsuarioAsignadoId))
            return NotFound(new { mensaje = "Etapa no encontrada en este expediente" });

        var nombreEtapa = historial.EtapaCatalogo?.Nombre ?? "Etapa";

        _context.HistorialEtapas.Remove(historial);
        await _context.SaveChangesAsync();

        var usuarioId = ObtenerUsuarioId();
        await RegistrarBitacora(id, usuarioId, "etapa_eliminada", $"Etapa '{nombreEtapa}' eliminada del historial");

        return NoContent();
    }

    // DELETE /api/expedientes/{id}/etapas/{etapaId}/completar
    [HttpDelete("{id}/etapas/{etapaId}/completar")]
    public async Task<IActionResult> RevertirEtapa(int id, int etapaId)
    {
        var historial = await _context.HistorialEtapas
            .Include(h => h.EtapaCatalogo)
            .Include(h => h.Expediente)
            .FirstOrDefaultAsync(h => h.Id == etapaId && h.ExpedienteId == id);

        if (historial == null || !TieneAccesoAExpediente(historial.Expediente.UsuarioAsignadoId))
            return NotFound(new { mensaje = "Etapa no encontrada en este expediente" });

        if (historial.FechaCompletada == null)
            return BadRequest(new { mensaje = "La etapa no está marcada como completada" });

        historial.FechaCompletada = null;
        historial.Atendido = false;
        await _context.SaveChangesAsync();

        var usuarioId = ObtenerUsuarioId();
        await RegistrarBitacora(id, usuarioId, "etapa_revertida",
            $"Etapa '{historial.EtapaCatalogo?.Nombre}' revertida a pendiente");

        return Ok(new { mensaje = "Etapa revertida correctamente" });
    }

    // PATCH /api/expedientes/5/etapas/12/atendido
    [HttpPatch("{id}/etapas/{etapaId}/atendido")]
    public async Task<IActionResult> MarcarEtapaAtendida(int id, int etapaId)
    {
        var historial = await _context.HistorialEtapas
            .Include(h => h.EtapaCatalogo)
            .Include(h => h.Expediente)
            .FirstOrDefaultAsync(h => h.Id == etapaId && h.ExpedienteId == id);

        if (historial == null || !TieneAccesoAExpediente(historial.Expediente.UsuarioAsignadoId))
            return NotFound(new { mensaje = "Etapa no encontrada en este expediente" });

        historial.Atendido = true;
        await _context.SaveChangesAsync();

        var usuarioId = ObtenerUsuarioId();
        await RegistrarBitacora(id, usuarioId, "etapa_atendida",
            $"Alerta de '{historial.EtapaCatalogo?.Nombre}' marcada como atendida");

        return Ok(new { mensaje = "Alerta marcada como atendida" });
    }

    // DELETE /api/expedientes/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var expediente = await _context.Expedientes.FindAsync(id);
        if (expediente == null || !TieneAccesoAExpediente(expediente.UsuarioAsignadoId))
            return NotFound(new { mensaje = "Expediente no encontrado" });

        var usuarioId = ObtenerUsuarioId();
        var numeroExpediente = expediente.NumeroExpediente;

        _context.Expedientes.Remove(expediente);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // GET /api/expedientes/por-usuario
[HttpGet("por-usuario")]
public async Task<IActionResult> GetPorUsuario([FromQuery] string? busqueda)
{
    var usuarioIdActual = ObtenerUsuarioId();
    if (usuarioIdActual != 1)
        return Forbid();

    var usuarios = await _context.Usuarios
        .Where(u => u.Activo && !u.EsCuentaSoporte)
        .OrderBy(u => u.Nombre)
        .ToListAsync();

    var resultado = new List<object>();

    foreach (var u in usuarios)
    {
        var query = _context.Expedientes
            .Include(e => e.Banco)
            .Include(e => e.UsuarioAsignado)
            .Where(e => e.UsuarioAsignadoId == u.Id && e.Estado != EstadoExpediente.Cerrado);

        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(e => e.NumeroExpediente.Contains(busqueda) || e.ParteDemandada.Contains(busqueda));

        var expedientes = await query
            .OrderByDescending(e => e.ActualizadoEn)
            .Select(e => MapToResponse(e))
            .ToListAsync();

        resultado.Add(new
        {
            usuarioId = u.Id,
            usuarioNombre = u.Nombre,
            expedientes
        });
    }

    return Ok(resultado);
}

    // ───────────── Helpers privados ─────────────

    private async Task NotificarAsignacionAsync(Expediente expediente, int usuarioIdQueAsigna)
    {
        if (!expediente.UsuarioAsignadoId.HasValue) return;
        if (expediente.UsuarioAsignadoId.Value == usuarioIdQueAsigna) return; // no enviar si se asigna a sí mismo

        var usuarioAsignado = expediente.UsuarioAsignado
            ?? await _context.Usuarios.FindAsync(expediente.UsuarioAsignadoId.Value);
        if (usuarioAsignado == null) return;

        var asunto = $"Nuevo expediente asignado — {expediente.NumeroExpediente}";
        var cuerpo = ConstruirCuerpoAsignacion(usuarioAsignado, expediente);

        try
        {
            await _emailService.EnviarAsync(usuarioAsignado.Email, usuarioAsignado.Nombre, asunto, cuerpo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar correo de asignación a {Email} para expediente {Numero}",
                usuarioAsignado.Email, expediente.NumeroExpediente);
        }
    }

    private static string ConstruirCuerpoAsignacion(Usuario usuarioAsignado, Expediente expediente)
    {
        var nombre = System.Net.WebUtility.HtmlEncode(usuarioAsignado.Nombre);
        var numeroExpediente = System.Net.WebUtility.HtmlEncode(expediente.NumeroExpediente);
        var parteDemandada = System.Net.WebUtility.HtmlEncode(expediente.ParteDemandada);
        var juzgado = System.Net.WebUtility.HtmlEncode(expediente.Juzgado ?? "—");
        var materia = System.Net.WebUtility.HtmlEncode(expediente.Materia ?? "—");

        return $@"
<!DOCTYPE html><html><head><meta charset='UTF-8'>
<style>
  body{{font-family:Georgia,'Times New Roman',serif;background:#f7f5f0;margin:0;padding:0;}}
  .container{{max-width:580px;margin:40px auto;background:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);}}
  .header{{background:#1c2b4a;padding:32px 40px;text-align:center;}}
  .header h1{{color:#ffffff;font-size:20px;margin:0;font-weight:normal;letter-spacing:1px;}}
  .header p{{color:#9a7c3c;font-size:13px;margin:6px 0 0;letter-spacing:2px;text-transform:uppercase;}}
  .body{{padding:40px;color:#333333;}}
  .body p{{font-size:15px;line-height:1.7;margin:0 0 16px;}}
  .highlight{{background:#f0f4fa;border-left:4px solid #9a7c3c;padding:16px 20px;margin:24px 0;border-radius:0 6px 6px 0;}}
  .highlight p{{margin:4px 0;font-size:14px;color:#444;}}
  .highlight strong{{color:#1c2b4a;}}
  .footer{{background:#f7f5f0;padding:20px 40px;text-align:center;border-top:1px solid #e0ddd6;}}
  .footer p{{font-size:12px;color:#888;margin:0;}}
</style></head>
<body><div class='container'>
  <div class='header'>
    <h1>Despacho Jurídico Acedo e Hijos</h1>
    <p>Nuevo expediente asignado</p>
  </div>
  <div class='body'>
    <p>Estimado(a) {nombre},</p>
    <p>Se le ha asignado un nuevo expediente en el Sistema de Gestión del Despacho
    Jurídico Acedo e Hijos. A continuación encontrará los datos del caso:</p>
    <div class='highlight'>
      <p><strong>Expediente:</strong> {numeroExpediente}</p>
      <p><strong>Parte demandada:</strong> {parteDemandada}</p>
      <p><strong>Juzgado:</strong> {juzgado}</p>
      <p><strong>Materia:</strong> {materia}</p>
      <p><strong>Prioridad:</strong> {expediente.Prioridad}</p>
    </div>
    <p>Por favor acceda al sistema para revisar los detalles completos del expediente
    y registrar las etapas procesales correspondientes.</p>
    <p>Atentamente,<br><strong>Despacho Jurídico Acedo e Hijos</strong></p>
  </div>
  <div class='footer'>
    <p>Este es un mensaje automático del Sistema de Gestión de Expedientes.</p>
    <p>Por favor no responda a este correo.</p>
  </div>
</div></body></html>";
    }

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
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null ? int.Parse(claim) : 2;
    }

    // Solo el Socio Principal (id=1) puede operar sobre expedientes de cualquier
    // usuario; el resto solo sobre los que tiene asignados. Mismo criterio que GetAll.
    private bool TieneAccesoAExpediente(int? usuarioAsignadoId)
    {
        var usuarioIdActual = ObtenerUsuarioId();
        return usuarioIdActual == 1 || usuarioAsignadoId == usuarioIdActual;
    }
}