using DespachoJuridico.API.Services;
using Xunit;

namespace DespachoJuridico.Tests;

// Extensión de ReevaluarOcultosAsync (26 oct 2026): además de Media (DJ-122,
// ver ReevaluarMediaTests.cs), ahora también sube a Alta un oculto Hermosillo
// cuyo texto de ADISON no tiene NINGÚN nombre que verificar -- mismo criterio
// que la rama "sin nombre" de EjecutarScrapingAsync, aplicado retroactivamente.
// Motivado por un caso real: exp. 2336/2026 (1ro y 4to Mercantil Hermosillo) y
// 2335/2026 (3ro Mercantil Hermosillo), 22-26 oct 2026, Partes="ESPECIAL
// MERCANTIL - ACCIÓN EJECUCIÓN DE GARANTÍA.-" -- se ocultaron por error porque
// PartesTieneNombre confundía esa frase con un nombre (ya corregido en
// FrasesGenericasConocidas), pero ReevaluarOcultosAsync nunca los habría
// desocultado solo con ese fix porque no tenía ninguna rama para "no hay
// nombre que comparar".
//
// Mismo patrón que ReevaluarMediaTests.cs: ReevaluarOcultosAsync en sí usa
// IServiceScopeFactory/DbContext reales, así que aquí se reproduce la decisión
// pieza por pieza, no de punta a punta.
public class ReevaluarSinNombreTests
{
    private const string PartesSinNombre = "ESPECIAL MERCANTIL - ACCIÓN EJECUCIÓN DE GARANTÍA.-";

    [Fact]
    public void CasoReal_HermosilloSinNombre_CalificaParaAlta()
    {
        // Reproduce la rama nueva: Hermosillo, no serie auxiliar, juzgado
        // coincide, y PartesTieneNombre ya da False (con la frase agregada a
        // FrasesGenericasConocidas) -- las 4 condiciones que exige la rama.
        Assert.Contains(158, ScraperAcuerdosService.JuzgadosHermosillo); // 1ro Mercantil Hermosillo
        Assert.False(ScraperAcuerdosService.EsSerieAuxiliar(null));
        Assert.True(ScraperAcuerdosService.JuzgadoCoincide("1ro Mercantil Hermosillo", "1ro Mercantil Hermosillo"));
        Assert.False(ScraperAcuerdosService.PartesTieneNombre(PartesSinNombre));
    }

    [Fact]
    public void ConNombrePresente_PartesTieneNombreDaTrue_NoAplicaEstaRama()
    {
        // Control de regresión: si ADISON sí publicó algo que parece un nombre
        // (aunque termine no coincidiendo), esta rama nueva no debe activarse --
        // eso lo sigue decidiendo PartesCoinciden como siempre, no un trust
        // automático por ausencia de verificación.
        var partesConNombreQueNoCoincide = "ESPECIAL MERCANTIL - ACCIÓN EJECUCIÓN DE GARANTÍA.- JUAN CARLOS PEREZ LOPEZ";

        Assert.True(ScraperAcuerdosService.PartesTieneNombre(partesConNombreQueNoCoincide));
    }

    [Fact]
    public void ForaneoSinNombre_NuncaCalificaPorEstaRama()
    {
        // Control de regresión: un foráneo (IdUnidad fuera de JuzgadosHermosillo)
        // nunca debe calificar por "sin nombre" -- ahí el riesgo de número de
        // orden reutilizado entre juzgados distintos es real y ya documentado
        // (DJ-79/DJ-118), a diferencia de Hermosillo. La condición explícita de
        // la rama (!esForaneo) es lo único que evita esto -- aquí se confirma
        // que un IdUnidad foráneo típico no está en el set de Hermosillo.
        Assert.DoesNotContain(324, ScraperAcuerdosService.JuzgadosHermosillo); // Tribunal Laboral Cajeme
    }

    [Fact]
    public void SerieAuxiliarSinNombre_NuncaCalificaPorEstaRama()
    {
        // Exh./Cuad. tienen numeración propia del juzgado receptor -- coincidir
        // con el expediente es casualidad de número, no identidad real. Igual
        // que en la rama de Media, nunca debe calificar aquí tampoco.
        Assert.True(ScraperAcuerdosService.EsSerieAuxiliar("Exh."));
        Assert.True(ScraperAcuerdosService.EsSerieAuxiliar("Cuad."));
    }

    [Fact]
    public void JuzgadoYaNoCoincide_NuncaCalificaAunqueSeaHermosillo()
    {
        // A diferencia de EjecutarScrapingAsync (donde JuzgadoCoincide es una
        // precondición ya garantizada por el filtro de "candidatos" previo),
        // ReevaluarOcultosAsync procesa acuerdos que pudieron guardarse hace
        // tiempo -- si el campo Juzgado del expediente se editó desde entonces
        // y ya no coincide, esta rama no debe activarse por sí sola.
        var juzgadoCoincide = ScraperAcuerdosService.JuzgadoCoincide("2do Mercantil Hermosillo", "1ro Mercantil Hermosillo");

        Assert.False(juzgadoCoincide);
    }
}
