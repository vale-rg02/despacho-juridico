using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        string nombreBD, Dictionary<int, ResultadoScrapeoJuzgado> respuestas)
    {
        var servicios = new ServiceCollection();
        servicios.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(nombreBD));
        servicios.AddScoped<IEmailService, FakeEmailService>();
        var proveedor = servicios.BuildServiceProvider();

        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreBD).Options);
        var config = new ConfigurationBuilder().Build();
        var scraper = new ScraperDePrueba(proveedor.GetRequiredService<IServiceScopeFactory>(), config, respuestas);

        var controller = new ScraperController(scraper, context, null!, null!);
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
