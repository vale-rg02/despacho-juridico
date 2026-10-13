using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-133: GenerarReporteAsync en sí (la parte con EF real -- scoping, revalidación
// de JuzgadoCoincide, exclusión de juzgados fuera de alcance) y la garantía de que
// nunca escribe nada. El cálculo puro (umbral, banco único, palabra única, etc.)
// se prueba aparte en FrasesGenericasCandidatasServiceTests.cs sin EF, igual que
// ReevaluarOcultosAsync separa su lógica pura de su orquestación (ver
// ReevaluarMediaTests.cs).
public class FrasesGenericasCandidatasServiceIntegracionTests
{
    private static (AppDbContext Context, FrasesGenericasCandidatasService Servicio) CrearEscenario(string nombreBD)
    {
        var servicios = new ServiceCollection();
        servicios.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(nombreBD));
        var proveedor = servicios.BuildServiceProvider();

        var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreBD).Options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FrasesGenericas:UmbralExpedientes"] = "3" })
            .Build();

        var servicio = new FrasesGenericasCandidatasService(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<FrasesGenericasCandidatasService>.Instance,
            config);

        return (context, servicio);
    }

    private static Expediente NuevoExpediente(string numero, string juzgado, string tipoJuicio = "Hipotecario") => new()
    {
        NumeroExpediente = numero,
        ParteDemandada = "Parte de prueba",
        Juzgado = juzgado,
        TipoJuicio = tipoJuicio,
        CreadoPorId = 1
    };

    private static AcuerdoScrapeado NuevoAcuerdo(
        Expediente expediente, int idUnidad, string nombreJuzgado, string partes,
        string? tipoAsunto = "Exp.", bool oculto = true, string? confianza = "Baja") => new()
    {
        ExpedienteId = expediente.Id,
        NumeroExpediente = expediente.NumeroExpediente,
        IdUnidad = idUnidad,
        NombreJuzgado = nombreJuzgado,
        Partes = partes,
        Sintesis = "Síntesis de prueba",
        FechaAcuerdo = DateOnly.FromDateTime(DateTime.UtcNow),
        FechaDetectado = DateTime.UtcNow,
        TipoAsunto = tipoAsunto,
        Oculto = oculto,
        Confianza = confianza
    };

    [Fact]
    public async Task JuzgadoForaneo_FueraDeHermosillo_NuncaCuentaComoCandidatoAunqueSeRepitaTresVeces()
    {
        var (context, servicio) = CrearEscenario(nameof(JuzgadoForaneo_FueraDeHermosillo_NuncaCuentaComoCandidatoAunqueSeRepitaTresVeces));

        for (var i = 0; i < 3; i++)
        {
            var exp = NuevoExpediente($"{100 + i}/2026", "1ro Civil Cajeme");
            context.Expedientes.Add(exp);
            await context.SaveChangesAsync();
            // IdUnidad 135 = "1ro Civil Cajeme" -- foráneo, no está en JuzgadosHermosillo.
            context.AcuerdosScrapeados.Add(NuevoAcuerdo(exp, 135, "1ro Civil Cajeme", "TRAMITE - ACCION FORANEA DE PRUEBA"));
        }
        await context.SaveChangesAsync();

        var reporte = await servicio.GenerarReporteAsync(null, CancellationToken.None);

        Assert.Empty(reporte.Candidatos);
    }

    [Fact]
    public async Task JuzgadoDelAcuerdoNoCoincideConElRegistradoEnElExpediente_SeExcluyeYSeCuenta()
    {
        // Reproduce el ruido real encontrado en la verificación de DJ-131: acuerdos
        // de un juzgado Laboral mal vinculados (por un JuzgadoCoincide viejo ya
        // corregido) a expedientes de otra materia -- JuzgadoCoincide actual los
        // rechaza, así que no deben contarse como candidato.
        var (context, servicio) = CrearEscenario(nameof(JuzgadoDelAcuerdoNoCoincideConElRegistradoEnElExpediente_SeExcluyeYSeCuenta));

        for (var i = 0; i < 3; i++)
        {
            // Registrado como Oral Mercantil, pero el acuerdo real es de un
            // Tribunal Laboral -- JuzgadoCoincide("2do Oral Mercantil Hermosillo",
            // "3er Tribunal Laboral Hermosillo") es false con el código actual.
            var exp = NuevoExpediente($"{400 + i}/2026", "2do Oral Mercantil Hermosillo", "Oral Mercantil");
            context.Expedientes.Add(exp);
            await context.SaveChangesAsync();
            // IdUnidad 333 = "3er Tribunal Laboral Hermosillo" (sí está en JuzgadosHermosillo).
            context.AcuerdosScrapeados.Add(NuevoAcuerdo(exp, 333, "3er Tribunal Laboral Hermosillo", "ORDINARIO - DESPIDO INJUSTIFICADO"));
        }
        await context.SaveChangesAsync();

        var reporte = await servicio.GenerarReporteAsync(null, CancellationToken.None);

        Assert.Empty(reporte.Candidatos);
        Assert.Equal(3, reporte.ExcluidosPorJuzgadoNoValido);
    }

    [Fact]
    public async Task CasoRealDelMismoJuzgado_SiCuentaComoCandidato()
    {
        // Control positivo: mismo escenario que el caso real corregido de DJ-131
        // (581/2018, 2do Civil Hermosillo) pero con 3 expedientes para cruzar el
        // umbral -- confirma que el filtro de arriba no excluye de más.
        var (context, servicio) = CrearEscenario(nameof(CasoRealDelMismoJuzgado_SiCuentaComoCandidato));

        for (var i = 0; i < 3; i++)
        {
            var exp = NuevoExpediente($"{500 + i}/2026", "2do Civil Hermosillo", "Hipotecario");
            context.Expedientes.Add(exp);
            await context.SaveChangesAsync();
            // IdUnidad 153 = "2do Civil Hermosillo", coincide con el registrado.
            context.AcuerdosScrapeados.Add(NuevoAcuerdo(exp, 153, "2do Civil Hermosillo", "ESPECIAL HIPOTECARIO - ACCION PERSONAL Y REAL NUEVA"));
        }
        await context.SaveChangesAsync();

        var reporte = await servicio.GenerarReporteAsync(null, CancellationToken.None);

        var candidato = Assert.Single(reporte.Candidatos);
        Assert.Equal(3, candidato.ExpedientesDistintos);
        Assert.Equal(0, reporte.ExcluidosPorJuzgadoNoValido);
    }

    [Fact]
    public async Task GenerarReporte_NuncaCambiaOcultoNiConfianzaDeNingunAcuerdo()
    {
        var (context, servicio) = CrearEscenario(nameof(GenerarReporte_NuncaCambiaOcultoNiConfianzaDeNingunAcuerdo));

        var ids = new List<int>();
        for (var i = 0; i < 3; i++)
        {
            var exp = NuevoExpediente($"{600 + i}/2026", "2do Civil Hermosillo", "Hipotecario");
            context.Expedientes.Add(exp);
            await context.SaveChangesAsync();
            var acuerdo = NuevoAcuerdo(exp, 153, "2do Civil Hermosillo", "ESPECIAL HIPOTECARIO - OTRA ACCION NUEVA SIN APROBAR");
            context.AcuerdosScrapeados.Add(acuerdo);
            await context.SaveChangesAsync();
            ids.Add(acuerdo.Id);
        }

        var reporte = await servicio.GenerarReporteAsync(null, CancellationToken.None);
        Assert.NotEmpty(reporte.Candidatos); // confirma que de verdad se evaluó el escenario, no que no hizo nada

        foreach (var id in ids)
        {
            var acuerdo = await context.AcuerdosScrapeados.FindAsync(id);
            Assert.NotNull(acuerdo);
            Assert.True(acuerdo!.Oculto);
            Assert.Equal("Baja", acuerdo.Confianza);
        }
    }
}
