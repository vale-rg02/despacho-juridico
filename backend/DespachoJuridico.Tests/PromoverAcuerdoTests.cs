using System.Security.Claims;
using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DespachoJuridico.Tests;

// DJ-140: PATCH /api/acuerdos/{id}/promover -- único camino para promover un
// acuerdo que el algoritmo escondió (Oculto=true), para cuando la revisión
// diaria del reporte de auditoría confirma que es un caso real. Distinto de
// Confirmar (solo Confianza=Media, ver ConfirmarAcuerdoTests.cs) y de
// Restaurar (solo si un litigante lo había descartado a mano).
public class PromoverAcuerdoTests
{
    private class EmailServiceQueFalla : IEmailService
    {
        public Task EnviarAsync(string destinatarioEmail, string destinatarioNombre, string asunto, string cuerpoHtml)
            => throw new InvalidOperationException("Fallo simulado al enviar notificación");
    }

    private static AppDbContext CrearContextoEnMemoria(string nombreBD) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreBD).Options);

    private static ScraperAcuerdosService CrearScraperService(string nombreBD, IEmailService? emailService = null)
    {
        var servicios = new ServiceCollection();
        servicios.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(nombreBD));
        servicios.AddSingleton<IEmailService>(emailService ?? new FakeEmailService());
        var proveedor = servicios.BuildServiceProvider();

        return new ScraperAcuerdosService(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ScraperAcuerdosService>.Instance,
            new ConfigurationBuilder().Build());
    }

    private static AcuerdosController CrearController(
        AppDbContext context, int usuarioId, IEmailService emailService, ScraperAcuerdosService scraper)
    {
        var controller = new AcuerdosController(
            context, new AccesoExpedientesService(context), emailService,
            new ConfigurationBuilder().Build(), NullLogger<AcuerdosController>.Instance, scraper);
        var identidad = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
        };
        return controller;
    }

    private static async Task<(Usuario admin, Usuario litigante, Expediente expediente, AcuerdoScrapeado acuerdo)> SembrarAcuerdoOcultoAsync(AppDbContext context)
    {
        var admin = new Usuario { Nombre = "Admin", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        var litigante = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.AddRange(admin, litigante);
        await context.SaveChangesAsync();

        var expediente = new Expediente
        {
            NumeroExpediente = "401/2026", ParteDemandada = "SIN IMPORTANCIA", Juzgado = "2do Civil Hermosillo",
            UsuarioAsignadoId = litigante.Id, CreadoPorId = litigante.Id
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        var acuerdo = new AcuerdoScrapeado
        {
            ExpedienteId = expediente.Id, NumeroExpediente = expediente.NumeroExpediente,
            IdUnidad = 153, NombreJuzgado = "2do Civil Hermosillo",
            Partes = "SIN NOMBRE RECONOCIBLE", Sintesis = "Se admite.",
            FechaAcuerdo = new DateOnly(2026, 10, 16), Confianza = "Baja", Oculto = true
        };
        context.AcuerdosScrapeados.Add(acuerdo);
        await context.SaveChangesAsync();

        return (admin, litigante, expediente, acuerdo);
    }

    [Fact]
    public async Task Promover_DryRunPorDefecto_NoTocaNadaNiNotifica()
    {
        var nombreBD = nameof(Promover_DryRunPorDefecto_NoTocaNadaNiNotifica);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        // Sin pasar dryRun explícito -- debe comportarse como dryRun=true.
        var resultado = await controller.Promover(acuerdo.Id);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Empty(email.Enviados);
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.True(actualizado!.Oculto);
        Assert.Equal("Baja", actualizado.Confianza);
        Assert.Empty(await context.BitacoraCambios.ToListAsync());
    }

    [Fact]
    public async Task Promover_DryRunMuestraADondeNotificariaSinEnviarNada()
    {
        var nombreBD = nameof(Promover_DryRunMuestraADondeNotificariaSinEnviarNada);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, litigante, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        var resultado = await controller.Promover(acuerdo.Id, dryRun: true);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains(litigante.Email, json);
        Assert.Contains(acuerdo.NumeroExpediente, json);
        Assert.Empty(email.Enviados);
    }

    [Fact]
    public async Task Promover_AcuerdoOculto_LoHaceVisibleYSubeAAlta()
    {
        var nombreBD = nameof(Promover_AcuerdoOculto_LoHaceVisibleYSubeAAlta);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        var resultado = await controller.Promover(acuerdo.Id, dryRun: false);

        Assert.IsType<OkObjectResult>(resultado);
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.False(actualizado!.Oculto);
        Assert.Equal("Alta", actualizado.Confianza);
    }

    [Fact]
    public async Task Promover_EnviaNotificacionAlLitiganteYMarcaNotificacionEnviada()
    {
        var nombreBD = nameof(Promover_EnviaNotificacionAlLitiganteYMarcaNotificacionEnviada);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, litigante, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        await controller.Promover(acuerdo.Id, dryRun: false);

        var enviado = Assert.Single(email.Enviados);
        Assert.Equal(litigante.Email, enviado.Email);
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.True(actualizado!.NotificacionEnviada);
    }

    [Fact]
    public async Task Promover_RegistraEnBitacora()
    {
        var nombreBD = nameof(Promover_RegistraEnBitacora);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, expediente, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        await controller.Promover(acuerdo.Id, dryRun: false);

        var entrada = await context.BitacoraCambios.SingleOrDefaultAsync(b => b.ExpedienteId == expediente.Id);
        Assert.NotNull(entrada);
        Assert.Equal("acuerdo_promovido", entrada!.Accion);
        Assert.Equal(admin.Id, entrada.UsuarioId);
    }

    [Fact]
    public async Task Promover_AcuerdoYaVisible_RegresaBadRequestYNoLoToca()
    {
        var nombreBD = nameof(Promover_AcuerdoYaVisible_RegresaBadRequestYNoLoToca);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        acuerdo.Oculto = false;
        acuerdo.Confianza = "Media";
        await context.SaveChangesAsync();
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        var resultado = await controller.Promover(acuerdo.Id, dryRun: false);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Empty(email.Enviados);
        Assert.Equal("Media", (await context.AcuerdosScrapeados.FindAsync(acuerdo.Id))!.Confianza);
    }

    [Fact]
    public async Task Promover_AcuerdoDescartadoManualmente_RegresaBadRequestYNoLoToca()
    {
        var nombreBD = nameof(Promover_AcuerdoDescartadoManualmente_RegresaBadRequestYNoLoToca);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        acuerdo.DescartadoManualmente = true; // un litigante ya dijo "esto no es mío"
        await context.SaveChangesAsync();
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        var resultado = await controller.Promover(acuerdo.Id, dryRun: false);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Empty(email.Enviados);
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.True(actualizado!.Oculto);
        Assert.Equal("Baja", actualizado.Confianza);
    }

    [Fact]
    public async Task Promover_AcuerdoInexistente_RegresaNotFound()
    {
        var nombreBD = nameof(Promover_AcuerdoInexistente_RegresaNotFound);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, _) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        var resultado = await controller.Promover(999999, dryRun: false);

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    [Fact]
    public async Task Promover_NotificarFalse_LoHaceVisibleSinMandarCorreo()
    {
        var nombreBD = nameof(Promover_NotificarFalse_LoHaceVisibleSinMandarCorreo);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        var resultado = await controller.Promover(acuerdo.Id, dryRun: false, notificar: false);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Empty(email.Enviados);
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.False(actualizado!.Oculto);
        Assert.Equal("Alta", actualizado.Confianza);
        Assert.False(actualizado.NotificacionEnviada);
    }

    [Fact]
    public async Task Promover_NotificarFalse_DryRunIndicaQueNoSeEnviaraCorreo()
    {
        var nombreBD = nameof(Promover_NotificarFalse_DryRunIndicaQueNoSeEnviaraCorreo);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        var resultado = await controller.Promover(acuerdo.Id, dryRun: true, notificar: false);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("sin enviar correo", json);
    }

    [Fact]
    public async Task Promover_NotificarFalse_RegistraEnBitacoraQueNoSeNotifico()
    {
        var nombreBD = nameof(Promover_NotificarFalse_RegistraEnBitacoraQueNoSeNotifico);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, expediente, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        await controller.Promover(acuerdo.Id, dryRun: false, notificar: false);

        var entrada = await context.BitacoraCambios.SingleOrDefaultAsync(b => b.ExpedienteId == expediente.Id);
        Assert.NotNull(entrada);
        Assert.Contains("sin notificar", entrada!.Detalle);
    }

    [Fact]
    public async Task Promover_SiFallaLaNotificacion_ElAcuerdoQuedaPromovidoIgual()
    {
        var nombreBD = nameof(Promover_SiFallaLaNotificacion_ElAcuerdoQuedaPromovidoIgual);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var emailQueFalla = new EmailServiceQueFalla();
        var controller = CrearController(context, admin.Id, emailQueFalla, CrearScraperService(nombreBD, emailQueFalla));

        // No debe lanzar -- la promoción queda aplicada aunque falle la notificación.
        var resultado = await controller.Promover(acuerdo.Id, dryRun: false);

        Assert.IsType<OkObjectResult>(resultado);
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.False(actualizado!.Oculto);
        Assert.Equal("Alta", actualizado.Confianza);
        Assert.False(actualizado.NotificacionEnviada); // la notificación en sí falló
    }
}
