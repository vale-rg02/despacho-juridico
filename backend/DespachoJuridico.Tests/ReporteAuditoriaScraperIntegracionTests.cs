using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-140: GenerarReporteAsync orquestando todo junto con una base real (EF
// InMemory) -- mismo patrón que BitacoraEvaluacionScraperIntegracionTests.cs
// de DJ-139. La lógica pura (CalcularDiasHabilesSinSeveridad, DetectarUnidadesEnCero,
// DetectarCaidasBruscas, ValidarHallazgo, PuntajeMaximoSimilitud) se prueba
// aparte en ReporteAuditoriaScraperTests.cs / PartesCoincidenTests.cs.
public class ReporteAuditoriaScraperIntegracionTests
{
    private const int IdUnidad2doCivilHermosillo = 153;
    private const int IdUnidad1roCivilCajeme = 135; // foráneo real

    private static (AppDbContext Context, ReporteAuditoriaScraperService Servicio, FakeEmailService Email) CrearEscenario(
        string nombreBD, Dictionary<string, string?>? configExtra = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(configExtra ?? new()).Build();

        var servicios = new ServiceCollection();
        servicios.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(nombreBD));
        var emailFake = new FakeEmailService();
        servicios.AddSingleton<IEmailService>(emailFake);
        var proveedor = servicios.BuildServiceProvider();

        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreBD).Options);

        var serviciosFrases = new ServiceCollection();
        serviciosFrases.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(nombreBD));
        var proveedorFrases = serviciosFrases.BuildServiceProvider();
        var frasesGenericas = new FrasesGenericasCandidatasService(
            proveedorFrases.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<FrasesGenericasCandidatasService>.Instance, config);

        var servicio = new ReporteAuditoriaScraperService(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ReporteAuditoriaScraperService>.Instance, config, frasesGenericas);

        return (context, servicio, emailFake);
    }

    private static Expediente NuevoExpediente(string numero, string juzgado, string parteDemandada = "SIN IMPORTANCIA") => new()
    {
        NumeroExpediente = numero, Juzgado = juzgado, ParteDemandada = parteDemandada,
        TipoJuicio = "Civil", CreadoPorId = 1
    };

    private static AcuerdoScrapeado NuevoAcuerdoOcultoBaja(
        Expediente expediente, int idUnidad, string nombreJuzgado, string partes = "DEMANDA.- SIN NOMBRE RECONOCIBLE") => new()
    {
        Expediente = expediente, ExpedienteId = expediente.Id, NumeroExpediente = expediente.NumeroExpediente,
        IdUnidad = idUnidad, NombreJuzgado = nombreJuzgado, Partes = partes, Sintesis = "Se admite.",
        FechaAcuerdo = new DateOnly(2026, 10, 16), FechaDetectado = DateTime.UtcNow,
        Oculto = true, Confianza = "Baja"
    };

    [Fact]
    public async Task Cola1_OcultoHermosilloConJuzgadoExacto_EntraALaCola()
    {
        var nombreBD = nameof(Cola1_OcultoHermosilloConJuzgadoExacto_EntraALaCola);
        var (context, servicio, _) = CrearEscenario(nombreBD);

        var expediente = NuevoExpediente("401/2026", "2do Civil Hermosillo");
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        context.AcuerdosScrapeados.Add(NuevoAcuerdoOcultoBaja(expediente, IdUnidad2doCivilHermosillo, "2do Civil Hermosillo"));
        await context.SaveChangesAsync();

        var reporte = await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: false, CancellationToken.None);

        var item = Assert.Single(reporte.Cola1OcultosHermosillo);
        Assert.Equal("401/2026", item.NumeroExpediente);
        Assert.Equal(0, reporte.Cola1ExcluidosPorRuidoConocido);
    }

    [Fact]
    public async Task Cola1_RuidoConocidoDeJuzgadoCoincideViejo_SeExcluyeYSeCuentaAparte()
    {
        var nombreBD = nameof(Cola1_RuidoConocidoDeJuzgadoCoincideViejo_SeExcluyeYSeCuentaAparte);
        var (context, servicio, _) = CrearEscenario(nombreBD);

        // Expediente de materia Penal vinculado (por el JuzgadoCoincide viejo) a
        // un acuerdo que en realidad es de Civil -- el caso real de DJ-98 (82 registros).
        var expediente = NuevoExpediente("401/2026", "1ro Penal Hermosillo");
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        context.AcuerdosScrapeados.Add(NuevoAcuerdoOcultoBaja(expediente, IdUnidad2doCivilHermosillo, "2do Civil Hermosillo"));
        await context.SaveChangesAsync();

        var reporte = await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: false, CancellationToken.None);

        Assert.Empty(reporte.Cola1OcultosHermosillo);
        Assert.Equal(1, reporte.Cola1ExcluidosPorRuidoConocido);
    }

    [Fact]
    public async Task Cola2_ForaneoDentroDeLaBanda_EntraALaCola()
    {
        var nombreBD = nameof(Cola2_ForaneoDentroDeLaBanda_EntraALaCola);
        var (context, servicio, _) = CrearEscenario(nombreBD);

        // "MORENO DIAZ RUIZ" vs "MORALES DIAZ RUIZ" -- puntaje medido 0.75,
        // dentro de la banda default [0.65, 0.80).
        var expediente = NuevoExpediente("401/2026", "2do Civil Hermosillo", "MORENO DIAZ RUIZ");
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        context.AcuerdosScrapeados.Add(NuevoAcuerdoOcultoBaja(
            expediente, IdUnidad1roCivilCajeme, "1ro Civil Cajeme", "DEMANDA.- PEDRO MORALES DIAZ RUIZ VS BANCO"));
        await context.SaveChangesAsync();

        var reporte = await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: false, CancellationToken.None);

        var item = Assert.Single(reporte.Cola2ForaneosParecidos);
        Assert.Equal("401/2026", item.NumeroExpediente);
        Assert.NotNull(item.PuntajeSimilitud);
        Assert.InRange(item.PuntajeSimilitud!.Value, 0.65, 0.80);
    }

    [Fact]
    public async Task Cola2_ForaneoFueraDeLaBanda_NoEntraALaCola()
    {
        var nombreBD = nameof(Cola2_ForaneoFueraDeLaBanda_NoEntraALaCola);
        var (context, servicio, _) = CrearEscenario(nombreBD);

        var expediente = NuevoExpediente("401/2026", "2do Civil Hermosillo", "GARCIA LOPEZ");
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        context.AcuerdosScrapeados.Add(NuevoAcuerdoOcultoBaja(
            expediente, IdUnidad1roCivilCajeme, "1ro Civil Cajeme", "DEMANDA.- TOTALMENTE OTRO ASUNTO SIN RELACION"));
        await context.SaveChangesAsync();

        var reporte = await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: false, CancellationToken.None);

        Assert.Empty(reporte.Cola2ForaneosParecidos);
    }

    [Fact]
    public async Task Cola2b_IncluyeSoloElDescarteConIndicioDeJuzgado()
    {
        var nombreBD = nameof(Cola2b_IncluyeSoloElDescarteConIndicioDeJuzgado);
        var (context, servicio, _) = CrearEscenario(nombreBD);

        // El expediente real tiene juzgado parecido al que devolvió ADISON --
        // indicio de bug de nombre de juzgado, no solo colisión de número.
        context.Expedientes.Add(NuevoExpediente("401/2026", "2do Civil Hermosillo"));
        await context.SaveChangesAsync();

        context.BitacoraDescarteItemizado.Add(new BitacoraDescarteItemizado
        {
            CorridaId = Guid.NewGuid(), FechaHoraCorrida = DateTime.UtcNow, IdUnidad = IdUnidad2doCivilHermosillo,
            NombreJuzgado = "2do Civl Hermosillo", // typo real plausible, similar
            NumeroExpediente = "401/2026", Razon = "NUMERO_COINCIDE_JUZGADO_NO"
        });
        // Colisión sin indicio: juzgado totalmente distinto (puntaje medido 0.257,
        // bien por debajo del umbral default de 0.5).
        context.Expedientes.Add(NuevoExpediente("999/2026", "Arrendamiento Hermosillo"));
        await context.SaveChangesAsync();
        context.BitacoraDescarteItemizado.Add(new BitacoraDescarteItemizado
        {
            CorridaId = Guid.NewGuid(), FechaHoraCorrida = DateTime.UtcNow, IdUnidad = 276,
            NombreJuzgado = "1er Tribunal Colegiado 1er Circuito",
            NumeroExpediente = "999/2026", Razon = "NUMERO_COINCIDE_JUZGADO_NO"
        });
        await context.SaveChangesAsync();

        var reporte = await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: false, CancellationToken.None);

        var item = Assert.Single(reporte.Cola2bColisionesConIndicio);
        Assert.Equal("401/2026", item.NumeroExpediente);
        Assert.Equal(1, reporte.Cola2bColisionesSinIndicio);
    }

    [Fact]
    public async Task MarcaDeAgua_LoYaReportadoNoSeRepite_YLaVistaPreviaNoLaAvanza()
    {
        var nombreBD = nameof(MarcaDeAgua_LoYaReportadoNoSeRepite_YLaVistaPreviaNoLaAvanza);
        var (context, servicio, _) = CrearEscenario(nombreBD);

        var expediente = NuevoExpediente("401/2026", "2do Civil Hermosillo");
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        context.AcuerdosScrapeados.Add(NuevoAcuerdoOcultoBaja(expediente, IdUnidad2doCivilHermosillo, "2do Civil Hermosillo"));
        await context.SaveChangesAsync();

        // Vista previa (dryRun): aparece, pero NO avanza la marca de agua.
        var preview1 = await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: false, CancellationToken.None);
        Assert.Single(preview1.Cola1OcultosHermosillo);

        var preview2 = await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: false, CancellationToken.None);
        Assert.Single(preview2.Cola1OcultosHermosillo); // sigue apareciendo -- la vista previa no avanzó nada

        // Envío real: avanza la marca de agua.
        await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: true, CancellationToken.None);

        // Un reporte nuevo después del envío real ya no debe repetirlo.
        var despues = await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: false, CancellationToken.None);
        Assert.Empty(despues.Cola1OcultosHermosillo);
    }

    [Fact]
    public async Task CorridasManualesForzadas_NoSeCuentanComoCicloAutomatico()
    {
        var nombreBD = nameof(CorridasManualesForzadas_NoSeCuentanComoCicloAutomatico);
        var (context, servicio, _) = CrearEscenario(nombreBD);
        var corridaManualId = Guid.NewGuid();

        context.BitacoraEvaluacionScraper.Add(new BitacoraEvaluacionScraper
        {
            CorridaId = corridaManualId, FechaHoraCorrida = DateTime.UtcNow, FechaConsultada = new DateOnly(2026, 10, 16),
            IdUnidad = IdUnidad2doCivilHermosillo, NombreJuzgado = "2do Civil Hermosillo",
            Origen = "ManualForzada", ConsultaExitosa = true, Devueltos = 5
        });
        context.BitacoraEvaluacionScraper.Add(new BitacoraEvaluacionScraper
        {
            CorridaId = Guid.NewGuid(), FechaHoraCorrida = DateTime.UtcNow, FechaConsultada = new DateOnly(2026, 10, 16),
            IdUnidad = IdUnidad2doCivilHermosillo, NombreJuzgado = "2do Civil Hermosillo",
            Origen = "Automatica", ConsultaExitosa = true, Devueltos = 3
        });
        await context.SaveChangesAsync();

        var reporte = await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: false, CancellationToken.None);

        Assert.Equal(1, reporte.CorridasNoAutomaticasDelDia);
        Assert.Equal(3, reporte.ResumenEmbudoAutomatico.Devueltos); // solo la fila Automatica
    }

    [Fact]
    public async Task SinHallazgosNiCandidatos_ElReporteSeArmaIgualConElEmbudoYConteos()
    {
        var nombreBD = nameof(SinHallazgosNiCandidatos_ElReporteSeArmaIgualConElEmbudoYConteos);
        var (context, servicio, email) = CrearEscenario(nombreBD, new() { ["Auditoria:Destinatarios"] = "equipo@test.com" });

        context.BitacoraEvaluacionScraper.Add(new BitacoraEvaluacionScraper
        {
            CorridaId = Guid.NewGuid(), FechaHoraCorrida = DateTime.UtcNow, FechaConsultada = new DateOnly(2026, 10, 16),
            IdUnidad = IdUnidad2doCivilHermosillo, NombreJuzgado = "2do Civil Hermosillo",
            Origen = "Automatica", ConsultaExitosa = true, Devueltos = 0
        });
        await context.SaveChangesAsync();

        var reporte = await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: false, CancellationToken.None);

        Assert.NotNull(reporte.ResumenEmbudoAutomatico);
        Assert.Empty(reporte.Cola1OcultosHermosillo);
        Assert.Empty(reporte.Cola2ForaneosParecidos);
    }

    [Fact]
    public async Task ElReporteNuncaCambiaOcultoNiConfianzaDeNingunAcuerdo()
    {
        var nombreBD = nameof(ElReporteNuncaCambiaOcultoNiConfianzaDeNingunAcuerdo);
        var (context, servicio, _) = CrearEscenario(nombreBD);

        var expediente = NuevoExpediente("401/2026", "2do Civil Hermosillo");
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        context.AcuerdosScrapeados.Add(NuevoAcuerdoOcultoBaja(expediente, IdUnidad2doCivilHermosillo, "2do Civil Hermosillo"));
        await context.SaveChangesAsync();

        await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: true, CancellationToken.None);

        var acuerdo = await context.AcuerdosScrapeados.SingleAsync();
        Assert.True(acuerdo.Oculto);
        Assert.Equal("Baja", acuerdo.Confianza);
    }

    [Fact]
    public async Task ElCorreoNuncaContieneElTextoDePartes()
    {
        var nombreBD = nameof(ElCorreoNuncaContieneElTextoDePartes);
        var (context, servicio, email) = CrearEscenario(nombreBD, new() { ["Auditoria:Destinatarios"] = "equipo@test.com" });

        var textoPartesSensible = "DEMANDA.- JUAN PEREZ LOPEZ VS BANCO XYZ CONFIDENCIAL";
        var expediente = NuevoExpediente("401/2026", "2do Civil Hermosillo", "PEREZ LOPEZ");
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        context.AcuerdosScrapeados.Add(NuevoAcuerdoOcultoBaja(expediente, IdUnidad2doCivilHermosillo, "2do Civil Hermosillo", textoPartesSensible));
        await context.SaveChangesAsync();

        var reporte = await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: true, CancellationToken.None);
        await servicio.EnviarReporteAsync(reporte);

        var enviado = Assert.Single(email.Enviados);
        Assert.DoesNotContain("PEREZ LOPEZ", enviado.Cuerpo);
        Assert.DoesNotContain(textoPartesSensible, enviado.Cuerpo);
    }

    [Fact]
    public async Task DiasHabilesYTablaDeHallazgos_SeLeenDesdeElRegistroReal()
    {
        var nombreBD = nameof(DiasHabilesYTablaDeHallazgos_SeLeenDesdeElRegistroReal);
        var (context, servicio, _) = CrearEscenario(nombreBD, new()
        {
            ["Auditoria:InicioVentana"] = "2026-10-12"
        });

        var registrador = new Usuario { Nombre = "Admin", Email = "admin@test.com", PasswordHash = "x" };
        context.Usuarios.Add(registrador);
        await context.SaveChangesAsync();

        context.HallazgosAuditoria.Add(new HallazgoAuditoria
        {
            Fecha = new DateOnly(2026, 10, 14), Severidad = "S3", Categoria = "cosmetico",
            Descripcion = "detalle menor", RegistradoPorId = registrador.Id
        });
        await context.SaveChangesAsync();

        var reporte = await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: false, CancellationToken.None);

        Assert.True(reporte.VentanaIniciada);
        Assert.Equal(5, reporte.DiasHabilesSinS1); // 12,13,14,15,16 -- S1 nunca ocurrió
        Assert.Equal(5, reporte.DiasHabilesSinS2);
        Assert.Equal(2, reporte.DiasHabilesSinS3); // reinicia en 14, suma 15 y 16
        var fila = Assert.Single(reporte.TablaHallazgos);
        Assert.Equal("Admin", fila.RegistradoPorNombre);
    }

    [Fact]
    public async Task AntesDeInicioDeVentana_MuestraVentanaNoIniciada()
    {
        var nombreBD = nameof(AntesDeInicioDeVentana_MuestraVentanaNoIniciada);
        var (_, servicio, _) = CrearEscenario(nombreBD, new()
        {
            ["Auditoria:InicioVentana"] = "2026-11-01"
        });

        var reporte = await servicio.GenerarReporteAsync(new DateOnly(2026, 10, 16), avanzarMarcaDeAgua: false, CancellationToken.None);

        Assert.False(reporte.VentanaIniciada);
        Assert.Null(reporte.DiasHabilesSinS1);
    }
}
