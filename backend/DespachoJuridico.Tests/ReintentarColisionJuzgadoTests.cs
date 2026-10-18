using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static DespachoJuridico.API.Services.ScraperAcuerdosService;

namespace DespachoJuridico.Tests;

// DJ-140: POST /api/scraper/colisiones-juzgado/{id}/reintentar -- la Cola 2b
// (NUMERO_COINCIDE_JUZGADO_NO) nunca guardó el acuerdo original, así que la
// única forma de recuperarlo es volver a pedírselo a ADISON para la fecha y
// el juzgado correctos, calculados a partir de CorridaId+IdUnidad.
public class ReintentarColisionJuzgadoTests
{
    private const int IdUnidadDePrueba = 153; // "2do Civil Hermosillo"

    private class ScraperDePrueba : ScraperAcuerdosService
    {
        private readonly Dictionary<int, ResultadoScrapeoJuzgado> _respuestas;

        public ScraperDePrueba(
            IServiceScopeFactory scopeFactory, IConfiguration config,
            Dictionary<int, ResultadoScrapeoJuzgado> respuestas)
            : base(scopeFactory, NullLogger<ScraperAcuerdosService>.Instance, config)
        {
            _respuestas = respuestas;
        }

        internal override Task<ResultadoScrapeoJuzgado> ScrapearJuzgadoAsync(int idUnidad, string nombreJuzgado, DateOnly fecha)
            => Task.FromResult(_respuestas.TryGetValue(idUnidad, out var r) ? r : new ResultadoScrapeoJuzgado(new(), true, null));
    }

