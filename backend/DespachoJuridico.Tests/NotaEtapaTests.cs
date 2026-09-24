using System.Security.Claims;
using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DespachoJuridico.Tests;

// Historial de notas por etapa: agregar una nota nueva ya no sobreescribe el
// campo plano de antes (HistorialEtapa.NotasLegado, dejado como artefacto
// inerte) -- ahora vive en su propia tabla (NotaEtapa), acumulable en el
// tiempo. InMemory porque los endpoints corren consultas reales contra
// HistorialEtapas/NotasEtapa, no solo lógica interna.
public class NotaEtapaTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    private static ExpedientesController CrearControllerComoUsuario(AppDbContext context, int usuarioId)
    {
        var controller = new ExpedientesController(
            context, new CalculadorFechasService(), new FakeEmailService(),
            NullLogger<ExpedientesController>.Instance, new AccesoExpedientesService(context));
        var identidad = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
        };
        return controller;
    }

    private static async Task<(Usuario titular, Expediente expediente, EtapaCatalogo catalogo, HistorialEtapa etapa)> SembrarEtapaAsync(AppDbContext context)
    {
        var titular = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.Add(titular);
        await context.SaveChangesAsync();

        var expediente = new Expediente
        {
            NumeroExpediente = "401/2026", ParteDemandada = "Juan Pérez", UsuarioAsignadoId = titular.Id, CreadoPorId = titular.Id
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        var catalogo = new EtapaCatalogo { Nombre = "Contestación", TipoJuicio = "Hipotecario", Orden = 1 };
        context.EtapasCatalogo.Add(catalogo);
        await context.SaveChangesAsync();

        var etapa = new HistorialEtapa
        {
            ExpedienteId = expediente.Id, EtapaCatalogoId = catalogo.Id,
            FechaInicio = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), RegistradoPorId = titular.Id
        };
        context.HistorialEtapas.Add(etapa);
        await context.SaveChangesAsync();

        return (titular, expediente, catalogo, etapa);
    }

    [Fact]
    public async Task AgregarNota_NoBorraLasAnteriores_OrdenCronologico()
    {
        using var context = CrearContextoEnMemoria(nameof(AgregarNota_NoBorraLasAnteriores_OrdenCronologico));
        var (titular, expediente, _, etapa) = await SembrarEtapaAsync(context);
        context.NotasEtapa.Add(new NotaEtapa
        {
            HistorialEtapaId = etapa.Id, Texto = "Primera nota", CreadoPorId = titular.Id,
            CreadoEn = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, titular.Id);

        var resultado = await controller.AgregarNotaEtapa(expediente.Id, etapa.Id, new AgregarNotaEtapaRequest { Texto = "Segunda nota" });

        Assert.IsType<OkObjectResult>(resultado);
        var todas = await context.NotasEtapa.Where(n => n.HistorialEtapaId == etapa.Id).OrderBy(n => n.CreadoEn).ToListAsync();
        Assert.Equal(2, todas.Count);
        Assert.Equal("Primera nota", todas[0].Texto);
        Assert.Equal("Segunda nota", todas[1].Texto);
    }

    [Fact]
    public async Task RegistrarEtapa_ConNotaInicial_CreaLaPrimeraNotaEtapa()
    {
        using var context = CrearContextoEnMemoria(nameof(RegistrarEtapa_ConNotaInicial_CreaLaPrimeraNotaEtapa));
        var titular = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.Add(titular);
        await context.SaveChangesAsync();
        var expediente = new Expediente { NumeroExpediente = "401/2026", ParteDemandada = "Juan Pérez", UsuarioAsignadoId = titular.Id, CreadoPorId = titular.Id };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        var catalogo = new EtapaCatalogo { Nombre = "Demanda", TipoJuicio = "Hipotecario", Orden = 1 };
        context.EtapasCatalogo.Add(catalogo);
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, titular.Id);

        var resultado = await controller.RegistrarEtapa(expediente.Id, new RegistrarEtapaRequest
        {
            EtapaCatalogoId = catalogo.Id, FechaInicio = new DateTime(2026, 9, 24), Notas = "Nota al crear"
        });

        var creado = Assert.IsType<CreatedAtActionResult>(resultado);
        var respuesta = Assert.IsType<EtapaHistorialResponse>(creado.Value);
        Assert.Single(respuesta.Notas);
        Assert.Equal("Nota al crear", respuesta.Notas[0].Texto);

        var enBd = await context.NotasEtapa.SingleAsync();
        Assert.Equal("Nota al crear", enBd.Texto);
    }

    [Fact]
    public async Task EditarEtapa_NoModificaNotasExistentes()
    {
        using var context = CrearContextoEnMemoria(nameof(EditarEtapa_NoModificaNotasExistentes));
        var (titular, expediente, catalogo, etapa) = await SembrarEtapaAsync(context);
        context.NotasEtapa.Add(new NotaEtapa { HistorialEtapaId = etapa.Id, Texto = "Nota original", CreadoPorId = titular.Id });
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, titular.Id);

        var resultado = await controller.EditarEtapa(expediente.Id, etapa.Id, new EditarEtapaRequest
        {
            EtapaCatalogoId = catalogo.Id, FechaInicio = new DateTime(2026, 9, 5)
        });

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<EtapaHistorialResponse>(ok.Value);
        Assert.Single(respuesta.Notas);
        Assert.Equal("Nota original", respuesta.Notas[0].Texto);
    }

    [Fact]
    public async Task AgregarNota_LitiganteSinAcceso_RegresaNotFound()
    {
        using var context = CrearContextoEnMemoria(nameof(AgregarNota_LitiganteSinAcceso_RegresaNotFound));
        var (_, expediente, _, etapa) = await SembrarEtapaAsync(context);
        var soporte = new Usuario { Nombre = "dev1", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x", EsCuentaSoporte = true };
        context.Usuarios.Add(soporte);
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, soporte.Id);

        var resultado = await controller.AgregarNotaEtapa(expediente.Id, etapa.Id, new AgregarNotaEtapaRequest { Texto = "x" });

        Assert.IsType<NotFoundObjectResult>(resultado);
        Assert.Empty(context.NotasEtapa);
    }

    [Fact]
    public async Task MigrarNotasEtapaLegado_CreaLaPrimeraNotaConFechaYAutorCorrectos()
    {
        using var context = CrearContextoEnMemoria(nameof(MigrarNotasEtapaLegado_CreaLaPrimeraNotaConFechaYAutorCorrectos));
        var (_, _, _, etapa) = await SembrarEtapaAsync(context);
        etapa.NotasLegado = "Nota vieja sobreescrita alguna vez";
        await context.SaveChangesAsync();

        await DbSeeder.MigrarNotasEtapaLegadoAsync(context);

        var nota = await context.NotasEtapa.SingleAsync(n => n.HistorialEtapaId == etapa.Id);
        Assert.Equal("Nota vieja sobreescrita alguna vez", nota.Texto);
        Assert.Equal(etapa.FechaInicio, nota.CreadoEn);
        Assert.Equal(etapa.RegistradoPorId, nota.CreadoPorId);
    }

    [Fact]
    public async Task MigrarNotasEtapaLegado_EsIdempotente_NoDuplicaSiYaMigro()
    {
        using var context = CrearContextoEnMemoria(nameof(MigrarNotasEtapaLegado_EsIdempotente_NoDuplicaSiYaMigro));
        var (_, _, _, etapa) = await SembrarEtapaAsync(context);
        etapa.NotasLegado = "Nota vieja";
        await context.SaveChangesAsync();

        await DbSeeder.MigrarNotasEtapaLegadoAsync(context);
        await DbSeeder.MigrarNotasEtapaLegadoAsync(context);

        var todas = await context.NotasEtapa.Where(n => n.HistorialEtapaId == etapa.Id).ToListAsync();
        Assert.Single(todas);
    }

    [Fact]
    public async Task MigrarNotasEtapaLegado_EtapaSinNotaVieja_NoCreaNada()
    {
        using var context = CrearContextoEnMemoria(nameof(MigrarNotasEtapaLegado_EtapaSinNotaVieja_NoCreaNada));
        await SembrarEtapaAsync(context); // NotasLegado queda null

        await DbSeeder.MigrarNotasEtapaLegadoAsync(context);

        Assert.Empty(context.NotasEtapa);
    }
}
