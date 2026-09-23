using DespachoJuridico.API.Data;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Services;

// DJ-112/DJ-87: validación de Sede/Juzgado contra el catálogo, usada por
// ExpedientesController al crear/editar. Separado del controller (en vez de
// un método privado ahí) para poder probarlo sin construir todo
// ExpedientesController (4 dependencias: calculador de fechas, email, logger,
// acceso) -- esta validación solo necesita el DbContext.
public static class CatalogoUbicacionService
{
    // Solo valida si la petición trae Sede -- compatibilidad hacia atrás
    // mientras Frontend (servicio separado en Railway) no haya desplegado el
    // combobox nuevo. Sin Sede en la petición, se acepta Juzgado como texto
    // libre igual que hoy (ver plan de rollout de esta historia).
    public static async Task<string?> ValidarSedeYJuzgadoAsync(AppDbContext context, string? sede, string? juzgado)
    {
        if (sede == null) return null;

        var sedeCatalogo = await context.SedesCatalogo.FirstOrDefaultAsync(s => s.Nombre == sede);
        if (sedeCatalogo == null)
            return $"Sede '{sede}' no existe en el catálogo";

        if (juzgado == null) return null;

        var juzgadoValido = await context.JuzgadosCatalogo
            .AnyAsync(j => j.SedeId == sedeCatalogo.Id && j.Nombre == juzgado);
        if (!juzgadoValido)
            return $"Juzgado '{juzgado}' no pertenece a la sede '{sede}'";

        return null;
    }
}
