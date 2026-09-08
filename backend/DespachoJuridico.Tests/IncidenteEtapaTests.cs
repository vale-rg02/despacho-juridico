using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.Tests;

// DJ-119: "Incidente" pedido por Mario para Hipotecario y Oral Mercantil —
// deliberadamente NO se agregó a Especial/Ordinario Mercantil (sin catálogo
// propio, cero expedientes activos reales) ni al resto de tipos, por falta de
// evidencia real de que aplique ahí (mismo criterio de DJ-78). InMemory porque
// SeedEtapasCatalogoAsync corre consultas reales sobre EtapasCatalogo, no solo
// lógica interna (mismo criterio que RemateSubmenuTests/TerminoTipoJuicioTests).
public class IncidenteEtapaTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    [Theory]
    [InlineData("Hipotecario")]
    [InlineData("Oral Mercantil")]
    public async Task SeedEtapasCatalogo_AgregaIncidenteEnLosTiposConfirmados(string tipoJuicio)
    {
        using var context = CrearContextoEnMemoria($"{nameof(SeedEtapasCatalogo_AgregaIncidenteEnLosTiposConfirmados)}_{tipoJuicio}");

        await DbSeeder.SeedEtapasCatalogoAsync(context);

        var incidente = await context.EtapasCatalogo
            .SingleOrDefaultAsync(e => e.Nombre == "Incidente" && e.TipoJuicio == tipoJuicio);

        Assert.NotNull(incidente);
        Assert.NotNull(incidente!.TipoJuicio); // nunca huérfana (TipoJuicio=NULL), a diferencia del bug de DJ-78
        Assert.Null(incidente.EtapaPadreId); // etapa de primer nivel, no submenú
    }

    [Theory]
    [InlineData("Familiar")]
    [InlineData("Arrendamiento")]
    [InlineData("Jurisdiccion Voluntaria")]
    [InlineData("Especial Mercantil")]
    [InlineData("Ordinario Mercantil")]
    public async Task SeedEtapasCatalogo_NoAgregaIncidenteEnTiposSinEvidencia(string tipoJuicio)
    {
        using var context = CrearContextoEnMemoria($"{nameof(SeedEtapasCatalogo_NoAgregaIncidenteEnTiposSinEvidencia)}_{tipoJuicio}");

        await DbSeeder.SeedEtapasCatalogoAsync(context);

        var incidente = await context.EtapasCatalogo
            .SingleOrDefaultAsync(e => e.Nombre == "Incidente" && e.TipoJuicio == tipoJuicio);

        Assert.Null(incidente);
    }

    [Fact]
    public async Task SeedEtapasCatalogo_TotalDeFilasIncidente_EsExactamenteDos()
    {
        // Confirma el alcance completo, no solo caso por caso: "Incidente" debe
        // existir en TODO el catálogo sembrado en exactamente 2 filas (Hipotecario
        // + Oral Mercantil), ni una más.
        using var context = CrearContextoEnMemoria(nameof(SeedEtapasCatalogo_TotalDeFilasIncidente_EsExactamenteDos));

        await DbSeeder.SeedEtapasCatalogoAsync(context);

        var todas = await context.EtapasCatalogo.Where(e => e.Nombre == "Incidente").ToListAsync();

        Assert.Equal(2, todas.Count);
        Assert.Equal(["Hipotecario", "Oral Mercantil"], todas.Select(e => e.TipoJuicio).OrderBy(t => t));
    }

    [Fact]
    public async Task SeedEtapasCatalogo_EsIdempotente_CorrerloDosVecesNoDuplicaIncidente()
    {
        using var context = CrearContextoEnMemoria(nameof(SeedEtapasCatalogo_EsIdempotente_CorrerloDosVecesNoDuplicaIncidente));

        await DbSeeder.SeedEtapasCatalogoAsync(context);
        await DbSeeder.SeedEtapasCatalogoAsync(context); // segunda corrida, como en cada arranque de la app

        var todas = await context.EtapasCatalogo.Where(e => e.Nombre == "Incidente").ToListAsync();

        Assert.Equal(2, todas.Count);
    }

    [Theory]
    [InlineData("Hipotecario")]
    [InlineData("Oral Mercantil")]
    public async Task RegistrarHistorialEtapa_Incidente_GuardaYLeeCorrectamente(string tipoJuicio)
    {
        // Cubre "registro de un incidente en cada TipoJuicio donde aplique" del
        // criterio de aceptación.
        using var context = CrearContextoEnMemoria($"{nameof(RegistrarHistorialEtapa_Incidente_GuardaYLeeCorrectamente)}_{tipoJuicio}");
        await DbSeeder.SeedEtapasCatalogoAsync(context);
        var incidente = await context.EtapasCatalogo.SingleAsync(e => e.Nombre == "Incidente" && e.TipoJuicio == tipoJuicio);

        var usuario = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        var expediente = new Expediente { NumeroExpediente = "1/2026", ParteDemandada = "Juan Pérez", TipoJuicio = tipoJuicio, CreadoPorId = 1 };
        context.Usuarios.Add(usuario);
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        context.HistorialEtapas.Add(new HistorialEtapa
        {
            ExpedienteId = expediente.Id,
            EtapaCatalogoId = incidente.Id,
            FechaInicio = DateTime.UtcNow,
            RegistradoPorId = usuario.Id,
            Notas = "Incidente de nulidad de notificaciones"
        });
        await context.SaveChangesAsync();

        var leida = await context.HistorialEtapas
            .Include(h => h.EtapaCatalogo)
            .SingleAsync(h => h.ExpedienteId == expediente.Id);

        Assert.Equal("Incidente", leida.EtapaCatalogo!.Nombre);
        Assert.Equal(tipoJuicio, leida.EtapaCatalogo.TipoJuicio);
    }

    [Theory]
    [InlineData("Especial Mercantil")]
    [InlineData("Familiar")]
    public async Task CatalogoPorTipoJuicio_NoOfreceIncidenteComoOpcion(string tipoJuicio)
    {
        // Simula exactamente el filtro que usa GET /api/etapas-catalogo?tipoJuicio=X
        // (EtapasCatalogoController.GetAll): confirma que el selector del frontend
        // nunca recibiría "Incidente" para estos tipos, sin necesitar ningún cambio
        // de frontend aparte — el filtrado ya es 100% por TipoJuicio en el backend.
        using var context = CrearContextoEnMemoria($"{nameof(CatalogoPorTipoJuicio_NoOfreceIncidenteComoOpcion)}_{tipoJuicio}");
        await DbSeeder.SeedEtapasCatalogoAsync(context);

        var catalogoDelTipo = await context.EtapasCatalogo
            .Where(e => e.TipoJuicio == tipoJuicio)
            .Select(e => e.Nombre)
            .ToListAsync();

        Assert.DoesNotContain("Incidente", catalogoDelTipo);
    }
}
