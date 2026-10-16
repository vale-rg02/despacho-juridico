using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static DespachoJuridico.API.Services.ScraperAcuerdosService;

namespace DespachoJuridico.Tests;

// DJ-139: EjecutarScrapingAsync orquestando la bitácora de verdad, con EF real
// (InMemory) -- sin tocar la red real: ScrapearJuzgadoAsync se sobreescribe
// con respuestas controladas (ver ScraperDePrueba). El cálculo puro
// (ClasificarDescarteSinCandidato) se prueba aparte en
// BitacoraEvaluacionScraperTests.cs.
public class BitacoraEvaluacionScraperIntegracionTests
{
    private const int IdUnidadDePrueba = 153; // "2do Civil Hermosillo" -- real, está en JuzgadosHermosillo
    private const string NombreJuzgadoDePrueba = "2do Civil Hermosillo";

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

    // DJ-139: para probar que un fallo al guardar la bitácora no interrumpe el
    // ciclo -- solo truena cuando hay una BitacoraEvaluacionScraper pendiente
    // de guardar, nunca al guardar el acuerdo real en sí.
    private class AppDbContextQueFallaAlGuardarBitacora : AppDbContext
    {
        public AppDbContextQueFallaAlGuardarBitacora(DbContextOptions<AppDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ChangeTracker.Entries<BitacoraEvaluacionScraper>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("Fallo simulado al guardar la bitácora");
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private static (AppDbContext Context, ScraperDePrueba Servicio) CrearEscenario(
        string nombreBD, Dictionary<int, ResultadoScrapeoJuzgado> respuestas)
    {
        var servicios = new ServiceCollection();
        servicios.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(nombreBD));
        servicios.AddScoped<IEmailService, FakeEmailService>();
        var proveedor = servicios.BuildServiceProvider();

        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreBD).Options);
        var config = new ConfigurationBuilder().Build();
        var servicio = new ScraperDePrueba(proveedor.GetRequiredService<IServiceScopeFactory>(), config, respuestas);

