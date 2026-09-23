using DespachoJuridico.API.Services;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-112/DJ-87: pruebas de MigracionSedeJuzgadoService contra los 27 valores
// reales y distintos de Expediente.Juzgado en producción (consultados 22 sep
// 2026, 377 expedientes totales). Cubre exactamente lo que pide el criterio de
// aceptación: migración correcta sin forzar los casos ambiguos.
public class MigracionSedeJuzgadoTests
{
    // ── DetectarSedeForanea ──

    [Theory]
    [InlineData("PRIMERO CIVIL DE NOGALES", "Nogales")]
    [InlineData("JUZGADO DE PRIMERA INSTANCIA MIXTO DE CANANEA", "Cananea")]
    [InlineData("MIXTO DE MAGDALENA", "Magdalena")]
    public void DetectarSedeForanea_CasosRealesConCiudadExplicita(string juzgado, string sedeEsperada)
    {
        Assert.Equal(sedeEsperada, MigracionSedeJuzgadoService.DetectarSedeForanea(juzgado));
    }

    [Theory]
    [InlineData("1ro Oral Mercantil")]
    [InlineData("2do Oral Mecantil")]
    [InlineData("SEGUNDO ORAL DE LO MERCANTIL")]
    [InlineData("PRIMERO ORAL MERCANTIL")]
    [InlineData("Arrendamiento")]
    [InlineData("PRUEBA")]
    public void DetectarSedeForanea_SinCiudadMencionada_RegresaNull(string juzgado)
    {
        // null significa "se asume Hermosillo" -- lo decide el llamador
        // (MigrarAsync), no esta función.
        Assert.Null(MigracionSedeJuzgadoService.DetectarSedeForanea(juzgado));
    }

    // ── EncontrarJuzgadoCanonico — catálogo real de Hermosillo (subconjunto) ──

    private static readonly string[] CatalogoHermosilloMuestra =
    {
        "1ro Civil Hermosillo", "2do Civil Hermosillo", "3ro Civil Hermosillo",
        "1ro Mercantil Hermosillo",
        "1ro Oral Mercantil Hermosillo", "2do Oral Mercantil Hermosillo",
        "Arrendamiento Hermosillo",
        "2do Familiar Hermosillo", "4to Familiar Hermosillo",
    };

    [Theory]
    [InlineData("1ro Oral Mercantil", "1ro Oral Mercantil Hermosillo")] // 86 expedientes reales
    [InlineData("2do Oral Mecantil", "2do Oral Mercantil Hermosillo")] // 74 reales, typo
    [InlineData("2do Civil", "2do Civil Hermosillo")] // 35 reales
    [InlineData("3ro Civil", "3ro Civil Hermosillo")] // 35 reales
    [InlineData("1ro Civil", "1ro Civil Hermosillo")] // 33 reales
    [InlineData("SEGUNDO ORAL DE LO MERCANTIL", "2do Oral Mercantil Hermosillo")] // 30 reales, ordinal escrito
    [InlineData("PRIMERO ORAL MERCANTIL", "1ro Oral Mercantil Hermosillo")] // 28 reales, ordinal escrito
    [InlineData("2do Oral Mercantil", "2do Oral Mercantil Hermosillo")] // 6 reales
    [InlineData("TERCERO CIVIL", "3ro Civil Hermosillo")] // 3 reales
    [InlineData("PRIMERO CIVIL", "1ro Civil Hermosillo")] // 2 reales
    [InlineData("Arrendamiento", "Arrendamiento Hermosillo")] // 2 reales -- texto MÁS CORTO que el catálogo
    [InlineData("2do Familiar", "2do Familiar Hermosillo")] // 2 reales
    [InlineData("1ro Mercantil", "1ro Mercantil Hermosillo")] // 2 reales
    [InlineData("SEGUNDO CIVIL", "2do Civil Hermosillo")] // 1 real
    [InlineData("4to Familiar", "4to Familiar Hermosillo")] // 1 real
    public void EncontrarJuzgadoCanonico_CasosRealesDeHermosillo_MapeanCorrecto(string juzgadoTexto, string esperado)
    {
        Assert.Equal(esperado, MigracionSedeJuzgadoService.EncontrarJuzgadoCanonico(juzgadoTexto, CatalogoHermosilloMuestra));
    }

