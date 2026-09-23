using System.Security.Claims;
using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DespachoJuridico.Tests;

// DJ-102: POST /api/scraper/actualizar-mis-expedientes y
// GET /api/scraper/mi-estado-actualizacion. El litigante de estos tests nunca
// tiene expedientes activos con un Juzgado real (o ya tiene EnProgreso/cooldown
// vigente antes de llamar), así que EjecutarScrapingManualAsync -- disparada en
// segundo plano por el controller -- nunca deriva juzgados y nunca le pega a
// ADISON; solo se verifica el comportamiento SÍNCRONO del controller (el que no
// depende de que la tarea de fondo termine).
public class ActualizarMisExpedientesControllerTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    private static ScraperAcuerdosService CrearScraperService(string nombreBD)
    {
        var servicios = new ServiceCollection();
        servicios.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(nombreBD));
        var proveedor = servicios.BuildServiceProvider();

        return new ScraperAcuerdosService(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ScraperAcuerdosService>.Instance,
            new ConfigurationBuilder().Build());
    }

    private static ScraperController CrearControllerComoUsuario(AppDbContext context, string nombreBD, int usuarioId)
    {
        var controller = new ScraperController(CrearScraperService(nombreBD), context);
        var identidad = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
        };
        return controller;
    }

    private static async Task<Usuario> SembrarLitiganteAsync(AppDbContext context)
    {
        var litigante = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.Add(litigante);
        await context.SaveChangesAsync();
        return litigante;
    }

    [Fact]
    public async Task ActualizarMisExpedientes_PrimeraVez_MarcaEnProgresoDeInmediatoYRegresa202()
    {
        var nombreBD = nameof(ActualizarMisExpedientes_PrimeraVez_MarcaEnProgresoDeInmediatoYRegresa202);
        using var context = CrearContextoEnMemoria(nombreBD);
        var litigante = await SembrarLitiganteAsync(context);
        var controller = CrearControllerComoUsuario(context, nombreBD, litigante.Id);

        var resultado = await controller.ActualizarMisExpedientes();

        Assert.IsType<AcceptedResult>(resultado);
        var actualizado = await context.Usuarios.FindAsync(litigante.Id);
        Assert.True(actualizado!.ScraperEnProgreso);
        Assert.NotNull(actualizado.ScraperIniciadoEn);
    }

    [Fact]
    public async Task ActualizarMisExpedientes_MientrasYaEnProgreso_Regresa409()
    {
        var nombreBD = nameof(ActualizarMisExpedientes_MientrasYaEnProgreso_Regresa409);
        using var context = CrearContextoEnMemoria(nombreBD);
        var litigante = await SembrarLitiganteAsync(context);
        litigante.ScraperEnProgreso = true;
        litigante.ScraperIniciadoEn = DateTime.UtcNow;
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, nombreBD, litigante.Id);

        var resultado = await controller.ActualizarMisExpedientes();

        Assert.IsType<ConflictObjectResult>(resultado);
    }

    [Fact]
    public async Task ActualizarMisExpedientes_DentroDelCooldown_Regresa409ConSegundosRestantes()
    {
        var nombreBD = nameof(ActualizarMisExpedientes_DentroDelCooldown_Regresa409ConSegundosRestantes);
        using var context = CrearContextoEnMemoria(nombreBD);
        var litigante = await SembrarLitiganteAsync(context);
        litigante.UltimaConsultaScraperEn = DateTime.UtcNow.AddMinutes(-5); // faltan ~10 min
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, nombreBD, litigante.Id);

        var resultado = await controller.ActualizarMisExpedientes();

        var conflicto = Assert.IsType<ConflictObjectResult>(resultado);
        var cooldownRestante = (int)conflicto.Value!.GetType().GetProperty("cooldownRestanteSegundos")!.GetValue(conflicto.Value)!;
        Assert.InRange(cooldownRestante, 1, 600);
    }

    [Fact]
    public async Task ActualizarMisExpedientes_DespuesDeCooldownVencido_PermiteDeNuevo()
    {
        var nombreBD = nameof(ActualizarMisExpedientes_DespuesDeCooldownVencido_PermiteDeNuevo);
        using var context = CrearContextoEnMemoria(nombreBD);
        var litigante = await SembrarLitiganteAsync(context);
        litigante.UltimaConsultaScraperEn = DateTime.UtcNow.AddMinutes(-16); // ya pasaron los 15 min
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, nombreBD, litigante.Id);

        var resultado = await controller.ActualizarMisExpedientes();

        Assert.IsType<AcceptedResult>(resultado);
    }

    [Fact]
    public async Task MiEstadoActualizacion_CombinaGlobalYManual_RegresaElMasReciente()
    {
        var nombreBD = nameof(MiEstadoActualizacion_CombinaGlobalYManual_RegresaElMasReciente);
        using var context = CrearContextoEnMemoria(nombreBD);
        var litigante = await SembrarLitiganteAsync(context);
        var corridaGlobal = new DateTime(2026, 9, 23, 8, 0, 0, DateTimeKind.Utc);
        var consultaManual = new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc); // más reciente
        litigante.UltimaConsultaScraperEn = consultaManual;
        context.EstadosScraper.Add(new EstadoScraper { Id = 1, UltimaCorridaCompletaEn = corridaGlobal });
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, nombreBD, litigante.Id);

        var resultado = await controller.MiEstadoActualizacion();

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<EstadoActualizacionScraperResponse>(ok.Value);
        Assert.Equal(consultaManual, respuesta.UltimaActualizacionEn);
    }

    [Fact]
    public async Task MiEstadoActualizacion_SoloCorridaGlobalMasReciente_RegresaLaGlobal()
    {
        var nombreBD = nameof(MiEstadoActualizacion_SoloCorridaGlobalMasReciente_RegresaLaGlobal);
        using var context = CrearContextoEnMemoria(nombreBD);
        var litigante = await SembrarLitiganteAsync(context);
        var corridaGlobal = new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);
        var consultaManual = new DateTime(2026, 9, 23, 8, 0, 0, DateTimeKind.Utc); // más vieja
        litigante.UltimaConsultaScraperEn = consultaManual;
        context.EstadosScraper.Add(new EstadoScraper { Id = 1, UltimaCorridaCompletaEn = corridaGlobal });
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, nombreBD, litigante.Id);

        var resultado = await controller.MiEstadoActualizacion();

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<EstadoActualizacionScraperResponse>(ok.Value);
        Assert.Equal(corridaGlobal, respuesta.UltimaActualizacionEn);
    }

    [Fact]
    public async Task MiEstadoActualizacion_NuncaCorrioNinguna_RegresaNull()
    {
        var nombreBD = nameof(MiEstadoActualizacion_NuncaCorrioNinguna_RegresaNull);
        using var context = CrearContextoEnMemoria(nombreBD);
        var litigante = await SembrarLitiganteAsync(context);
        var controller = CrearControllerComoUsuario(context, nombreBD, litigante.Id);

        var resultado = await controller.MiEstadoActualizacion();

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<EstadoActualizacionScraperResponse>(ok.Value);
        Assert.Null(respuesta.UltimaActualizacionEn);
        Assert.True(respuesta.PuedeActualizar);
    }
}
