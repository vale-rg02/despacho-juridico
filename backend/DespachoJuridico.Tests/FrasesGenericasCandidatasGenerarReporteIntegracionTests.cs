using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-137: a diferencia del resto de FrasesGenericasCandidatasServiceTests.cs (que
// prueba CalcularCandidatos puro, pasándole candidatosYaDescartados a mano), esta
// prueba sí ejercita GenerarReporteAsync de verdad -- con EF real, consultando
// RevisionesCandidatoFraseGenerica tal como lo hace producción. Se agrega aparte
// (no es la convención general de esta clase, ver comentario del otro archivo)
// porque este cableado específico es CRUD simple y barato de probar end-to-end,
// a diferencia de la lógica de coincidencia de texto que sí se verifica con datos
// reales por separado. Cadena de candidato inventada, no texto real.
public class FrasesGenericasCandidatasGenerarReporteIntegracionTests
{
    private const int IdUnidadDePrueba = 153; // "2do Civil Hermosillo"
    private const string NombreJuzgadoDePrueba = "2do Civil Hermosillo";
    private const string FraseDePrueba = "FRASE DE INTEGRACION INVENTADA";

    private static (AppDbContext Context, FrasesGenericasCandidatasService Servicio) CrearEscenario(string nombreBD)
    {
        var servicios = new ServiceCollection();
        servicios.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(nombreBD));
        var proveedor = servicios.BuildServiceProvider();

        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreBD).Options);
        var servicio = new FrasesGenericasCandidatasService(
            proveedor.GetRequiredService<IServiceScopeFactory>(), NullLogger<FrasesGenericasCandidatasService>.Instance,
            new ConfigurationBuilder().Build());

        return (context, servicio);
    }

    private static async Task SembrarTresAcuerdosOcultosConLaMismaFraseAsync(AppDbContext context)
    {
        for (var i = 1; i <= 3; i++)
        {
            var numero = $"{100 * i}/2026";
            var expediente = new Expediente
            {
                NumeroExpediente = numero, ParteDemandada = "SIN IMPORTANCIA", Juzgado = NombreJuzgadoDePrueba,
                TipoJuicio = "Civil", CreadoPorId = 1
            };
            context.Expedientes.Add(expediente);
            await context.SaveChangesAsync();

            context.AcuerdosScrapeados.Add(new AcuerdoScrapeado
            {
                ExpedienteId = expediente.Id, NumeroExpediente = numero, IdUnidad = IdUnidadDePrueba,
                NombreJuzgado = NombreJuzgadoDePrueba, Partes = $"ORAL MERCANTIL.- {FraseDePrueba}",
                Sintesis = "Se admite.", FechaAcuerdo = new DateOnly(2026, 9, 1),
                Confianza = "Baja", Oculto = true
            });
        }
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CandidatoSinRevision_ApareceEnElReporteReal()
    {
        var nombreBD = nameof(CandidatoSinRevision_ApareceEnElReporteReal);
        var (context, servicio) = CrearEscenario(nombreBD);
        await SembrarTresAcuerdosOcultosConLaMismaFraseAsync(context);

        var reporte = await servicio.GenerarReporteAsync(null, CancellationToken.None);

        var candidato = Assert.Single(reporte.Candidatos);
        Assert.Equal(FraseDePrueba, candidato.Frase);
        Assert.Equal(0, reporte.ExcluidosPorYaDescartado);
    }

    [Fact]
    public async Task CandidatoYaDescartadoEnLaTablaReal_GenerarReporteAsyncLoExcluye()
    {
        // Esta es la prueba que faltaba: confirma el cableado real de
        // GenerarReporteAsync -> RevisionesCandidatoFraseGenerica, no solo la
        // función pura CalcularCandidatos con la lista pasada a mano.
        var nombreBD = nameof(CandidatoYaDescartadoEnLaTablaReal_GenerarReporteAsyncLoExcluye);
        var (context, servicio) = CrearEscenario(nombreBD);
        await SembrarTresAcuerdosOcultosConLaMismaFraseAsync(context);

        var admin = new Usuario { Nombre = "Admin", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.Add(admin);
        await context.SaveChangesAsync();
        context.RevisionesCandidatoFraseGenerica.Add(new RevisionCandidatoFraseGenerica
        {
            CandidatoNormalizado = FraseDePrueba, Decision = "Descartado", RevisadoPorId = admin.Id
        });
        await context.SaveChangesAsync();

        var reporte = await servicio.GenerarReporteAsync(null, CancellationToken.None);

        Assert.Empty(reporte.Candidatos);
        Assert.Equal(1, reporte.ExcluidosPorYaDescartado);
    }
}
