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
    private readonly IAccesoExpedientesService _acceso;

    public ExpedientesController(
        AppDbContext context,
        ICalculadorFechasService calculador,
        IEmailService emailService,
        ILogger<ExpedientesController> logger,
        IAccesoExpedientesService acceso)
    {
        _context = context;
        _calculador = calculador;
        _emailService = emailService;
        _logger = logger;
        _acceso = acceso;
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
        var esCuentaSoporte = !esSocioPrincipal && await _context.Usuarios
            .Where(u => u.Id == usuarioIdActual)
            .Select(u => u.EsCuentaSoporte)
            .FirstOrDefaultAsync();

        var query = _context.Expedientes
            .Include(e => e.Banco)
            .Include(e => e.UsuarioAsignado)
            .Include(e => e.Accesos)
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
                query = query.Where(e =>
                    e.UsuarioAsignadoId == filtroUsuario ||
                    e.Accesos.Any(a => a.UsuarioId == filtroUsuario));
            }
        }
        else if (esCuentaSoporte)
        {
            // Cuentas de soporte (dev1/dev2): ven lo suyo, más lo que tengan
            // como colaborador (ej. entre dev1/dev2 para probar el feature)
            query = query.Where(e =>
                e.UsuarioAsignadoId == usuarioIdActual ||
                e.Accesos.Any(a => a.UsuarioId == usuarioIdActual));
        }
        else
        {
            // Litigantes reales: ven cualquier expediente (trabajan en conjunto),
            // excepto los asignados a una cuenta de soporte — esos quedan ocultos.
            // El filtro de "todos"/"mis expedientes"/"[compañero]" es opcional, igual
            // que para Socio Principal; si se pide un usuario de soporte específico,
            // la exclusión de abajo hace que simplemente no devuelva nada.
            query = query.Where(e => e.UsuarioAsignado == null || !e.UsuarioAsignado.EsCuentaSoporte);

            if (!usuarioId.HasValue)
            {
                // Además de titular, incluye expedientes donde es colaborador explícito
                query = query.Where(e =>
                    e.UsuarioAsignadoId == usuarioIdActual ||
                    e.Accesos.Any(a => a.UsuarioId == usuarioIdActual));
            }
            else if (usuarioId.Value != 0)
            {
                query = query.Where(e =>
                    e.UsuarioAsignadoId == usuarioId.Value ||
                    e.Accesos.Any(a => a.UsuarioId == usuarioId.Value));
            }
        }

        if (!string.IsNullOrWhiteSpace(estado) && Enum.TryParse<EstadoExpediente>(estado, true, out var estadoEnum))
        {
            query = query.Where(e => e.Estado == estadoEnum);
        }

        query = AplicarFiltroBusqueda(query, busqueda);

        var expedientes = await query
            .OrderByDescending(e => e.ActualizadoEn)
            .Select(e => MapToResponse(e, usuarioIdActual))
            .ToListAsync();

        return Ok(expedientes);
    }

    // GET /api/expedientes/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var usuarioIdActual = ObtenerUsuarioId();
        var expediente = await _context.Expedientes
            .Include(e => e.Banco)
            .Include(e => e.UsuarioAsignado)
            .Include(e => e.Accesos)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (expediente == null || !await _acceso.TieneAccesoAsync(usuarioIdActual, expediente.UsuarioAsignadoId, id))
            return NotFound(new { mensaje = "Expediente no encontrado" });

        return Ok(MapToResponse(expediente, usuarioIdActual));
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

        return CreatedAtAction(nameof(GetById), new { id = expediente.Id }, MapToResponse(expediente, usuarioId));
    }

    // PUT /api/expedientes/5
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ExpedienteUpdateRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var expediente = await _context.Expedientes.FindAsync(id);
        if (expediente == null || !await _acceso.TieneAccesoAsync(ObtenerUsuarioId(), expediente.UsuarioAsignadoId, id))
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
        await _context.Entry(expediente).Collection(e => e.Accesos).LoadAsync();

        if (usuarioAsignadoCambio)
        {
            await NotificarAsignacionAsync(expediente, usuarioId);
        }

        return Ok(MapToResponse(expediente, usuarioId));
    }

    // PATCH /api/expedientes/5/estado
    [HttpPatch("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoRequest request)
    {
        var expediente = await _context.Expedientes.FindAsync(id);
        if (expediente == null || !await _acceso.TieneAccesoAsync(ObtenerUsuarioId(), expediente.UsuarioAsignadoId, id))
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
        if (expediente == null || !await _acceso.TieneAccesoAsync(ObtenerUsuarioId(), expediente.UsuarioAsignadoId, id))
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
        if (expediente == null || !await _acceso.TieneAccesoAsync(ObtenerUsuarioId(), expediente.UsuarioAsignadoId, id))
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
        if (expedienteEtapas == null || !await _acceso.TieneAccesoAsync(ObtenerUsuarioId(), expedienteEtapas.UsuarioAsignadoId, id))
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
        if (expediente == null || !await _acceso.TieneAccesoAsync(ObtenerUsuarioId(), expediente.UsuarioAsignadoId, id))
            return NotFound(new { mensaje = "Expediente no encontrado" });

        var etapaCatalogo = await _context.EtapasCatalogo.FindAsync(request.EtapaCatalogoId);
        if (etapaCatalogo == null)
            return BadRequest(new { mensaje = "La etapa del catálogo no existe" });

        // Si no viene fecha límite explícita, se calcula con el catálogo
        // Normalizamos la fecha de inicio a UTC (PostgreSQL lo exige para timestamptz)
        var fechaInicioUtc = CombinarFechaHora(request.FechaInicio, request.HoraInicio);

        // Si no viene fecha límite explícita, se calcula con el catálogo (en días, sin hora);
        // la hora (si se mandó) se aplica encima tanto si vino explícita como si fue calculada.
        var fechaLimiteCalculada = request.FechaLimite
            ?? _calculador.CalcularFechaLimite(fechaInicioUtc, etapaCatalogo.TerminoDias, etapaCatalogo.EsDiasHabiles);

        DateTime? fechaLimiteUtc = fechaLimiteCalculada.HasValue
            ? CombinarFechaHora(fechaLimiteCalculada.Value, request.HoraLimite)
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

        if (historial == null || !await _acceso.TieneAccesoAsync(ObtenerUsuarioId(), historial.Expediente.UsuarioAsignadoId, id))
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

        if (historial == null || !await _acceso.TieneAccesoAsync(ObtenerUsuarioId(), historial.Expediente.UsuarioAsignadoId, id))
            return NotFound(new { mensaje = "Etapa no encontrada en este expediente" });

        var etapaCatalogo = await _context.EtapasCatalogo.FindAsync(request.EtapaCatalogoId);
        if (etapaCatalogo == null)
            return BadRequest(new { mensaje = "La etapa del catálogo no existe" });

        var fechaInicioUtc = CombinarFechaHora(request.FechaInicio, request.HoraInicio);
        DateTime? fechaLimiteUtc = request.FechaLimite.HasValue
            ? CombinarFechaHora(request.FechaLimite.Value, request.HoraLimite)
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

        if (historial == null || !await _acceso.TieneAccesoAsync(ObtenerUsuarioId(), historial.Expediente.UsuarioAsignadoId, id))
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

        if (historial == null || !await _acceso.TieneAccesoAsync(ObtenerUsuarioId(), historial.Expediente.UsuarioAsignadoId, id))
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

        if (historial == null || !await _acceso.TieneAccesoAsync(ObtenerUsuarioId(), historial.Expediente.UsuarioAsignadoId, id))
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
        if (expediente == null || !await _acceso.TieneAccesoAsync(ObtenerUsuarioId(), expediente.UsuarioAsignadoId, id))
            return NotFound(new { mensaje = "Expediente no encontrado" });

        var usuarioId = ObtenerUsuarioId();
        var numeroExpediente = expediente.NumeroExpediente;

        _context.Expedientes.Remove(expediente);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // GET /api/expedientes/5/accesos
    [HttpGet("{id}/accesos")]
    public async Task<IActionResult> GetAccesos(int id)
    {
        var expediente = await _context.Expedientes.FindAsync(id);
        if (expediente == null || !await _acceso.TieneAccesoAsync(ObtenerUsuarioId(), expediente.UsuarioAsignadoId, id))
            return NotFound(new { mensaje = "Expediente no encontrado" });

        var accesos = await _context.ExpedienteAccesos
            .Include(a => a.Usuario)
            .Where(a => a.ExpedienteId == id)
            .Select(a => new ExpedienteAccesoResponse
            {
                Id = a.Id,
                UsuarioId = a.UsuarioId,
                UsuarioNombre = a.Usuario.Nombre,
                UsuarioEmail = a.Usuario.Email,
                CreadoEn = a.CreadoEn
            })
            .ToListAsync();

        return Ok(accesos);
    }

    // POST /api/expedientes/5/accesos
    // Solo el Socio Principal (id=1) o el Titular del expediente pueden agregar colaboradores
    [HttpPost("{id}/accesos")]
    public async Task<IActionResult> AgregarAcceso(int id, [FromBody] AgregarAccesoRequest request)
    {
        var expediente = await _context.Expedientes.FindAsync(id);
        if (expediente == null) return NotFound(new { mensaje = "Expediente no encontrado" });

        var usuarioIdActual = ObtenerUsuarioId();
        var esSocioPrincipal = usuarioIdActual == 1;
        var esTitular = expediente.UsuarioAsignadoId == usuarioIdActual;

        if (!esSocioPrincipal && !esTitular)
            return Forbid();

        if (request.UsuarioId == expediente.UsuarioAsignadoId)
            return BadRequest(new { mensaje = "El usuario ya es el titular de este expediente" });

        // Igual que en UsuariosController.GetAll: solo se excluyen cuentas de soporte
        // si quien agrega NO es cuenta de soporte — un dev necesita poder agregar a
        // otro dev como colaborador de sus propios expedientes de prueba.
        var actualEsSoporte = !esSocioPrincipal && await _context.Usuarios
            .Where(u => u.Id == usuarioIdActual)
            .Select(u => u.EsCuentaSoporte)
            .FirstOrDefaultAsync();

        var usuarioQuery = _context.Usuarios.Where(u => u.Id == request.UsuarioId && u.Activo);
        if (!actualEsSoporte)
            usuarioQuery = usuarioQuery.Where(u => !u.EsCuentaSoporte);

        var usuario = await usuarioQuery.FirstOrDefaultAsync();

        if (usuario == null)
            return BadRequest(new { mensaje = "Usuario no válido" });

        var yaExiste = await _context.ExpedienteAccesos
            .AnyAsync(a => a.ExpedienteId == id && a.UsuarioId == request.UsuarioId);

        if (yaExiste)
            return BadRequest(new { mensaje = "El usuario ya tiene acceso a este expediente" });

        var acceso = new ExpedienteAcceso
        {
            ExpedienteId = id,
            UsuarioId = request.UsuarioId,
            CreadoEn = DateTime.UtcNow
        };

        _context.ExpedienteAccesos.Add(acceso);
        await _context.SaveChangesAsync();

        await RegistrarBitacora(id, usuarioIdActual, "colaborador_agregado",
            $"Se agregó a {usuario.Nombre} como colaborador");

        return Ok(new ExpedienteAccesoResponse
        {
            Id = acceso.Id,
            UsuarioId = usuario.Id,
            UsuarioNombre = usuario.Nombre,
            UsuarioEmail = usuario.Email,
            CreadoEn = acceso.CreadoEn
        });
    }

    // DELETE /api/expedientes/5/accesos/12
    // Solo el Socio Principal (id=1) o el Titular del expediente pueden quitar colaboradores
    [HttpDelete("{id}/accesos/{accesoId}")]
    public async Task<IActionResult> QuitarAcceso(int id, int accesoId)
    {
        var expediente = await _context.Expedientes.FindAsync(id);
        if (expediente == null) return NotFound(new { mensaje = "Expediente no encontrado" });

        var usuarioIdActual = ObtenerUsuarioId();
        var esSocioPrincipal = usuarioIdActual == 1;
        var esTitular = expediente.UsuarioAsignadoId == usuarioIdActual;

        if (!esSocioPrincipal && !esTitular)
            return Forbid();

        var acceso = await _context.ExpedienteAccesos
            .Include(a => a.Usuario)
            .FirstOrDefaultAsync(a => a.Id == accesoId && a.ExpedienteId == id);

        if (acceso == null)
            return NotFound(new { mensaje = "Acceso no encontrado" });

        _context.ExpedienteAccesos.Remove(acceso);
        await _context.SaveChangesAsync();

        await RegistrarBitacora(id, usuarioIdActual, "colaborador_removido",
            $"Se removió a {acceso.Usuario.Nombre} como colaborador");

        return Ok(new { mensaje = "Colaborador removido correctamente" });
    }

    // GET /api/expedientes/por-usuario
[HttpGet("por-usuario")]
public async Task<IActionResult> GetPorUsuario([FromQuery] string? busqueda)
{
    var usuarioIdActual = ObtenerUsuarioId();

    // Antes exclusivo de Socio Principal; ahora cualquier litigante real puede ver
    // los expedientes agrupados por compañero. Las cuentas de soporte (dev1/dev2)
    // quedan fuera, igual que antes.
    var esCuentaSoporte = usuarioIdActual != 1 && await _context.Usuarios
        .Where(u => u.Id == usuarioIdActual)
        .Select(u => u.EsCuentaSoporte)
        .FirstOrDefaultAsync();

    if (esCuentaSoporte)
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
            .Include(e => e.Accesos)
            .Where(e => e.UsuarioAsignadoId == u.Id && e.Estado != EstadoExpediente.Cerrado);

        query = AplicarFiltroBusqueda(query, busqueda);

        var expedientes = await query
            .OrderByDescending(e => e.ActualizadoEn)
            .Select(e => MapToResponse(e, usuarioIdActual))
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

    // Filtro de búsqueda compartido por GetAll y GetPorUsuario (DJ-110) — antes cada
    // uno tenía su propia copia de la comparación, con el mismo hueco de mayúsculas
    // en las dos. Insensible a mayúsculas/minúsculas: "Sue", "SUE" y "suE" deben
    // encontrar lo mismo. Se usa ILIKE (nativo de Postgres) en vez de .ToLower() en
    // ambos lados — Npgsql lo traduce directo al operador ILIKE de Postgres, sin
    // envolver la columna en una función que además le impediría usar un índice si
    // algún día se agrega uno.
    internal static IQueryable<Expediente> AplicarFiltroBusqueda(IQueryable<Expediente> query, string? busqueda)
    {
        if (string.IsNullOrWhiteSpace(busqueda)) return query;

        // unaccent() quita acentos de los dos lados antes de comparar, así que
        // "Mexico" encuentra "México" y viceversa — mismo tipo de variante de texto
        // que el caso de mayúsculas, ahora cubierto en la misma pasada. unaccent()
        // no le hace nada a "%"/"_" (no son caracteres acentuados), así que el
        // patrón con comodines se puede envolver completo sin romper el escapado.
        //
        // AppDbContext.Unaccent solo puede llamarse DENTRO del lambda de abajo — es
        // un método marcador ([DbFunction]) que EF Core traduce a SQL al armar la
        // consulta; invocarlo fuera de una expresión LINQ lanzaría NotSupportedException
        // porque nunca se ejecuta como C# de verdad, solo como unaccent() en Postgres.
        var patron = $"%{EscaparComodinesLike(busqueda)}%";
        return query.Where(e =>
            EF.Functions.ILike(AppDbContext.Unaccent(e.NumeroExpediente), AppDbContext.Unaccent(patron)) ||
            EF.Functions.ILike(AppDbContext.Unaccent(e.ParteDemandada), AppDbContext.Unaccent(patron)));
    }

    // Escapa los comodines de LIKE/ILIKE ("%", "_") y el propio carácter de escape
    // ("\") en un texto de búsqueda capturado por el usuario, para que se busque
    // como texto literal — sin esto, alguien buscando "50%" encontraría cualquier
    // expediente, no solo los que de verdad tienen "50%" en el texto.
    internal static string EscaparComodinesLike(string texto) =>
        texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static ExpedienteResponse MapToResponse(Expediente e, int usuarioIdActual) => new()
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
        EsColaborador = e.UsuarioAsignadoId != usuarioIdActual && e.Accesos.Any(a => a.UsuarioId == usuarioIdActual),
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


    // Combina la fecha con una hora opcional; si no se manda hora, queda a medianoche
    // (comportamiento anterior a DJ-66). Siempre normaliza a UTC para PostgreSQL.
    private static DateTime CombinarFechaHora(DateTime fecha, TimeOnly? hora)
    {
        var horaFinal = hora ?? TimeOnly.MinValue;
        return DateTime.SpecifyKind(fecha.Date + horaFinal.ToTimeSpan(), DateTimeKind.Utc);
    }
}