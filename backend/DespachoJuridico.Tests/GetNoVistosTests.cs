using System.Security.Claims;
using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DespachoJuridico.Tests;

// DJ-91 (hallazgo de scoping, no la evaluación en sí): GET /api/acuerdos/no-vistos
// solo consideraba al titular del expediente -- un colaborador (con acceso real
// vía ExpedienteAccesos, ya notificado por correo desde DJ-108) nunca veía el
// punto de "acuerdos nuevos" en la lista de expedientes. Se agrega colaborador
// al criterio, sin excluir expedientes Cerrado (comportamiento previo intacto).
public class GetNoVistosTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    private static AcuerdosController CrearControllerComoUsuario(AppDbContext context, int usuarioId)
    {
        var controller = new AcuerdosController(
            context, new AccesoExpedientesService(context), new FakeEmailService(),
            new ConfigurationBuilder().Build(), NullLogger<AcuerdosController>.Instance);
        var identidad = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
        };
        return controller;
    }

    [Fact]
    public async Task Colaborador_VeAcuerdosNoVistosDeExpedientesDondeNoEsTitular()
    {
        using var context = CrearContextoEnMemoria(nameof(Colaborador_VeAcuerdosNoVistosDeExpedientesDondeNoEsTitular));
        var titular = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        var colaborador = new Usuario { Nombre = "Carlos", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.AddRange(titular, colaborador);
        await context.SaveChangesAsync();

        var expediente = new Expediente
        {
            NumeroExpediente = "401/2026", ParteDemandada = "Juan Pérez", UsuarioAsignadoId = titular.Id, CreadoPorId = titular.Id
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        context.ExpedienteAccesos.Add(new ExpedienteAcceso { ExpedienteId = expediente.Id, UsuarioId = colaborador.Id });
        context.AcuerdosScrapeados.Add(new AcuerdoScrapeado
        {
            ExpedienteId = expediente.Id, NumeroExpediente = expediente.NumeroExpediente,
            Sintesis = "x", FechaAcuerdo = new DateOnly(2026, 9, 25), Visto = false, Oculto = false
        });
        await context.SaveChangesAsync();

        var controller = CrearControllerComoUsuario(context, colaborador.Id);
        var resultado = Assert.IsType<OkObjectResult>(await controller.GetNoVistos());

        var lista = Assert.IsAssignableFrom<System.Collections.IEnumerable>(resultado.Value).Cast<object>().ToList();
        Assert.Single(lista);
    }

    [Fact]
    public async Task LitiganteSinRelacion_NoVeAcuerdosNoVistosDeExpedientesAjenos()
    {
        using var context = CrearContextoEnMemoria(nameof(LitiganteSinRelacion_NoVeAcuerdosNoVistosDeExpedientesAjenos));
        var titular = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        var ajeno = new Usuario { Nombre = "Litigante Ajeno", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.AddRange(titular, ajeno);
        await context.SaveChangesAsync();

        var expediente = new Expediente
        {
            NumeroExpediente = "401/2026", ParteDemandada = "Juan Pérez", UsuarioAsignadoId = titular.Id, CreadoPorId = titular.Id
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        context.AcuerdosScrapeados.Add(new AcuerdoScrapeado
        {
            ExpedienteId = expediente.Id, NumeroExpediente = expediente.NumeroExpediente,
            Sintesis = "x", FechaAcuerdo = new DateOnly(2026, 9, 25), Visto = false, Oculto = false
        });
        await context.SaveChangesAsync();

        var controller = CrearControllerComoUsuario(context, ajeno.Id);
        var resultado = Assert.IsType<OkObjectResult>(await controller.GetNoVistos());

        var lista = Assert.IsAssignableFrom<System.Collections.IEnumerable>(resultado.Value).Cast<object>().ToList();
        Assert.Empty(lista);
    }

    [Fact]
    public async Task Titular_SigueViendoSusPropiosNoVistos_SinRegresion()
    {
        using var context = CrearContextoEnMemoria(nameof(Titular_SigueViendoSusPropiosNoVistos_SinRegresion));
        var titular = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.Add(titular);
        await context.SaveChangesAsync();

        var expediente = new Expediente
        {
            NumeroExpediente = "401/2026", ParteDemandada = "Juan Pérez", UsuarioAsignadoId = titular.Id, CreadoPorId = titular.Id
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        context.AcuerdosScrapeados.Add(new AcuerdoScrapeado
        {
            ExpedienteId = expediente.Id, NumeroExpediente = expediente.NumeroExpediente,
            Sintesis = "x", FechaAcuerdo = new DateOnly(2026, 9, 25), Visto = false, Oculto = false
        });
        await context.SaveChangesAsync();

        var controller = CrearControllerComoUsuario(context, titular.Id);
        var resultado = Assert.IsType<OkObjectResult>(await controller.GetNoVistos());

        var lista = Assert.IsAssignableFrom<System.Collections.IEnumerable>(resultado.Value).Cast<object>().ToList();
        Assert.Single(lista);
    }
}
