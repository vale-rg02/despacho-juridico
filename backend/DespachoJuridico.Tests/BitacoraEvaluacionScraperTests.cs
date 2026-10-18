using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Xunit;
using static DespachoJuridico.API.Services.ScraperAcuerdosService;

namespace DespachoJuridico.Tests;

// DJ-139: ClasificarDescarteSinCandidato es la pieza de decisión 100% pura de
// la bitácora -- sin EF, sin IO. El resto (EjecutarScrapingAsync orquestando
// todo junto con una base real) se prueba aparte en
// BitacoraEvaluacionScraperIntegracionTests.cs, igual que el resto del scraper
// separa lógica pura de orquestación (ver ReevaluarMediaTests.cs).
// DJ-140: ClasificarOrigenCorrida es la segunda pieza de decisión pura del
// scraper -- igual que ClasificarDescarteSinCandidato, sin EF ni IO.
public class ClasificarOrigenCorridaTests
{
    [Fact]
    public void ConFiltroDeExpedientes_EsManual_SinImportarLoDemas()
    {
        Assert.Equal("Manual", ScraperAcuerdosService.ClasificarOrigenCorrida(
            tieneFiltroExpedientes: true, notificar: true, esCicloAutomaticoProgramado: true));
        Assert.Equal("Manual", ScraperAcuerdosService.ClasificarOrigenCorrida(
            tieneFiltroExpedientes: true, notificar: false, esCicloAutomaticoProgramado: false));
    }

    [Fact]
    public void SinFiltroYNotificarFalse_EsBackfill()
    {
        Assert.Equal("Backfill", ScraperAcuerdosService.ClasificarOrigenCorrida(
            tieneFiltroExpedientes: false, notificar: false, esCicloAutomaticoProgramado: false));
    }

    [Fact]
    public void SinFiltroNotificarTrueYCicloInterno_EsAutomatica()
    {
        Assert.Equal("Automatica", ScraperAcuerdosService.ClasificarOrigenCorrida(
            tieneFiltroExpedientes: false, notificar: true, esCicloAutomaticoProgramado: true));
    }

    // DJ-140: el caso que antes se perdía dentro de "Automatica" -- un admin
    // forzando /ejecutar a mano, sin filtros, sin ser el ciclo interno.
    [Fact]
    public void SinFiltroNotificarTrueSinCicloInterno_EsManualForzada()
    {
        Assert.Equal("ManualForzada", ScraperAcuerdosService.ClasificarOrigenCorrida(
            tieneFiltroExpedientes: false, notificar: true, esCicloAutomaticoProgramado: false));
    }
}

public class ClasificarDescarteSinCandidatoTests
{
    private static Expediente Exp(string numero, int id = 1) => new() { Id = id, NumeroExpediente = numero, Juzgado = "2do Civil Hermosillo" };

    [Fact]
    public void SinCoincidenciaEnNingunLado_EsSinCandidato()
    {
        var bitacora = new BitacoraEvaluacionScraper();
        var descartes = new List<BitacoraDescarteItemizado>();

        ClasificarDescarteSinCandidato(
            "999/2026", 153, "2do Civil Hermosillo", esHermosillo: true,
            expedienteIdsFiltro: null, expedientesEnAlcance: new List<Expediente>(), todosActivos: new List<Expediente>(),
            numerosExpedientesCerrados: new List<string>(), Guid.NewGuid(), DateTime.UtcNow, bitacora, descartes);

        Assert.Equal(1, bitacora.DescartadoSinCandidato);
        Assert.Empty(descartes); // el descarte masivo no se itemiza
    }

    [Fact]
    public void HermosilloConNumeroCoincidente_PeroJuzgadoNoEnLaListaDeAlcance_EsNumeroJuzgadoNoCoincide()
    {
        var bitacora = new BitacoraEvaluacionScraper();
        var descartes = new List<BitacoraDescarteItemizado>();
        var enAlcance = new List<Expediente> { Exp("401/2026") };

        ClasificarDescarteSinCandidato(
            "401/2026", 153, "2do Civil Hermosillo", esHermosillo: true,
            expedienteIdsFiltro: null, expedientesEnAlcance: enAlcance, todosActivos: enAlcance,
            numerosExpedientesCerrados: new List<string>(), Guid.NewGuid(), DateTime.UtcNow, bitacora, descartes);

        Assert.Equal(1, bitacora.DescartadoNumeroJuzgadoNoCoincide);
        var item = Assert.Single(descartes);
        Assert.Equal("NUMERO_COINCIDE_JUZGADO_NO", item.Razon);
        Assert.Equal("401/2026", item.NumeroExpediente);
    }

