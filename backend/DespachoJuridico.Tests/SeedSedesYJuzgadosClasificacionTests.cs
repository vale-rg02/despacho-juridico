using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-122: SeedSedesYJuzgadosAsync deriva Materias/EsMixto del propio Nombre
// del juzgado (ClasificarJuzgado, privado) -- estos casos son evidencia real
// tomada del catálogo (ver DbSeeder), no inventada, y cubren los patrones que
// aparecen más de una vez en los ~83 juzgados reales de Sonora.
public class SeedSedesYJuzgadosClasificacionTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    private static async Task<JuzgadoCatalogo> SembrarYBuscarAsync(AppDbContext context, string nombre)
    {
        await DbSeeder.SeedSedesYJuzgadosAsync(context);
        return await context.JuzgadosCatalogo.FirstAsync(j => j.Nombre == nombre);
    }

    [Fact]
    public async Task JuzgadoDeUnaSolaMateria_SeClasificaPorElNombre()
    {
        using var context = CrearContextoEnMemoria(nameof(JuzgadoDeUnaSolaMateria_SeClasificaPorElNombre));
        var juzgado = await SembrarYBuscarAsync(context, "1ro Civil Hermosillo");

        Assert.Equal("Civil", juzgado.Materias);
        Assert.False(juzgado.EsMixto);
    }

    [Fact]
    public async Task OralMercantil_SeClasificaComoMercantil()
    {
        using var context = CrearContextoEnMemoria(nameof(OralMercantil_SeClasificaComoMercantil));
        var juzgado = await SembrarYBuscarAsync(context, "1ro Oral Mercantil Hermosillo");

        Assert.Equal("Mercantil", juzgado.Materias);
    }

    [Fact]
    public async Task NombreConVariasMaterias_LasIncluyeTodas()
    {
        using var context = CrearContextoEnMemoria(nameof(NombreConVariasMaterias_LasIncluyeTodas));
        var juzgado = await SembrarYBuscarAsync(context, "Juzgado Civil/Familiar Especializado Guaymas");

        Assert.Equal("Civil,Familiar", juzgado.Materias);
        Assert.False(juzgado.EsMixto);
    }

    [Fact]
    public async Task NombreConMateriaFueraDeAlcance_SeIgnoraPeroNoRompeLasDemas()
    {
        // "Civil/Mercantil/Penal" -- Penal no es una materia que litigue el
        // despacho (no está en MATERIAS del frontend), se ignora sin forzarla.
        using var context = CrearContextoEnMemoria(nameof(NombreConMateriaFueraDeAlcance_SeIgnoraPeroNoRompeLasDemas));
        var juzgado = await SembrarYBuscarAsync(context, "Juzgado 1ro Civil/Mercantil/Penal Agua Prieta");

        Assert.Equal("Civil,Mercantil", juzgado.Materias);
    }

    [Fact]
    public async Task JuzgadoMixto_NoTieneMateriasPeroQuedaMarcadoEsMixto()
    {
        using var context = CrearContextoEnMemoria(nameof(JuzgadoMixto_NoTieneMateriasPeroQuedaMarcadoEsMixto));
        var juzgado = await SembrarYBuscarAsync(context, "Juzgado Mixto Álamos");

        Assert.Null(juzgado.Materias);
        Assert.True(juzgado.EsMixto);
    }

    [Fact]
    public async Task JuzgadoFueraDeLasMateriasDelDespacho_QuedaSinMateriaAsignada()
    {
        // Penal, Laboral, Tribunal Colegiado, etc. no mencionan ninguna de las
        // 4 materias del despacho -- se dejan ambiguos a propósito, no se
        // fuerzan (ver comentario en ClasificarJuzgado).
        using var context = CrearContextoEnMemoria(nameof(JuzgadoFueraDeLasMateriasDelDespacho_QuedaSinMateriaAsignada));
        var juzgado = await SembrarYBuscarAsync(context, "1ro Penal Hermosillo");

        Assert.Null(juzgado.Materias);
        Assert.False(juzgado.EsMixto);
    }

    [Fact]
    public async Task SegundaCorrida_ActualizaMateriasDeFilasYaSembradas()
    {
        // Simula el deploy a producción: el juzgado ya existía (desde DJ-87,
        // sin Materias) antes de que este seeder supiera clasificarlo -- debe
        // backfillearse en vez de quedarse null para siempre por el chequeo
        // de idempotencia.
        using var context = CrearContextoEnMemoria(nameof(SegundaCorrida_ActualizaMateriasDeFilasYaSembradas));
        var sede = new SedeCatalogo { Nombre = "Hermosillo" };
        context.SedesCatalogo.Add(sede);
        await context.SaveChangesAsync();
        context.JuzgadosCatalogo.Add(new JuzgadoCatalogo { Nombre = "1ro Civil Hermosillo", SedeId = sede.Id });
        await context.SaveChangesAsync();

        await DbSeeder.SeedSedesYJuzgadosAsync(context);

        var juzgado = await context.JuzgadosCatalogo.FirstAsync(j => j.Nombre == "1ro Civil Hermosillo");
        Assert.Equal("Civil", juzgado.Materias);
    }
}
