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

// DJ-141: PATCH /api/acuerdos/{id}/sugerir -- hermano de Promover
// (ver PromoverAcuerdoTests.cs) para cuando el equipo técnico no tiene certeza
// de que un acuerdo oculto sea real. En vez de subirlo a Confianza=Alta
// (confirmado), lo sube a Media (sugerido) y reusa el correo de "posible
// acuerdo, confirma o descarta" que ya existe para cualquier Media.
public class SugerirAcuerdoTests
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
    public async Task Sugerir_DryRunPorDefecto_NoTocaNadaNiNotifica()
    {
        var nombreBD = nameof(Sugerir_DryRunPorDefecto_NoTocaNadaNiNotifica);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        // Sin pasar dryRun explícito -- debe comportarse como dryRun=true.
        var resultado = await controller.Sugerir(acuerdo.Id);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Empty(email.Enviados);
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.True(actualizado!.Oculto);
        Assert.Equal("Baja", actualizado.Confianza);
        Assert.Empty(await context.BitacoraCambios.ToListAsync());
    }

    [Fact]
    public async Task Sugerir_DryRunMuestraADondeNotificariaSinEnviarNada()
    {
        var nombreBD = nameof(Sugerir_DryRunMuestraADondeNotificariaSinEnviarNada);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, litigante, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        var resultado = await controller.Sugerir(acuerdo.Id, dryRun: true);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains(litigante.Email, json);
        Assert.Contains(acuerdo.NumeroExpediente, json);
        Assert.Empty(email.Enviados);
    }

    [Fact]
    public async Task Sugerir_AcuerdoOculto_LoHaceVisibleYSubeAMedia_NoAlta()
    {
        var nombreBD = nameof(Sugerir_AcuerdoOculto_LoHaceVisibleYSubeAMedia_NoAlta);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        var resultado = await controller.Sugerir(acuerdo.Id, dryRun: false);

        Assert.IsType<OkObjectResult>(resultado);
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.False(actualizado!.Oculto);
        Assert.Equal("Media", actualizado.Confianza);
    }

    [Fact]
    public async Task Sugerir_EnviaCorreoDeSugerenciaNoDeCerteza()
    {
        var nombreBD = nameof(Sugerir_EnviaCorreoDeSugerenciaNoDeCerteza);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, litigante, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        await controller.Sugerir(acuerdo.Id, dryRun: false);

        var enviado = Assert.Single(email.Enviados);
        Assert.Equal(litigante.Email, enviado.Email);
        // Mismo criterio que cualquier acuerdo Media (DJ-122): el asunto deja
        // claro que necesita confirmación, no que ya se confirmó.
        Assert.Contains("confirma", enviado.Asunto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requiere confirmación", enviado.Cuerpo, StringComparison.OrdinalIgnoreCase);
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.True(actualizado!.NotificacionEnviada);
    }

    [Fact]
    public async Task Sugerir_ReiniciaFechaDetectado_ParaNoDispararRecordatorioAlDiaSiguiente()
    {
        var nombreBD = nameof(Sugerir_ReiniciaFechaDetectado_ParaNoDispararRecordatorioAlDiaSiguiente);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        // Simula un acuerdo que lleva semanas oculto -- antes del fix, Sugerir
        // dejaba este valor intacto y el servicio de recordatorio lo tomaba como
        // "ya se le avisó hace mucho", disparando el correo de seguimiento al
        // día siguiente aunque la sugerencia acabara de salir.
        acuerdo.FechaDetectado = DateTime.UtcNow.AddDays(-34);
        await context.SaveChangesAsync();
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        await controller.Sugerir(acuerdo.Id, dryRun: false);

        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.True((DateTime.UtcNow - actualizado!.FechaDetectado).TotalMinutes < 1);
        Assert.False(RecordatorioAcuerdosPendientesService.EsElegibleParaRecordatorio(actualizado, DateTime.UtcNow, diasUmbral: 3));
    }

    [Fact]
    public async Task Sugerir_RegistraEnBitacora()
    {
        var nombreBD = nameof(Sugerir_RegistraEnBitacora);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, expediente, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        await controller.Sugerir(acuerdo.Id, dryRun: false);

        var entrada = await context.BitacoraCambios.SingleOrDefaultAsync(b => b.ExpedienteId == expediente.Id);
        Assert.NotNull(entrada);
        Assert.Equal("acuerdo_sugerido", entrada!.Accion);
        Assert.Equal(admin.Id, entrada.UsuarioId);
    }

    [Fact]
    public async Task Sugerir_AcuerdoYaVisible_RegresaBadRequestYNoLoToca()
    {
        var nombreBD = nameof(Sugerir_AcuerdoYaVisible_RegresaBadRequestYNoLoToca);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        acuerdo.Oculto = false;
        acuerdo.Confianza = "Media";
        await context.SaveChangesAsync();
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        var resultado = await controller.Sugerir(acuerdo.Id, dryRun: false);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Empty(email.Enviados);
    }

    [Fact]
    public async Task Sugerir_AcuerdoDescartadoManualmente_RegresaBadRequestYNoLoToca()
    {
        var nombreBD = nameof(Sugerir_AcuerdoDescartadoManualmente_RegresaBadRequestYNoLoToca);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        acuerdo.DescartadoManualmente = true; // un litigante ya dijo "esto no es mío"
        await context.SaveChangesAsync();
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        var resultado = await controller.Sugerir(acuerdo.Id, dryRun: false);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Empty(email.Enviados);
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.True(actualizado!.Oculto);
        Assert.Equal("Baja", actualizado.Confianza);
    }

    [Fact]
    public async Task Sugerir_AcuerdoInexistente_RegresaNotFound()
    {
        var nombreBD = nameof(Sugerir_AcuerdoInexistente_RegresaNotFound);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, _) = await SembrarAcuerdoOcultoAsync(context);
        var email = new FakeEmailService();
        var controller = CrearController(context, admin.Id, email, CrearScraperService(nombreBD, email));

        var resultado = await controller.Sugerir(999999, dryRun: false);

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    [Fact]
    public async Task Sugerir_SiFallaLaNotificacion_ElAcuerdoQuedaSugeridoIgual()
    {
        var nombreBD = nameof(Sugerir_SiFallaLaNotificacion_ElAcuerdoQuedaSugeridoIgual);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (admin, _, _, acuerdo) = await SembrarAcuerdoOcultoAsync(context);
        var emailQueFalla = new EmailServiceQueFalla();
        var controller = CrearController(context, admin.Id, emailQueFalla, CrearScraperService(nombreBD, emailQueFalla));

        // No debe lanzar -- la sugerencia queda aplicada aunque falle la notificación.
        var resultado = await controller.Sugerir(acuerdo.Id, dryRun: false);

        Assert.IsType<OkObjectResult>(resultado);
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.False(actualizado!.Oculto);
        Assert.Equal("Media", actualizado.Confianza);
        Assert.False(actualizado.NotificacionEnviada); // la notificación en sí falló
    }
}
