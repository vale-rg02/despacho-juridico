using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-122: GET /api/juzgados ahora acepta un ?materia= opcional. Un juzgado
// aparece si su Materias (comma-separado) contiene la materia pedida, o si es
// EsMixto (atiende cualquier materia -- típico del único juzgado de un
// municipio chico). Si ninguno de la sede coincide, se regresa la sede
// completa sin filtrar en vez de dejar el combobox vacío (ver
// JuzgadosController.GetAll).
public class JuzgadosFiltradoPorMateriaTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    private static async Task<SedeCatalogo> SembrarHermosilloAsync(AppDbContext context)
    {
        var sede = new SedeCatalogo { Nombre = "Hermosillo" };
        context.SedesCatalogo.Add(sede);
        await context.SaveChangesAsync();

        context.JuzgadosCatalogo.AddRange(
            new JuzgadoCatalogo { Nombre = "1ro Civil Hermosillo", SedeId = sede.Id, Materias = "Civil" },
            new JuzgadoCatalogo { Nombre = "1ro Familiar Hermosillo", SedeId = sede.Id, Materias = "Familiar" },
            new JuzgadoCatalogo { Nombre = "Civil/Familiar Especializado Hermosillo", SedeId = sede.Id, Materias = "Civil,Familiar" },
            new JuzgadoCatalogo { Nombre = "1ro Penal Hermosillo", SedeId = sede.Id, Materias = null }
        );
        await context.SaveChangesAsync();
        return sede;
    }

    private static List<JuzgadoCatalogoResponse> ValorDe(IActionResult resultado)
        => Assert.IsAssignableFrom<IEnumerable<JuzgadoCatalogoResponse>>(Assert.IsType<OkObjectResult>(resultado).Value).ToList();

    [Fact]
    public async Task FiltraPorMateria_SoloRegresaJuzgadosQueLaAtienden()
    {
        using var context = CrearContextoEnMemoria(nameof(FiltraPorMateria_SoloRegresaJuzgadosQueLaAtienden));
        var sede = await SembrarHermosilloAsync(context);
        var controller = new JuzgadosController(context);

        var resultado = ValorDe(await controller.GetAll(sede.Id, "Familiar"));

        Assert.Equal(
            new[] { "1ro Familiar Hermosillo", "Civil/Familiar Especializado Hermosillo" },
            resultado.Select(j => j.Nombre).OrderBy(n => n));
    }

    [Fact]
    public async Task JuzgadoMixto_SiempreApareceSinImportarLaMateria()
    {
        using var context = CrearContextoEnMemoria(nameof(JuzgadoMixto_SiempreApareceSinImportarLaMateria));
        var sede = new SedeCatalogo { Nombre = "Álamos" };
        context.SedesCatalogo.Add(sede);
        await context.SaveChangesAsync();
        context.JuzgadosCatalogo.Add(new JuzgadoCatalogo { Nombre = "Juzgado Mixto Álamos", SedeId = sede.Id, EsMixto = true });
        await context.SaveChangesAsync();
        var controller = new JuzgadosController(context);

        var resultado = ValorDe(await controller.GetAll(sede.Id, "Arrendamiento"));

        Assert.Single(resultado);
        Assert.Equal("Juzgado Mixto Álamos", resultado[0].Nombre);
    }

    [Fact]
    public async Task SinNingunJuzgadoQueCoincida_RegresaLaSedeCompletaSinFiltrar()
    {
        // Caso real: Guaymas no tiene juzgado Mercantil ni Mixto -- no se deja
        // el combobox vacío, se cae de vuelta a listar todos los de la sede.
        using var context = CrearContextoEnMemoria(nameof(SinNingunJuzgadoQueCoincida_RegresaLaSedeCompletaSinFiltrar));
        var sede = await SembrarHermosilloAsync(context);
        var controller = new JuzgadosController(context);

        var resultado = ValorDe(await controller.GetAll(sede.Id, "Mercantil"));

        Assert.Equal(4, resultado.Count);
    }

    [Fact]
    public async Task SinMateriaEnLaPeticion_RegresaTodosLosJuzgadosDeLaSede_CompatibilidadHaciaAtras()
    {
        using var context = CrearContextoEnMemoria(nameof(SinMateriaEnLaPeticion_RegresaTodosLosJuzgadosDeLaSede_CompatibilidadHaciaAtras));
        var sede = await SembrarHermosilloAsync(context);
        var controller = new JuzgadosController(context);

        var resultado = ValorDe(await controller.GetAll(sede.Id, materia: null));

        Assert.Equal(4, resultado.Count);
    }
}
