using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.Tests;

// Solicitud de Carlos (27 oct 2026): agregar "Providencias Precautorias" al
// catálogo de Oral Mercantil (materia Mercantil) -- deliberadamente NO se
// agrega a Especial/Ordinario Mercantil ni al resto de tipos, mismo criterio
// que Incidente (DJ-119, ver IncidenteEtapaTests.cs, que sigue el mismo
// patrón de pruebas). Pueden pedirse antes o durante el juicio, así que
// comparte Orden=1 con "Demanda" en vez de ocupar un punto fijo de la
// secuencia. InMemory porque SeedEtapasCatalogoAsync corre consultas reales
// sobre EtapasCatalogo, no solo lógica interna.
public class ProvidenciasPrecautoriasEtapaTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    [Fact]
    public async Task SeedEtapasCatalogo_AgregaProvidenciasPrecautoriasEnOralMercantil()
    {
        using var context = CrearContextoEnMemoria(nameof(SeedEtapasCatalogo_AgregaProvidenciasPrecautoriasEnOralMercantil));

        await DbSeeder.SeedEtapasCatalogoAsync(context);

        var etapa = await context.EtapasCatalogo
            .SingleOrDefaultAsync(e => e.Nombre == "Providencias Precautorias" && e.TipoJuicio == "Oral Mercantil");

        Assert.NotNull(etapa);
        Assert.Null(etapa!.EtapaPadreId); // etapa de primer nivel, sin submenú
        Assert.Equal(1, etapa.Orden); // comparte Orden con "Demanda"
        Assert.Null(etapa.TerminoDias); // sin base clara para un plazo específico
    }

    [Theory]
    [InlineData("Hipotecario")]
    [InlineData("Familiar")]
    [InlineData("Arrendamiento")]
    [InlineData("Jurisdiccion Voluntaria")]
    [InlineData("Especial Mercantil")]
    [InlineData("Ordinario Mercantil")]
    public async Task SeedEtapasCatalogo_NoAgregaProvidenciasPrecautoriasEnOtrosTipos(string tipoJuicio)
    {
        using var context = CrearContextoEnMemoria($"{nameof(SeedEtapasCatalogo_NoAgregaProvidenciasPrecautoriasEnOtrosTipos)}_{tipoJuicio}");

        await DbSeeder.SeedEtapasCatalogoAsync(context);

        var etapa = await context.EtapasCatalogo
            .SingleOrDefaultAsync(e => e.Nombre == "Providencias Precautorias" && e.TipoJuicio == tipoJuicio);

        Assert.Null(etapa);
    }

    [Fact]
    public async Task SeedEtapasCatalogo_TotalDeFilasProvidenciasPrecautorias_EsExactamenteUna()
    {
        // Confirma el alcance completo: debe existir en TODO el catálogo
        // sembrado en exactamente 1 fila (solo Oral Mercantil).
        using var context = CrearContextoEnMemoria(nameof(SeedEtapasCatalogo_TotalDeFilasProvidenciasPrecautorias_EsExactamenteUna));

        await DbSeeder.SeedEtapasCatalogoAsync(context);

        var todas = await context.EtapasCatalogo.Where(e => e.Nombre == "Providencias Precautorias").ToListAsync();

        Assert.Equal(["Oral Mercantil"], todas.Select(e => e.TipoJuicio));
    }

    [Fact]
    public async Task SeedEtapasCatalogo_EsIdempotente_CorrerloDosVecesNoDuplica()
    {
        using var context = CrearContextoEnMemoria(nameof(SeedEtapasCatalogo_EsIdempotente_CorrerloDosVecesNoDuplica));

        await DbSeeder.SeedEtapasCatalogoAsync(context);
        await DbSeeder.SeedEtapasCatalogoAsync(context); // segunda corrida, como en cada arranque de la app

        var todas = await context.EtapasCatalogo.Where(e => e.Nombre == "Providencias Precautorias").ToListAsync();

        Assert.Single(todas);
    }

    [Fact]
    public async Task SeedEtapasCatalogo_NoModificaNiBorraEtapasExistentesDeOralMercantil()
    {
        // La nueva etapa es puramente aditiva -- el resto del catálogo de Oral
        // Mercantil (24 filas antes de este cambio) debe seguir exactamente
        // igual, sin perder ni alterar ninguna.
        using var context = CrearContextoEnMemoria(nameof(SeedEtapasCatalogo_NoModificaNiBorraEtapasExistentesDeOralMercantil));

        await DbSeeder.SeedEtapasCatalogoAsync(context);

        var demanda = await context.EtapasCatalogo
            .SingleAsync(e => e.Nombre == "Demanda" && e.TipoJuicio == "Oral Mercantil");
        var totalOralMercantil = await context.EtapasCatalogo
            .CountAsync(e => e.TipoJuicio == "Oral Mercantil");

        Assert.Equal(1, demanda.Orden);
        Assert.Equal(25, totalOralMercantil); // 24 ya existentes + 1 nueva
    }

    [Fact]
    public async Task RegistrarHistorialEtapa_ProvidenciasPrecautorias_GuardaYLeeCorrectamente()
    {
        using var context = CrearContextoEnMemoria(nameof(RegistrarHistorialEtapa_ProvidenciasPrecautorias_GuardaYLeeCorrectamente));
        await DbSeeder.SeedEtapasCatalogoAsync(context);
        var etapa = await context.EtapasCatalogo
            .SingleAsync(e => e.Nombre == "Providencias Precautorias" && e.TipoJuicio == "Oral Mercantil");

        var usuario = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        var expediente = new Expediente { NumeroExpediente = "1/2026", ParteDemandada = "Juan Pérez", TipoJuicio = "Oral Mercantil", CreadoPorId = 1 };
        context.Usuarios.Add(usuario);
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        context.HistorialEtapas.Add(new HistorialEtapa
        {
            ExpedienteId = expediente.Id,
            EtapaCatalogoId = etapa.Id,
            FechaInicio = DateTime.UtcNow,
            RegistradoPorId = usuario.Id
        });
        await context.SaveChangesAsync();

        var leida = await context.HistorialEtapas
            .Include(h => h.EtapaCatalogo)
            .SingleAsync(h => h.ExpedienteId == expediente.Id);

        Assert.Equal("Providencias Precautorias", leida.EtapaCatalogo!.Nombre);
        Assert.Equal("Oral Mercantil", leida.EtapaCatalogo.TipoJuicio);
    }

    [Theory]
    [InlineData("Especial Mercantil")]
    [InlineData("Ordinario Mercantil")]
    [InlineData("Hipotecario")]
    public async Task CatalogoPorTipoJuicio_NoOfreceProvidenciasPrecautoriasComoOpcion(string tipoJuicio)
    {
        // Simula el filtro que usa GET /api/etapas-catalogo?tipoJuicio=X
        // (EtapasCatalogoController.GetAll): confirma que el selector del
        // frontend nunca recibiría "Providencias Precautorias" para estos
        // tipos, sin necesitar ningún cambio de frontend aparte.
        using var context = CrearContextoEnMemoria($"{nameof(CatalogoPorTipoJuicio_NoOfreceProvidenciasPrecautoriasComoOpcion)}_{tipoJuicio}");
        await DbSeeder.SeedEtapasCatalogoAsync(context);

        var catalogoDelTipo = await context.EtapasCatalogo
            .Where(e => e.TipoJuicio == tipoJuicio)
            .Select(e => e.Nombre)
            .ToListAsync();

        Assert.DoesNotContain("Providencias Precautorias", catalogoDelTipo);
    }
}
