using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DespachoJuridico.Tests;

// 26 oct 2026: ReevaluarOcultosAsync ahora acepta "notificar" (mismo patrón que
// Promover) y envuelve el envío de correo en try/catch -- antes, un solo correo
// fallido abortaba la función completa, dejando sin evaluar cualquier candidato
// restante del lote. Estas pruebas ejercitan el método REAL con un
// IServiceProvider real (mismo patrón que
// AcuerdoManualNormal_NuncaApareceEntreCandidatosDeReevaluarOcultos en
// RegistrarAcuerdoManualTests.cs), no solo las piezas.
public class ReevaluarOcultosResilienciaTests
{
    // Lanza siempre al intentar enviar -- simula un fallo transitorio real de
    // Resend sin depender de red.
    private class EmailServiceQueSiempreFalla : IEmailService
    {
        public Task EnviarAsync(string destinatarioEmail, string destinatarioNombre, string asunto, string cuerpoHtml) =>
            throw new Exception("Fallo simulado de Resend");
    }

    private static AppDbContext CrearContextoEnMemoria(string nombreBD) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreBD).Options);

    private static async Task<Expediente> SembrarExpedienteConAcuerdoOcultoAsync(
        AppDbContext context, string numeroExpediente, string parteDemandada, string partesAcuerdo)
    {
        var titular = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.Add(titular);
        await context.SaveChangesAsync();

        var expediente = new Expediente
        {
            NumeroExpediente = numeroExpediente,
            ParteDemandada = parteDemandada,
            UsuarioAsignadoId = titular.Id,
            CreadoPorId = titular.Id
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        context.AcuerdosScrapeados.Add(new AcuerdoScrapeado
        {
            ExpedienteId = expediente.Id,
            NumeroExpediente = numeroExpediente,
            NombreJuzgado = "1ro Civil Hermosillo",
            IdUnidad = 152,
            Partes = partesAcuerdo,
            Sintesis = "x",
            FechaAcuerdo = new DateOnly(2026, 10, 26),
            Oculto = true,
            Confianza = "Baja"
        });
        await context.SaveChangesAsync();

        return expediente;
    }

    private static ScraperAcuerdosService CrearScraperConEmailService(
        string nombreBD, IEmailService emailService, int? topeReevaluacionPorCorrida = null)
    {
        var servicios = new ServiceCollection();
        servicios.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(nombreBD));
        servicios.AddSingleton(emailService);
        var proveedor = servicios.BuildServiceProvider();

        var configBuilder = new ConfigurationBuilder();
        if (topeReevaluacionPorCorrida.HasValue)
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ScraperAcuerdos:TopeReevaluacionPorCorrida"] = topeReevaluacionPorCorrida.Value.ToString()
            });

        return new ScraperAcuerdosService(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ScraperAcuerdosService>.Instance,
            configBuilder.Build());
    }

    [Fact]
    public async Task NotificarFalse_DesocultaSinEnviarCorreo_YMarcaNotificacionOmitida()
    {
        var nombreBD = nameof(NotificarFalse_DesocultaSinEnviarCorreo_YMarcaNotificacionOmitida);
        using var context = CrearContextoEnMemoria(nombreBD);
        await SembrarExpedienteConAcuerdoOcultoAsync(context, "500/2026", "Pedro Ramirez Lopez", "ORAL MERCANTIL.- PEDRO RAMIREZ LOPEZ VS BANCO");

        var fakeEmail = new FakeEmailService();
        var scraper = CrearScraperConEmailService(nombreBD, fakeEmail);

        var resultado = await scraper.ReevaluarOcultosAsync(dryRun: false, notificar: false);

        Assert.Single(resultado.RegistrosDesocultados);
        Assert.Empty(fakeEmail.Enviados);

        // Contexto nuevo -- el "context" de arriba ya trae estas filas en su
        // propio change tracker desde que las sembró, así que reconsultarlo
        // devolvería la instancia vieja en memoria en vez de leer lo que
        // ReevaluarOcultosAsync guardó desde su propio scope/contexto.
        using var contextVerificacion = CrearContextoEnMemoria(nombreBD);
        var acuerdo = await contextVerificacion.AcuerdosScrapeados.SingleAsync();
        Assert.False(acuerdo.Oculto);
        Assert.Equal("Alta", acuerdo.Confianza);
        Assert.False(acuerdo.NotificacionEnviada);
        Assert.True(acuerdo.NotificacionOmitida);
    }

    [Fact]
    public async Task UnCorreoQueFalla_NoAbortaElRestoDelLote()
    {
        var nombreBD = nameof(UnCorreoQueFalla_NoAbortaElRestoDelLote);
        using var context = CrearContextoEnMemoria(nombreBD);
        await SembrarExpedienteConAcuerdoOcultoAsync(context, "501/2026", "Ana Torres Beltran", "ORAL MERCANTIL.- ANA TORRES BELTRAN VS BANCO");
        await SembrarExpedienteConAcuerdoOcultoAsync(context, "502/2026", "Luis Ocampo Vera", "ORAL MERCANTIL.- LUIS OCAMPO VERA VS BANCO");

        var scraper = CrearScraperConEmailService(nombreBD, new EmailServiceQueSiempreFalla());

        // No debe lanzar -- el try/catch alrededor del envío debe absorber el
        // fallo del primero y seguir con el segundo.
        var resultado = await scraper.ReevaluarOcultosAsync(dryRun: false, notificar: true);

        Assert.Equal(2, resultado.RegistrosEvaluados);
        Assert.Equal(2, resultado.RegistrosDesocultados.Count);

        using var contextVerificacion = CrearContextoEnMemoria(nombreBD);
        var acuerdos = await contextVerificacion.AcuerdosScrapeados.OrderBy(a => a.NumeroExpediente).ToListAsync();
        Assert.All(acuerdos, a =>
        {
            Assert.False(a.Oculto);
            Assert.Equal("Alta", a.Confianza);
            // El correo falló para ambos (mismo IEmailService que siempre
            // lanza) -- lo que importa es que AMBOS quedaron procesados
            // (Oculto/Confianza guardados), no solo el primero.
            Assert.False(a.NotificacionEnviada);
        });
    }

    [Fact]
    public async Task TopeExcedido_NoGuardaNiNotificaNada()
    {
        // 27 oct 2026: si dryRun=false pero el total de candidatos supera el
        // tope (acotado aquí a 1 para no tener que sembrar 11 acuerdos), la
        // función debe comportarse como si hubiera sido dryRun=true -- nada se
        // guarda, nada se notifica -- y marcar TopeExcedido para que quien lo
        // llame sepa por qué no se aplicó nada.
        var nombreBD = nameof(TopeExcedido_NoGuardaNiNotificaNada);
        using var context = CrearContextoEnMemoria(nombreBD);
        await SembrarExpedienteConAcuerdoOcultoAsync(context, "503/2026", "Rosa Delia Figueroa", "ORAL MERCANTIL.- ROSA DELIA FIGUEROA VS BANCO");
        await SembrarExpedienteConAcuerdoOcultoAsync(context, "504/2026", "Gilberto Nava Ruiz", "ORAL MERCANTIL.- GILBERTO NAVA RUIZ VS BANCO");

        var fakeEmail = new FakeEmailService();
        var scraper = CrearScraperConEmailService(nombreBD, fakeEmail, topeReevaluacionPorCorrida: 1);

        var resultado = await scraper.ReevaluarOcultosAsync(dryRun: false, notificar: true);

        Assert.True(resultado.TopeExcedido);
        Assert.Equal(1, resultado.Tope);
        Assert.Equal(2, resultado.RegistrosDesocultados.Count); // sí informa qué habría cambiado
        Assert.Empty(fakeEmail.Enviados);

        using var contextVerificacion = CrearContextoEnMemoria(nombreBD);
        var acuerdos = await contextVerificacion.AcuerdosScrapeados.ToListAsync();
        Assert.All(acuerdos, a =>
        {
            Assert.True(a.Oculto);
            Assert.Equal("Baja", a.Confianza);
        });
    }
}