    // ── EncontrarJuzgadoCanonico — basura real que NO debe mapearse ──

    [Theory]
    [InlineData("11")]
    [InlineData("PRUEBA")]
    [InlineData("Prueba")]
    [InlineData("kk")]
    [InlineData("asaasa")]
    [InlineData("1")]
    [InlineData("A")]
    [InlineData("0000")]
    public void EncontrarJuzgadoCanonico_BasuraReal_NoSeFuerzaNingunMapeo(string juzgadoTexto)
    {
        Assert.Null(MigracionSedeJuzgadoService.EncontrarJuzgadoCanonico(juzgadoTexto, CatalogoHermosilloMuestra));
    }

    // ── EncontrarJuzgadoCanonico — catálogo foráneo real ──

    [Fact]
    public void EncontrarJuzgadoCanonico_PrimeroCivilDeNogales_MapeaAJuzgadoReal()
    {
        var catalogoNogales = new[]
        {
            "Juzgado 1ro Civil Nogales", "Juzgado 1ro Familiar Nogales",
            "Juzgado 2do Familiar Nogales", "Juzgado Oral Penal Nogales", "Tribunal Laboral Nogales",
        };

        Assert.Equal(
            "Juzgado 1ro Civil Nogales",
            MigracionSedeJuzgadoService.EncontrarJuzgadoCanonico("PRIMERO CIVIL DE NOGALES", catalogoNogales));
    }

    [Fact]
    public void EncontrarJuzgadoCanonico_MixtoDeMagdalena_MapeaAJuzgadoReal()
    {
        Assert.Equal(
            "Juzgado Mixto Magdalena",
            MigracionSedeJuzgadoService.EncontrarJuzgadoCanonico("MIXTO DE MAGDALENA", new[] { "Juzgado Mixto Magdalena" }));
    }

    [Fact]
    public void EncontrarJuzgadoCanonico_ListaVacia_RegresaNull()
    {
        Assert.Null(MigracionSedeJuzgadoService.EncontrarJuzgadoCanonico("1ro Civil", Array.Empty<string>()));
    }

    [Fact]
    public void EncontrarJuzgadoCanonico_CasoResidual_CananeaConTextoInterpuesto_QuedaSinMapear()
    {
        // Caso residual DOCUMENTADO, no resuelto -- encontrado corriendo esta
        // prueba contra los 27 valores reales de producción, tal como se pidió.
        // La Sede sí se detecta bien (DetectarSedeForanea encuentra "CANANEA"),
        // pero "PRIMERA INSTANCIA" interpuesto entre "JUZGADO" y "MIXTO" rompe
        // el supuesto de coincidencia por subcadena aproximada (que asume una
        // franja contigua con typos/inserciones simples, no palabras completas
        // insertadas en medio) -- el costo de edición para "saltarse" esa frase
        // completa supera el umbral. Es 1 solo expediente real; se deja sin
        // mapear (no se fuerza) en vez de complicar el algoritmo para un caso
        // único -- queda para revisión manual en el reporte de la migración.
        var candidatos = new[] { "Juzgado Mixto Cananea", "Sala Oral Penal Cananea" };

        Assert.Equal("Cananea", MigracionSedeJuzgadoService.DetectarSedeForanea(
            "JUZGADO DE PRIMERA INSTANCIA MIXTO DE CANANEA"));
        Assert.Null(MigracionSedeJuzgadoService.EncontrarJuzgadoCanonico(
            "JUZGADO DE PRIMERA INSTANCIA MIXTO DE CANANEA", candidatos));
    }
}
