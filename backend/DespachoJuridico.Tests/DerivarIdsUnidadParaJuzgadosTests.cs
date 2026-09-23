using DespachoJuridico.API.Services;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-102: DerivarIdsUnidadParaJuzgados mapea el texto libre de Expediente.Juzgado
// a los idUnidad del catálogo ADISON que un litigante necesita consultar para
// actualizar sus propios expedientes -- pura y sin BD/HTTP.
public class DerivarIdsUnidadParaJuzgadosTests
{
    [Fact]
    public void JuzgadoDeHermosillo_DerivaElIdUnidadCorrecto()
    {
        var resultado = ScraperAcuerdosService.DerivarIdsUnidadParaJuzgados(new[] { "1ro Civil Hermosillo" });

        Assert.Contains(152, resultado);
    }

    [Fact]
    public void JuzgadoForaneo_DerivaElIdUnidadCorrecto()
    {
        // Mismo texto libre real usado en ConfirmarAcuerdoTests, coincide con
        // "Juzgado 1ro Civil Nogales" (idUnidad 188) vía JuzgadoForaneoCoincide.
        var resultado = ScraperAcuerdosService.DerivarIdsUnidadParaJuzgados(new[] { "PRIMERO CIVIL DE NOGALES" });

        Assert.Contains(188, resultado);
    }

    [Fact]
    public void TextoVacioONulo_NoAgregaNada()
    {
        var resultado = ScraperAcuerdosService.DerivarIdsUnidadParaJuzgados(new[] { "", "   ", null });

        Assert.Empty(resultado);
    }

    [Fact]
    public void SinExpedientes_RegresaConjuntoVacio()
    {
        var resultado = ScraperAcuerdosService.DerivarIdsUnidadParaJuzgados(Array.Empty<string?>());

        Assert.Empty(resultado);
    }

    [Fact]
    public void DosExpedientesConJuzgadosDistintos_AcumulaAmbosSinDuplicar()
    {
        var resultado = ScraperAcuerdosService.DerivarIdsUnidadParaJuzgados(new[]
        {
            "1ro Civil Hermosillo",
            "PRIMERO CIVIL DE NOGALES",
            "1ro Civil Hermosillo", // repetido -- no debe duplicar
        });

        Assert.Equal(new HashSet<int> { 152, 188 }, resultado);
    }

    [Fact]
    public void JuzgadoQueNoCoincideConNada_NoAgregaNada()
    {
        var resultado = ScraperAcuerdosService.DerivarIdsUnidadParaJuzgados(new[] { "texto que no existe en ningún catálogo" });

        Assert.Empty(resultado);
    }
}
