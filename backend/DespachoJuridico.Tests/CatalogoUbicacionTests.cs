using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-112/DJ-87: valida Sede/Juzgado contra el catálogo al crear/editar un
// expediente. InMemory, mismo patrón que ConfirmarAcuerdoTests.cs.
public class CatalogoUbicacionTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    private static async Task SembrarCatalogoAsync(AppDbContext context)
    {
        var hermosillo = new SedeCatalogo { Nombre = "Hermosillo" };
        var nogales = new SedeCatalogo { Nombre = "Nogales" };
        context.SedesCatalogo.AddRange(hermosillo, nogales);
        await context.SaveChangesAsync();

        context.JuzgadosCatalogo.AddRange(
            new JuzgadoCatalogo { Nombre = "1ro Civil Hermosillo", SedeId = hermosillo.Id },
            new JuzgadoCatalogo { Nombre = "Juzgado 1ro Civil Nogales", SedeId = nogales.Id }
        );
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task SinSedeEnLaPeticion_AceptaJuzgadoComoTextoLibre_CompatibilidadHaciaAtras()
    {
        // Mientras el Frontend viejo (sin combobox) siga desplegado, no manda
        // Sede -- no debe romperse la creación de expedientes.
        using var context = CrearContextoEnMemoria(nameof(SinSedeEnLaPeticion_AceptaJuzgadoComoTextoLibre_CompatibilidadHaciaAtras));
        await SembrarCatalogoAsync(context);

        var error = await CatalogoUbicacionService.ValidarSedeYJuzgadoAsync(context, sede: null, juzgado: "cualquier texto libre");

        Assert.Null(error);
    }

    [Fact]
    public async Task SedeYJuzgadoValidosYCoherentes_NoDaError()
    {
        using var context = CrearContextoEnMemoria(nameof(SedeYJuzgadoValidosYCoherentes_NoDaError));
        await SembrarCatalogoAsync(context);

        var error = await CatalogoUbicacionService.ValidarSedeYJuzgadoAsync(context, "Hermosillo", "1ro Civil Hermosillo");

        Assert.Null(error);
    }

    [Fact]
    public async Task SedeQueNoExisteEnElCatalogo_DaError()
    {
        using var context = CrearContextoEnMemoria(nameof(SedeQueNoExisteEnElCatalogo_DaError));
        await SembrarCatalogoAsync(context);

        var error = await CatalogoUbicacionService.ValidarSedeYJuzgadoAsync(context, "Ciudad Inventada", "1ro Civil Hermosillo");

        Assert.NotNull(error);
    }

    [Fact]
    public async Task JuzgadoDeOtraSede_DaError()
    {
        // Sede válida (Hermosillo), pero el Juzgado elegido pertenece a Nogales
        // -- exactamente el caso que DJ-87 quiere impedir.
        using var context = CrearContextoEnMemoria(nameof(JuzgadoDeOtraSede_DaError));
        await SembrarCatalogoAsync(context);

        var error = await CatalogoUbicacionService.ValidarSedeYJuzgadoAsync(context, "Hermosillo", "Juzgado 1ro Civil Nogales");

        Assert.NotNull(error);
    }

    [Fact]
    public async Task SedeValidaSinJuzgadoTodavia_NoDaError()
    {
        // El litigante eligió Sede pero aún no Juzgado (paso intermedio del
        // formulario) -- no debe bloquearse antes de tiempo.
        using var context = CrearContextoEnMemoria(nameof(SedeValidaSinJuzgadoTodavia_NoDaError));
        await SembrarCatalogoAsync(context);

        var error = await CatalogoUbicacionService.ValidarSedeYJuzgadoAsync(context, "Hermosillo", juzgado: null);

        Assert.Null(error);
    }
}