    [Fact]
    public void Foraneo_NumeroCoincideEnAlcance_NuncaEsNumeroJuzgadoNoCoincide()
    {
        // Foráneo nunca exige que el juzgado coincida -- si llegó aquí con
        // candidatos.Count==0 siendo foráneo, no puede ser por el juzgado.
        var bitacora = new BitacoraEvaluacionScraper();
        var descartes = new List<BitacoraDescarteItemizado>();
        var enAlcance = new List<Expediente> { Exp("401/2026") };

        ClasificarDescarteSinCandidato(
            "401/2026", 135, "1ro Civil Cajeme", esHermosillo: false,
            expedienteIdsFiltro: null, expedientesEnAlcance: enAlcance, todosActivos: enAlcance,
            numerosExpedientesCerrados: new List<string>(), Guid.NewGuid(), DateTime.UtcNow, bitacora, descartes);

        Assert.Equal(0, bitacora.DescartadoNumeroJuzgadoNoCoincide);
    }

    [Fact]
    public void CoincideSoloFueraDelFiltroDeLaCorridaManual_EsFueraDeAlcanceFiltro()
    {
        var bitacora = new BitacoraEvaluacionScraper();
        var descartes = new List<BitacoraDescarteItemizado>();
        var todos = new List<Expediente> { Exp("401/2026", id: 7) };
        var filtro = new HashSet<int> { 999 }; // no incluye el Id=7

        ClasificarDescarteSinCandidato(
            "401/2026", 153, "2do Civil Hermosillo", esHermosillo: true,
            expedienteIdsFiltro: filtro, expedientesEnAlcance: new List<Expediente>(), todosActivos: todos,
            numerosExpedientesCerrados: new List<string>(), Guid.NewGuid(), DateTime.UtcNow, bitacora, descartes);

        Assert.Equal(1, bitacora.DescartadoFueraDeAlcanceFiltro);
        Assert.Equal("FUERA_DE_ALCANCE_FILTRO", Assert.Single(descartes).Razon);
    }

    [Fact]
    public void CoincideSoloConUnExpedienteCerrado_EsExpedienteCerrado()
    {
        var bitacora = new BitacoraEvaluacionScraper();
        var descartes = new List<BitacoraDescarteItemizado>();

        ClasificarDescarteSinCandidato(
            "401/2026", 153, "2do Civil Hermosillo", esHermosillo: true,
            expedienteIdsFiltro: null, expedientesEnAlcance: new List<Expediente>(), todosActivos: new List<Expediente>(),
            numerosExpedientesCerrados: new List<string> { "401/2026" }, Guid.NewGuid(), DateTime.UtcNow, bitacora, descartes);

        Assert.Equal(1, bitacora.DescartadoExpedienteCerrado);
        Assert.Equal("EXPEDIENTE_CERRADO", Assert.Single(descartes).Razon);
    }

    [Fact]
    public void NumeroJuzgadoNoCoincide_TienePrioridadSobreExpedienteCerrado()
    {
        // Si el mismo número aparece tanto en el alcance activo (con juzgado
        // distinto) como en cerrados, la razón más específica/útil (el
        // juzgado no coincidió) gana.
        var bitacora = new BitacoraEvaluacionScraper();
        var descartes = new List<BitacoraDescarteItemizado>();
        var enAlcance = new List<Expediente> { Exp("401/2026") };

        ClasificarDescarteSinCandidato(
            "401/2026", 153, "2do Civil Hermosillo", esHermosillo: true,
            expedienteIdsFiltro: null, expedientesEnAlcance: enAlcance, todosActivos: enAlcance,
            numerosExpedientesCerrados: new List<string> { "401/2026" }, Guid.NewGuid(), DateTime.UtcNow, bitacora, descartes);

        Assert.Equal(1, bitacora.DescartadoNumeroJuzgadoNoCoincide);
        Assert.Equal(0, bitacora.DescartadoExpedienteCerrado);
    }

    [Fact]
    public void NingunCampoDeLosModelos_MencionaPartesONombre()
    {
        // Guardarraíl estructural: si alguien agrega un campo "Partes" o
        // "Nombre" a estos modelos sin querer, esta prueba truena.
        var propiedadesEvaluacion = typeof(BitacoraEvaluacionScraper).GetProperties().Select(p => p.Name);
        var propiedadesDescarte = typeof(BitacoraDescarteItemizado).GetProperties().Select(p => p.Name);

        foreach (var nombre in propiedadesEvaluacion.Concat(propiedadesDescarte))
        {
            Assert.DoesNotContain("Partes", nombre);
            Assert.DoesNotContain("NombrePart", nombre); // ej. "NombreParte"
        }
    }
}
