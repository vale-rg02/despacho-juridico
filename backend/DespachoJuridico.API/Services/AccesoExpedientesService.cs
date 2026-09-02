using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Services;

public interface IAccesoExpedientesService
{
    Task<bool> TieneAccesoAsync(int usuarioActualId, int? usuarioAsignadoId, int expedienteId);
}

// Regla compartida por ExpedientesController y AcuerdosController (antes duplicada
// e inconsistente entre ambos). Socio Principal (id=1) y niveles Administrativo/
// Superior mantienen acceso total. Las cuentas de soporte (dev1/dev2) mantienen su
// alcance actual sin cambios: solo ven lo que tienen asignado a sí mismas. Cualquier
// otro usuario (litigante real) ve/opera cualquier expediente, excepto los asignados
// a una cuenta de soporte — así se mantiene oculto el espacio de pruebas de dev1/dev2
// en ambas direcciones. Además, ser colaborador explícito (ExpedienteAccesos) siempre
// da acceso, incluso si el expediente estuviera asignado a una cuenta de soporte.
public class AccesoExpedientesService : IAccesoExpedientesService
{
    private readonly AppDbContext _context;

    public AccesoExpedientesService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> TieneAccesoAsync(int usuarioActualId, int? usuarioAsignadoId, int expedienteId)
    {
        if (usuarioActualId == 1) return true;

        var usuarioActual = await _context.Usuarios
            .Where(u => u.Id == usuarioActualId)
            .Select(u => new { u.EsCuentaSoporte, u.NivelAcceso })
            .FirstOrDefaultAsync();

        if (usuarioActual == null) return false;

        if (!usuarioActual.EsCuentaSoporte &&
            (usuarioActual.NivelAcceso == NivelAcceso.Administrativo || usuarioActual.NivelAcceso == NivelAcceso.Superior))
            return true;

        var esColaborador = await _context.ExpedienteAccesos
            .AnyAsync(a => a.ExpedienteId == expedienteId && a.UsuarioId == usuarioActualId);
        if (esColaborador) return true;

        if (usuarioActual.EsCuentaSoporte)
            return usuarioAsignadoId == usuarioActualId;

        if (usuarioAsignadoId == usuarioActualId) return true;
        if (!usuarioAsignadoId.HasValue) return true;

        var asignadoEsSoporte = await _context.Usuarios
            .Where(u => u.Id == usuarioAsignadoId.Value)
            .Select(u => u.EsCuentaSoporte)
            .FirstOrDefaultAsync();

        return !asignadoEsSoporte;
    }
}
