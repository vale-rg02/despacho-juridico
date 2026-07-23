using DespachoJuridico.API.Data;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Services;

public interface IAccesoExpedientesService
{
    Task<bool> TieneAccesoAsync(int usuarioActualId, int? usuarioAsignadoId);
}

// Regla compartida por ExpedientesController y AcuerdosController (antes duplicada
// e inconsistente entre ambos). Socio Principal (id=1) mantiene acceso total. Las
// cuentas de soporte (dev1/dev2) mantienen su alcance actual sin cambios: solo ven
// lo que tienen asignado a sí mismas. Cualquier otro usuario (litigante real) ahora
// ve/opera cualquier expediente, excepto los asignados a una cuenta de soporte —
// así se mantiene oculto el espacio de pruebas de dev1/dev2 en ambas direcciones.
public class AccesoExpedientesService : IAccesoExpedientesService
{
    private readonly AppDbContext _context;

    public AccesoExpedientesService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> TieneAccesoAsync(int usuarioActualId, int? usuarioAsignadoId)
    {
        if (usuarioActualId == 1) return true;

        var actualEsSoporte = await _context.Usuarios
            .Where(u => u.Id == usuarioActualId)
            .Select(u => u.EsCuentaSoporte)
            .FirstOrDefaultAsync();

        if (actualEsSoporte)
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