        return (context, servicio);
    }

    // DJ-139: para probar EtapaFalla == "notificacion" -- el acuerdo ya se
    // guardó cuando esto truena, a diferencia de un fallo antes de guardar.
    private class EmailServiceQueFalla : IEmailService
    {
        public Task EnviarAsync(string destinatarioEmail, string destinatarioNombre, string asunto, string cuerpoHtml)
            => throw new InvalidOperationException("Fallo simulado al enviar notificación");
    }

    private static (AppDbContext Context, ScraperDePrueba Servicio) CrearEscenarioConNotificacionQueFalla(
        string nombreBD, Dictionary<int, ResultadoScrapeoJuzgado> respuestas)
    {
        var servicios = new ServiceCollection();
        servicios.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(nombreBD));
        servicios.AddScoped<IEmailService, EmailServiceQueFalla>();
        var proveedor = servicios.BuildServiceProvider();

        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreBD).Options);
        var config = new ConfigurationBuilder().Build();
        var servicio = new ScraperDePrueba(proveedor.GetRequiredService<IServiceScopeFactory>(), config, respuestas);

        return (context, servicio);
    }

    private static Expediente NuevoExpediente(string numero, string parteDemandada) => new()
    {
        NumeroExpediente = numero,
        ParteDemandada = parteDemandada,
        Juzgado = NombreJuzgadoDePrueba,
        TipoJuicio = "Civil",
        CreadoPorId = 1
    };

    [Fact]
    public async Task UnidadConCeroRegistros_DejaFilaConCeros()
    {
        var respuestas = new Dictionary<int, ResultadoScrapeoJuzgado> { [IdUnidadDePrueba] = new(new(), true, null) };
        var (context, servicio) = CrearEscenario(nameof(UnidadConCeroRegistros_DejaFilaConCeros), respuestas);

        var resultado = await servicio.EjecutarScrapingAsync(
            fechaConsulta: new DateOnly(2026, 10, 1), idsUnidad: new HashSet<int> { IdUnidadDePrueba });

        var fila = Assert.Single(resultado.Embudo);
        Assert.True(fila.ConsultaExitosa);
        Assert.Equal(0, fila.Devueltos);
        Assert.Equal(0, fila.Evaluaciones);

        var filaBd = await context.BitacoraEvaluacionScraper.SingleAsync(b => b.IdUnidad == IdUnidadDePrueba);
        Assert.True(filaBd.ConsultaExitosa);
        Assert.Equal(0, filaBd.Devueltos);
    }

    [Fact]
    public async Task ConsultaFallida_QuedaRegistradaComoFallida()
    {
        var respuestas = new Dictionary<int, ResultadoScrapeoJuzgado> { [IdUnidadDePrueba] = new(new(), false, "HTTP_403") };
        var (context, servicio) = CrearEscenario(nameof(ConsultaFallida_QuedaRegistradaComoFallida), respuestas);

        var resultado = await servicio.EjecutarScrapingAsync(
            fechaConsulta: new DateOnly(2026, 10, 1), idsUnidad: new HashSet<int> { IdUnidadDePrueba });

        var fila = Assert.Single(resultado.Embudo);
        Assert.False(fila.ConsultaExitosa);
        Assert.Equal("HTTP_403", fila.MotivoFallo);

        var filaBd = await context.BitacoraEvaluacionScraper.SingleAsync(b => b.IdUnidad == IdUnidadDePrueba);
        Assert.False(filaBd.ConsultaExitosa);
        Assert.Equal("HTTP_403", filaBd.MotivoFallo);
    }

    [Fact]
    public async Task DryRun_NoEscribeNadaEnLaBaseDeDatosPeroElEmbudoSiSeVeEnLaRespuesta()
    {
        var nombreBD = nameof(DryRun_NoEscribeNadaEnLaBaseDeDatosPeroElEmbudoSiSeVeEnLaRespuesta);
        var acuerdos = new List<(string, string, string, DateOnly, string?)>
        {
            ("401/2026", "SE ADMITE DEMANDA.- JUAN PEREZ LOPEZ", "Se admite.", new DateOnly(2026, 10, 1), "Exp.")
        };
        var respuestas = new Dictionary<int, ResultadoScrapeoJuzgado> { [IdUnidadDePrueba] = new(acuerdos, true, null) };
        var (context, servicio) = CrearEscenario(nombreBD, respuestas);
        context.Expedientes.Add(NuevoExpediente("401/2026", "JUAN PEREZ LOPEZ"));
        await context.SaveChangesAsync();

        var resultado = await servicio.EjecutarScrapingAsync(
            fechaConsulta: new DateOnly(2026, 10, 1), dryRun: true, idsUnidad: new HashSet<int> { IdUnidadDePrueba });

        Assert.Equal(0, await context.AcuerdosScrapeados.CountAsync());
        Assert.Equal(0, await context.BitacoraEvaluacionScraper.CountAsync());
        Assert.Equal(0, await context.BitacoraDescarteItemizado.CountAsync());

        var fila = Assert.Single(resultado.Embudo);
        Assert.Equal(1, fila.GuardadoVisibleAlta);
    }

    [Fact]
    public async Task Invariante_SumaDeCategoriasSiempreIgualAEvaluaciones_InclusoConNumeroDuplicado()
    {
        // Mismo número de expediente, dos litigantes distintos (ver DJ-130/133
        // y docs/propuestas-tecnicas.md #3) -- un Devuelto produce 2
        // Evaluaciones. Más un tercer acuerdo totalmente ajeno.
        var nombreBD = nameof(Invariante_SumaDeCategoriasSiempreIgualAEvaluaciones_InclusoConNumeroDuplicado);
        var acuerdos = new List<(string, string, string, DateOnly, string?)>
        {
            ("401/2026", "SE ADMITE DEMANDA.- JUAN PEREZ LOPEZ", "Se admite.", new DateOnly(2026, 10, 1), "Exp."),
            ("999/2026", "ALGO SIN RELACION CON EL DESPACHO", "Síntesis ajena.", new DateOnly(2026, 10, 1), "Exp.")
        };
        var respuestas = new Dictionary<int, ResultadoScrapeoJuzgado> { [IdUnidadDePrueba] = new(acuerdos, true, null) };
        var (context, servicio) = CrearEscenario(nombreBD, respuestas);
        context.Expedientes.Add(NuevoExpediente("401/2026", "JUAN PEREZ LOPEZ"));
        context.Expedientes.Add(NuevoExpediente("401/2026", "MARIA LOPEZ GARCIA"));
        await context.SaveChangesAsync();

        var resultado = await servicio.EjecutarScrapingAsync(
            fechaConsulta: new DateOnly(2026, 10, 1), idsUnidad: new HashSet<int> { IdUnidadDePrueba });

        var fila = Assert.Single(resultado.Embudo);
        Assert.Equal(2, fila.Devueltos);
        Assert.Equal(3, fila.Evaluaciones); // 2 (número duplicado) + 1 (ajeno)

        var sumaCategorias = fila.GuardadoVisibleAlta + fila.GuardadoVisibleMedia + fila.GuardadoOcultoBaja +
            fila.YaExistia + fila.DescartadoSinCandidato + fila.DescartadoNumeroJuzgadoNoCoincide +
            fila.DescartadoExpedienteCerrado + fila.DescartadoFueraDeAlcanceFiltro + fila.DescartadoExcepcion;

        Assert.Equal(fila.Evaluaciones, sumaCategorias);
        Assert.Equal(1, fila.GuardadoVisibleAlta);
        Assert.Equal(1, fila.GuardadoOcultoBaja);
        Assert.Equal(1, fila.DescartadoSinCandidato);
    }

    [Theory]
    [InlineData(true, "Automatica")]
    [InlineData(false, "Backfill")]
    public async Task OrigenAutomaticaOBackfill_SeDerivaDeNotificar(bool notificar, string origenEsperado)
    {
        var nombreBD = $"{nameof(OrigenAutomaticaOBackfill_SeDerivaDeNotificar)}_{origenEsperado}";
        var respuestas = new Dictionary<int, ResultadoScrapeoJuzgado> { [IdUnidadDePrueba] = new(new(), true, null) };
        var (_, servicio) = CrearEscenario(nombreBD, respuestas);

        var resultado = await servicio.EjecutarScrapingAsync(
            fechaConsulta: new DateOnly(2026, 10, 1), idsUnidad: new HashSet<int> { IdUnidadDePrueba }, notificar: notificar);

        Assert.Equal(origenEsperado, Assert.Single(resultado.Embudo).Origen);
    }

    [Fact]
    public async Task OrigenManual_SeDerivaDeExpedienteIdsFiltro_YNoSeMezclaConAutomatica()
    {
        var nombreBD = nameof(OrigenManual_SeDerivaDeExpedienteIdsFiltro_YNoSeMezclaConAutomatica);
        var respuestas = new Dictionary<int, ResultadoScrapeoJuzgado> { [IdUnidadDePrueba] = new(new(), true, null) };
        var (_, servicio) = CrearEscenario(nombreBD, respuestas);

        var resultadoManual = await servicio.EjecutarScrapingAsync(
            fechaConsulta: new DateOnly(2026, 10, 1), idsUnidad: new HashSet<int> { IdUnidadDePrueba },
            expedienteIdsFiltro: new HashSet<int> { 1 });
        var resultadoAutomatico = await servicio.EjecutarScrapingAsync(
            fechaConsulta: new DateOnly(2026, 10, 1), idsUnidad: new HashSet<int> { IdUnidadDePrueba });

        Assert.Equal("Manual", Assert.Single(resultadoManual.Embudo).Origen);
        Assert.Equal("Automatica", Assert.Single(resultadoAutomatico.Embudo).Origen);
        Assert.NotEqual(resultadoManual.CorridaId, resultadoAutomatico.CorridaId);
    }

    [Fact]
    public async Task SiFallaElGuardadoDeLaBitacora_ElCicloTerminaNormalYElAcuerdoQuedaGuardado()
    {
        var nombreBD = nameof(SiFallaElGuardadoDeLaBitacora_ElCicloTerminaNormalYElAcuerdoQuedaGuardado);

        var opcionesBd = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreBD).Options;
        var servicios = new ServiceCollection();
        servicios.AddScoped<AppDbContext>(_ => new AppDbContextQueFallaAlGuardarBitacora(opcionesBd));
        servicios.AddScoped<IEmailService, FakeEmailService>();
        var proveedor = servicios.BuildServiceProvider();

        using (var seedContext = new AppDbContextQueFallaAlGuardarBitacora(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreBD).Options))
        {
            seedContext.Expedientes.Add(NuevoExpediente("401/2026", "JUAN PEREZ LOPEZ"));
            seedContext.SaveChanges();
        }

        var acuerdos = new List<(string, string, string, DateOnly, string?)>
        {
            ("401/2026", "SE ADMITE DEMANDA.- JUAN PEREZ LOPEZ", "Se admite.", new DateOnly(2026, 10, 1), "Exp.")
        };
        var respuestas = new Dictionary<int, ResultadoScrapeoJuzgado> { [IdUnidadDePrueba] = new(acuerdos, true, null) };
        var config = new ConfigurationBuilder().Build();
        var servicio = new ScraperDePrueba(proveedor.GetRequiredService<IServiceScopeFactory>(), config, respuestas);

        // No debe lanzar -- el ciclo termina normal aunque la bitácora falle.
        await servicio.EjecutarScrapingAsync(
            fechaConsulta: new DateOnly(2026, 10, 1), idsUnidad: new HashSet<int> { IdUnidadDePrueba });

        using var verificacion = new AppDbContextQueFallaAlGuardarBitacora(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreBD).Options);
        Assert.Equal(1, await verificacion.AcuerdosScrapeados.CountAsync());
        Assert.Equal(0, await verificacion.BitacoraEvaluacionScraper.CountAsync());
    }

    [Fact]
    public async Task SiFallaLaNotificacion_ElAcuerdoQuedaGuardadoYElItemizadoDiceEtapaNotificacion()
    {
        var nombreBD = nameof(SiFallaLaNotificacion_ElAcuerdoQuedaGuardadoYElItemizadoDiceEtapaNotificacion);
        var acuerdos = new List<(string, string, string, DateOnly, string?)>
        {
            ("401/2026", "SE ADMITE DEMANDA.- JUAN PEREZ LOPEZ", "Se admite.", new DateOnly(2026, 10, 1), "Exp.")
        };
        var respuestas = new Dictionary<int, ResultadoScrapeoJuzgado> { [IdUnidadDePrueba] = new(acuerdos, true, null) };
        var (context, servicio) = CrearEscenarioConNotificacionQueFalla(nombreBD, respuestas);
        var titular = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.Add(titular);
        await context.SaveChangesAsync();
        var expediente = NuevoExpediente("401/2026", "JUAN PEREZ LOPEZ");
        expediente.UsuarioAsignadoId = titular.Id;
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        // No debe lanzar -- el ciclo termina normal aunque falle la notificación.
        var resultado = await servicio.EjecutarScrapingAsync(
            fechaConsulta: new DateOnly(2026, 10, 1), idsUnidad: new HashSet<int> { IdUnidadDePrueba });

        Assert.Equal(1, await context.AcuerdosScrapeados.CountAsync());

        var fila = Assert.Single(resultado.Embudo);
        Assert.Equal(1, fila.GuardadoVisibleAlta); // ya se contó al guardar, antes de que fallara la notificación
        Assert.Equal(0, fila.DescartadoExcepcion); // no es un descarte -- el acuerdo sí se guardó

        var itemizado = Assert.Single(resultado.DescartesItemizados);
        Assert.Equal("EXCEPCION", itemizado.Razon);
        Assert.Equal("notificacion", itemizado.EtapaFalla);
    }
}