    private static (AppDbContext Context, ScraperController Controller) CrearEscenario(
        string nombreBD, Dictionary<int, ResultadoScrapeoJuzgado> respuestas, ILogger<ScraperController>? logger = null)
    {
        var servicios = new ServiceCollection();
        servicios.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(nombreBD));
        servicios.AddScoped<IEmailService, FakeEmailService>();
        var proveedor = servicios.BuildServiceProvider();

        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreBD).Options);
        var config = new ConfigurationBuilder().Build();
        var scraper = new ScraperDePrueba(proveedor.GetRequiredService<IServiceScopeFactory>(), config, respuestas);

        var controller = new ScraperController(scraper, context, null!, null!, logger ?? NullLogger<ScraperController>.Instance);
        var identidad = new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "1") });
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identidad) }
        };
        return (context, controller);
    }

    private static BitacoraDescarteItemizado NuevoDescarte(Guid corridaId, string razon = "NUMERO_COINCIDE_JUZGADO_NO") => new()
    {
        CorridaId = corridaId, FechaHoraCorrida = DateTime.UtcNow, IdUnidad = IdUnidadDePrueba,
        NombreJuzgado = "2do Civil Hermosillo", NumeroExpediente = "401/2026", Razon = razon
    };

    private static BitacoraEvaluacionScraper NuevaCorrida(Guid corridaId, DateOnly fechaConsultada) => new()
    {
        CorridaId = corridaId, FechaHoraCorrida = DateTime.UtcNow, FechaConsultada = fechaConsultada,
        IdUnidad = IdUnidadDePrueba, NombreJuzgado = "2do Civil Hermosillo", Origen = "Automatica", ConsultaExitosa = true
    };

    // Captura los mensajes de log para confirmar que reintentar deja constancia
    // de quién lo pidió y cuándo -- a diferencia de Promover (BitacoraCambios),
    // esto no tiene un único expediente dueño, así que queda en el log de
    // aplicación en vez de una tabla.
    private class LoggerDePrueba : ILogger<ScraperController>
    {
        public List<string> Mensajes { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Mensajes.Add(formatter(state, exception));
    }

    [Fact]
    public async Task DejaConstanciaDeQuienYCuandoEnElLog()
    {
        var nombreBD = nameof(DejaConstanciaDeQuienYCuandoEnElLog);
        var respuestas = new Dictionary<int, ResultadoScrapeoJuzgado> { [IdUnidadDePrueba] = new(new(), true, null) };
        var loggerDePrueba = new LoggerDePrueba();
        var (context, controller) = CrearEscenario(nombreBD, respuestas, loggerDePrueba);

        var corridaId = Guid.NewGuid();
        context.BitacoraEvaluacionScraper.Add(NuevaCorrida(corridaId, new DateOnly(2026, 9, 10)));
        var descarte = NuevoDescarte(corridaId);
        context.BitacoraDescarteItemizado.Add(descarte);
        await context.SaveChangesAsync();

        await controller.ReintentarColisionJuzgado(descarte.Id, dryRun: true);

        var mensaje = Assert.Single(loggerDePrueba.Mensajes);
        Assert.Contains(descarte.Id.ToString(), mensaje);
        Assert.Contains("usuario 1", mensaje); // claim NameIdentifier="1" del arnés de prueba
    }

    [Fact]
    public async Task CalculaLaFechaCorrectaYReintentaEseJuzgado()
    {
        var nombreBD = nameof(CalculaLaFechaCorrectaYReintentaEseJuzgado);
        var respuestas = new Dictionary<int, ResultadoScrapeoJuzgado> { [IdUnidadDePrueba] = new(new(), true, null) };
        var (context, controller) = CrearEscenario(nombreBD, respuestas);

        var corridaId = Guid.NewGuid();
        var fechaOriginal = new DateOnly(2026, 9, 10);
        context.BitacoraEvaluacionScraper.Add(NuevaCorrida(corridaId, fechaOriginal));
        var descarte = NuevoDescarte(corridaId);
        context.BitacoraDescarteItemizado.Add(descarte);
        await context.SaveChangesAsync();

        var resultado = await controller.ReintentarColisionJuzgado(descarte.Id, dryRun: true);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<API.DTOs.ResultadoScrapingResponse>(ok.Value);
        Assert.Equal(fechaOriginal, respuesta.Fecha);
    }

    [Fact]
    public async Task RazonDistinta_RegresaBadRequest()
    {
        var nombreBD = nameof(RazonDistinta_RegresaBadRequest);
        var respuestas = new Dictionary<int, ResultadoScrapeoJuzgado> { [IdUnidadDePrueba] = new(new(), true, null) };
        var (context, controller) = CrearEscenario(nombreBD, respuestas);

        var corridaId = Guid.NewGuid();
        context.BitacoraEvaluacionScraper.Add(NuevaCorrida(corridaId, new DateOnly(2026, 9, 10)));
        var descarte = NuevoDescarte(corridaId, razon: "EXPEDIENTE_CERRADO");
        context.BitacoraDescarteItemizado.Add(descarte);
        await context.SaveChangesAsync();

        var resultado = await controller.ReintentarColisionJuzgado(descarte.Id, dryRun: true);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task DescarteInexistente_RegresaNotFound()
    {
        var nombreBD = nameof(DescarteInexistente_RegresaNotFound);
        var respuestas = new Dictionary<int, ResultadoScrapeoJuzgado> { [IdUnidadDePrueba] = new(new(), true, null) };
        var (_, controller) = CrearEscenario(nombreBD, respuestas);

        var resultado = await controller.ReintentarColisionJuzgado(999999, dryRun: true);

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    [Fact]
    public async Task SinCorridaOriginalParaCalcularLaFecha_RegresaBadRequest()
    {
        var nombreBD = nameof(SinCorridaOriginalParaCalcularLaFecha_RegresaBadRequest);
        var respuestas = new Dictionary<int, ResultadoScrapeoJuzgado> { [IdUnidadDePrueba] = new(new(), true, null) };
        var (context, controller) = CrearEscenario(nombreBD, respuestas);

        // Descarte huérfano -- ninguna BitacoraEvaluacionScraper comparte su CorridaId+IdUnidad.
        var descarte = NuevoDescarte(Guid.NewGuid());
        context.BitacoraDescarteItemizado.Add(descarte);
        await context.SaveChangesAsync();

        var resultado = await controller.ReintentarColisionJuzgado(descarte.Id, dryRun: true);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task ConDryRunFalseYCausaYaCorregida_SeGuardaYQuedaManualForzada()
    {
        var nombreBD = nameof(ConDryRunFalseYCausaYaCorregida_SeGuardaYQuedaManualForzada);

        // Simula que ADISON ahora sí trae el acuerdo correctamente (la causa
        // real ya se corrigió -- ej. el juzgado del expediente ya se arregló).
        var acuerdos = new List<(string, string, string, DateOnly, string?)>
        {
            ("401/2026", "SE ADMITE DEMANDA.- JUAN PEREZ LOPEZ", "Se admite.", new DateOnly(2026, 9, 10), "Exp.")
        };
        var respuestas = new Dictionary<int, ResultadoScrapeoJuzgado> { [IdUnidadDePrueba] = new(acuerdos, true, null) };
        var (context, controller) = CrearEscenario(nombreBD, respuestas);

        context.Expedientes.Add(new Expediente
        {
            NumeroExpediente = "401/2026", ParteDemandada = "JUAN PEREZ LOPEZ", Juzgado = "2do Civil Hermosillo",
            TipoJuicio = "Civil", CreadoPorId = 1
        });
        var corridaId = Guid.NewGuid();
        context.BitacoraEvaluacionScraper.Add(NuevaCorrida(corridaId, new DateOnly(2026, 9, 10)));
        var descarte = NuevoDescarte(corridaId);
        context.BitacoraDescarteItemizado.Add(descarte);
        await context.SaveChangesAsync();

        var resultado = await controller.ReintentarColisionJuzgado(descarte.Id, dryRun: false);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<API.DTOs.ResultadoScrapingResponse>(ok.Value);
        var fila = Assert.Single(respuesta.Embudo);
        Assert.Equal(1, fila.GuardadoVisibleAlta);
        // No es el ciclo real -- es un reintento puntual, debe quedar distinguible.
        Assert.Equal("ManualForzada", fila.Origen);
        Assert.Equal(1, await context.AcuerdosScrapeados.CountAsync());
    }
}
