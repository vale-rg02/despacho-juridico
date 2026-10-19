using System.Security.Claims;
using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-137: PATCH/POST /api/scraper/frases-genericas/revisiones -- registra que una
// persona ya revisó un candidato a frase genérica (DJ-133), "Aprobado" o
// "Descartado", para que CalcularCandidatos pueda excluirlo de ahí en adelante
// (ver FrasesGenericasCandidatasServiceTests.cs para la lógica de exclusión en
// sí). Todas las cadenas de prueba aquí son inventadas -- nunca texto real de un
// candidato real del despacho.
public class ValidarRevisionCandidatoTests
{
    private static RevisionCandidatoRequest Valido() => new() { Candidato = "FRASE INVENTADA", Decision = "Descartado" };

    [Fact]
    public void RequestValido_NoDaError() => Assert.Null(ScraperController.ValidarRevisionCandidato(Valido()));

    [Theory]
    [InlineData("")]
    [InlineData("Aprobada")] // no "Aprobado"
    [InlineData("descartado")] // minúsculas no cuenta
    public void DecisionInvalida_DaError(string decision)
    {
        var req = Valido();
        req.Decision = decision;
        Assert.NotNull(ScraperController.ValidarRevisionCandidato(req));
    }

    [Fact]
    public void CandidatoVacio_DaError()
    {
        var req = Valido();
        req.Candidato = "";
        Assert.NotNull(ScraperController.ValidarRevisionCandidato(req));
    }

    [Fact]
    public void CandidatoDemasiadoLargo_DaError()
    {
        var req = Valido();
        req.Candidato = new string('a', 301);
        Assert.NotNull(ScraperController.ValidarRevisionCandidato(req));
    }

    [Fact]
    public void MotivoDemasiadoLargo_DaError()
    {
        var req = Valido();
        req.Motivo = new string('a', 201);
        Assert.NotNull(ScraperController.ValidarRevisionCandidato(req));
    }

    [Fact]
    public void SinMotivo_NoDaError()
    {
        var req = Valido();
        req.Motivo = null;
        Assert.Null(ScraperController.ValidarRevisionCandidato(req));
    }
}

