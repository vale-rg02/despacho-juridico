using System.Security.Claims;
using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.Tests;

// DJ-122: PATCH /api/acuerdos/{id}/confirmar — el litigante confirma que un
// acuerdo "Media" (sugerido por banco, o por número duplicado) sí le pertenece,
// subiéndolo a Alta. InMemory por el mismo motivo que DescartarAcuerdoTests.
public class ConfirmarAcuerdoTests
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
        var controller = new AcuerdosController(context, new AccesoExpedientesService(context));
        var identidad = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
        };
        return controller;
    }

    private static async Task<(Usuario litigante, Expediente expediente, AcuerdoScrapeado acuerdo)> SembrarAcuerdoMediaAsync(AppDbContext context)
    {
        var litigante = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.Add(litigante);
        await context.SaveChangesAsync();

        var expediente = new Expediente
        {
            NumeroExpediente = "401/2026",
            ParteDemandada = "Adrián Omar Tiznado Valenzuela y otra",
            Juzgado = "PRIMERO CIVIL DE NOGALES",
            UsuarioAsignadoId = litigante.Id,
            CreadoPorId = litigante.Id
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        var acuerdo = new AcuerdoScrapeado
        {
            ExpedienteId = expediente.Id,
            NumeroExpediente = expediente.NumeroExpediente,
            IdUnidad = 195,
            NombreJuzgado = "Juzgado 1ro Civil Nogales",
            Partes = "ESPECIAL HIPOTECARIO - OTROS.- BBVA MEXICO SA INSTITUCION BANCA MULTIPLE",
            Sintesis = "232.- Radica demanda, ordena expedición de cédula hipotecaria.",
            FechaAcuerdo = new DateOnly(2026, 8, 6),
            Confianza = "Media",
            Oculto = false
        };
        context.AcuerdosScrapeados.Add(acuerdo);
        await context.SaveChangesAsync();

        return (litigante, expediente, acuerdo);
    }

    [Fact]
    public async Task Confirmar_AcuerdoMedia_SubeAAlta()
    {
        using var context = CrearContextoEnMemoria(nameof(Confirmar_AcuerdoMedia_SubeAAlta));
        var (litigante, _, acuerdo) = await SembrarAcuerdoMediaAsync(context);
        var controller = CrearControllerComoUsuario(context, litigante.Id);

        var resultado = await controller.Confirmar(acuerdo.Id);

        Assert.IsType<OkObjectResult>(resultado);
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.Equal("Alta", actualizado!.Confianza);
        Assert.False(actualizado.Oculto);
    }

    [Fact]
    public async Task Confirmar_RegistraQuienYCuandoEnBitacora()
    {
        using var context = CrearContextoEnMemoria(nameof(Confirmar_RegistraQuienYCuandoEnBitacora));
        var (litigante, expediente, acuerdo) = await SembrarAcuerdoMediaAsync(context);
        var controller = CrearControllerComoUsuario(context, litigante.Id);

        await controller.Confirmar(acuerdo.Id);

        var entrada = await context.BitacoraCambios.SingleOrDefaultAsync(b => b.ExpedienteId == expediente.Id);
        Assert.NotNull(entrada);
        Assert.Equal("acuerdo_confirmado", entrada!.Accion);
        Assert.Equal(litigante.Id, entrada.UsuarioId);
    }

    [Theory]
    [InlineData("Alta")]
    [InlineData("Baja")]
    [InlineData(null)]
    public async Task Confirmar_AcuerdoQueNoEsMedia_RegresaBadRequest(string? confianzaActual)
    {
        using var context = CrearContextoEnMemoria($"{nameof(Confirmar_AcuerdoQueNoEsMedia_RegresaBadRequest)}_{confianzaActual ?? "null"}");
        var (litigante, _, acuerdo) = await SembrarAcuerdoMediaAsync(context);
        acuerdo.Confianza = confianzaActual;
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, litigante.Id);

        var resultado = await controller.Confirmar(acuerdo.Id);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(confianzaActual, (await context.AcuerdosScrapeados.FindAsync(acuerdo.Id))!.Confianza);
    }

    [Fact]
    public async Task Confirmar_UsuarioSinAcceso_RegresaNotFound()
    {
        using var context = CrearContextoEnMemoria(nameof(Confirmar_UsuarioSinAcceso_RegresaNotFound));
        var (_, _, acuerdo) = await SembrarAcuerdoMediaAsync(context);

        var soporte = new Usuario { Nombre = "dev1", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x", EsCuentaSoporte = true };
        context.Usuarios.Add(soporte);
        await context.SaveChangesAsync();

        var controller = CrearControllerComoUsuario(context, soporte.Id);
        var resultado = await controller.Confirmar(acuerdo.Id);

        Assert.IsType<NotFoundObjectResult>(resultado);
        Assert.Equal("Media", (await context.AcuerdosScrapeados.FindAsync(acuerdo.Id))!.Confianza);
    }

    [Fact]
    public async Task Confirmar_AcuerdoInexistente_RegresaNotFound()
    {
        using var context = CrearContextoEnMemoria(nameof(Confirmar_AcuerdoInexistente_RegresaNotFound));
        var (litigante, _, _) = await SembrarAcuerdoMediaAsync(context);
        var controller = CrearControllerComoUsuario(context, litigante.Id);

        var resultado = await controller.Confirmar(999999);

        Assert.IsType<NotFoundObjectResult>(resultado);
    }
}
