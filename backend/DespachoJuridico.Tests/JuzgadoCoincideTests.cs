using DespachoJuridico.API.Services;
using Xunit;

namespace DespachoJuridico.Tests;

public class JuzgadoCoincideTests
{
    // DJ-140: hallazgo real -- "Juzgado Especializado Violencia de Género
    // Hermosillo" (IdUnidad 905) estaba en JuzgadosHermosillo desde la
    // auditoría de agosto 2026 (junto con Penal/Laboral/Adolescentes/Ejecución),
    // pero nunca se le agregó su patrón en JuzgadoCoincide -- ningún acuerdo de
    // ese juzgado podía coincidir jamás, cayendo siempre en
    // NUMERO_COINCIDE_JUZGADO_NO (Cola 2b) cuando el número sí coincidía.
    [Fact]
    public void ViolenciaDeGenero_SiCoincideConsigoMisma()
    {
        Assert.True(ScraperAcuerdosService.JuzgadoCoincide(
            "Juzgado Especializado Violencia de Género Hermosillo",
            "Juzgado Especializado Violencia de Género Hermosillo"));
    }

    [Fact]
    public void ViolenciaDeGenero_ToleraSinAcento()
    {
        Assert.True(ScraperAcuerdosService.JuzgadoCoincide(
            "Juzgado Especializado Violencia de Genero Hermosillo",
            "Juzgado Especializado Violencia de Género Hermosillo"));
    }

    [Fact]
    public void ViolenciaDeGenero_NuncaCruzaConOtraMateria()
    {
        Assert.False(ScraperAcuerdosService.JuzgadoCoincide(
            "Juzgado Especializado Violencia de Género Hermosillo", "1ro Civil Hermosillo"));
    }

    // Guardarraíl de regresión: si en el futuro se agrega un juzgado nuevo a
    // JuzgadosHermosillo sin agregarle su patrón correspondiente en
    // JuzgadoCoincide (exactamente el bug de Violencia de Género), esta prueba
    // lo atrapa sola -- cada juzgado de Hermosillo debe coincidir consigo
    // mismo, como mínimo.
    [Fact]
    public void TodosLosJuzgadosDeHermosillo_CoincidenConsigoMismos()
    {
        var sinPatron = new List<string>();

        foreach (var idUnidad in ScraperAcuerdosService.JuzgadosHermosillo)
        {
            var nombre = ScraperAcuerdosService.Juzgados[idUnidad];
            if (!ScraperAcuerdosService.JuzgadoCoincide(nombre, nombre))
                sinPatron.Add($"{idUnidad}: {nombre}");
        }

        Assert.True(sinPatron.Count == 0, $"Juzgados sin patrón en JuzgadoCoincide: {string.Join(", ", sinPatron)}");
    }
}