public class RevisionCandidatoFraseGenericaControllerTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreBD).Options);

    private static ScraperController CrearControllerComoAdmin(AppDbContext context, int usuarioId)
    {
        var controller = new ScraperController(null!, context, null!, null!, null!);
        var identidad = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
        };
        return controller;
    }

    private static async Task<Usuario> SembrarAdminAsync(AppDbContext context)
    {
        var admin = new Usuario { Nombre = "Admin", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.Add(admin);
        await context.SaveChangesAsync();
        return admin;
    }

    [Fact]
    public async Task Registrar_CandidatoDescartado_SeGuardaNormalizado()
    {
        var nombreBD = nameof(Registrar_CandidatoDescartado_SeGuardaNormalizado);
        using var context = CrearContextoEnMemoria(nombreBD);
        var admin = await SembrarAdminAsync(context);
        var controller = CrearControllerComoAdmin(context, admin.Id);

        var resultado = await controller.RegistrarRevisionCandidato(new RevisionCandidatoRequest
        {
            Candidato = "  Frase Inventada, Con Acentuación.  ",
            Decision = "Descartado",
            Motivo = "Nombre real, coincidió por casualidad"
        });

        Assert.IsType<OkObjectResult>(resultado);
        var fila = await context.RevisionesCandidatoFraseGenerica.SingleAsync();
        Assert.Equal("FRASE INVENTADA, CON ACENTUACION", fila.CandidatoNormalizado);
        Assert.Equal("Descartado", fila.Decision);
        Assert.Equal(admin.Id, fila.RevisadoPorId);
    }

    [Fact]
    public async Task Registrar_CandidatoYaRegistrado_RegresaBadRequestYNoDuplica()
    {
        var nombreBD = nameof(Registrar_CandidatoYaRegistrado_RegresaBadRequestYNoDuplica);
        using var context = CrearContextoEnMemoria(nombreBD);
        var admin = await SembrarAdminAsync(context);
        var controller = CrearControllerComoAdmin(context, admin.Id);
        await controller.RegistrarRevisionCandidato(new RevisionCandidatoRequest { Candidato = "FRASE REPETIDA INVENTADA", Decision = "Descartado" });

        var resultado = await controller.RegistrarRevisionCandidato(new RevisionCandidatoRequest { Candidato = "frase repetida inventada", Decision = "Aprobado" });

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(1, await context.RevisionesCandidatoFraseGenerica.CountAsync());
    }

    [Fact]
    public async Task Registrar_DescartarAlgoYaEnFrasesGenericasConocidas_RegresaBadRequest()
    {
        var nombreBD = nameof(Registrar_DescartarAlgoYaEnFrasesGenericasConocidas_RegresaBadRequest);
        using var context = CrearContextoEnMemoria(nombreBD);
        var admin = await SembrarAdminAsync(context);
        var controller = CrearControllerComoAdmin(context, admin.Id);
        // Una frase real ya conocida y aprobada en código (no inventada -- es la
        // lista real de ScraperAcuerdosService.FrasesGenericasConocidas).
        var fraseYaAprobada = DespachoJuridico.API.Services.ScraperAcuerdosService.FrasesGenericasConocidas.First();

        var resultado = await controller.RegistrarRevisionCandidato(new RevisionCandidatoRequest { Candidato = fraseYaAprobada, Decision = "Descartado" });

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(0, await context.RevisionesCandidatoFraseGenerica.CountAsync());
    }

    [Fact]
    public async Task Registrar_Aprobado_SeGuardaComoRastroSinTocarFrasesGenericasConocidas()
    {
        var nombreBD = nameof(Registrar_Aprobado_SeGuardaComoRastroSinTocarFrasesGenericasConocidas);
        using var context = CrearContextoEnMemoria(nombreBD);
        var admin = await SembrarAdminAsync(context);
        var controller = CrearControllerComoAdmin(context, admin.Id);
        var cantidadOriginal = DespachoJuridico.API.Services.ScraperAcuerdosService.FrasesGenericasConocidas.Length;

        var resultado = await controller.RegistrarRevisionCandidato(new RevisionCandidatoRequest { Candidato = "FRASE NUEVA INVENTADA", Decision = "Aprobado" });

        Assert.IsType<OkObjectResult>(resultado);
        var fila = await context.RevisionesCandidatoFraseGenerica.SingleAsync();
        Assert.Equal("Aprobado", fila.Decision);
        // El registro es solo rastro -- la lista real en código nunca cambia por esto.
        Assert.Equal(cantidadOriginal, DespachoJuridico.API.Services.ScraperAcuerdosService.FrasesGenericasConocidas.Length);
    }

    [Fact]
    public async Task Listar_RegresaMetadatosSinExponerElCandidato()
    {
        var nombreBD = nameof(Listar_RegresaMetadatosSinExponerElCandidato);
        using var context = CrearContextoEnMemoria(nombreBD);
        var admin = await SembrarAdminAsync(context);
        var controller = CrearControllerComoAdmin(context, admin.Id);
        await controller.RegistrarRevisionCandidato(new RevisionCandidatoRequest { Candidato = "FRASE LISTADA INVENTADA", Decision = "Descartado" });

        var resultado = await controller.ListarRevisionesCandidato();

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var lista = Assert.IsType<List<RevisionCandidatoResponse>>(ok.Value);
        var fila = Assert.Single(lista);
        Assert.Equal("Descartado", fila.Decision);
        Assert.Equal(admin.Nombre, fila.RevisadoPorNombre);
        // RevisionCandidatoResponse no tiene ningún campo de texto del candidato --
        // verificado por tipo, no hace falta buscar el texto en la respuesta.
    }
}
