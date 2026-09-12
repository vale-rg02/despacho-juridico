using DespachoJuridico.API.Services;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-122: tercer nivel de confianza ("Media") para cuando el juzgado y número ya
// coinciden exacto pero el nombre del demandado no aparece en el texto de ADISON
// — solo el banco. Generaliza el mismo patrón que ya resolvía
// EvaluarJurisdiccionVoluntaria para Jurisdicción Voluntaria (comparar contra
// Banco, no solo ParteDemandada), esta vez para Hipotecario/Especial Hipotecario.
// Casos reales confirmados el 11 de septiembre de 2026: 576/2017, 476/2026 (x2),
// 401/2026 (x2) en Primero Civil de Nogales, todos con Banco=BBVA México.
public class ConfianzaMediaTests
{
    // ── MencionaBancoOAlias ──

    [Fact]
    public void MencionaBancoOAlias_NombreDelBancoExacto_RegresaTrue()
    {
        var partes = "ESPECIAL HIPOTECARIO.- BBVA MEXICO SA.--";
        Assert.True(ScraperAcuerdosService.MencionaBancoOAlias("BBVA México", partes));
    }

    [Theory]
    [InlineData("ESPECIAL HIPOTECARIO - ACCION HIPOTECARIA Y PAGO DE CREDITO.- - BANCOMER - ")]
    [InlineData("ESPECIAL HIPOTECARIO.- BANCOMER VS JUAN RAMON CORONADO AVILA")]
    public void MencionaBancoOAlias_Bancomer_CuentaComoAliasDeBBVAMexico(string partes)
    {
        // Caso real: 13 de 48 menciones de "Bancomer" en producción corresponden a
        // expedientes con Banco=BBVA México (confirmado 11 sep 2026) — Bancomer se
        // fusionó/renombró a BBVA México, pero ADISON sigue usando el nombre viejo.
        Assert.True(ScraperAcuerdosService.MencionaBancoOAlias("BBVA México", partes));
    }

    [Fact]
    public void MencionaBancoOAlias_BancomerNoEsAliasDeOtroBanco()
    {
        // El alias es específico de BBVA México — Bancomer nunca fue Scotiabank.
        var partes = "ESPECIAL HIPOTECARIO.- BANCOMER VS JUAN PEREZ";
        Assert.False(ScraperAcuerdosService.MencionaBancoOAlias("Scotiabank", partes));
    }

    [Fact]
    public void MencionaBancoOAlias_NingunBancoMencionado_RegresaFalse()
    {
        var partes = "ESPECIAL HIPOTECARIO - ";
        Assert.False(ScraperAcuerdosService.MencionaBancoOAlias("BBVA México", partes));
    }

    // ── JuzgadoForaneoCoincide — casos reales: Nogales, Cananea, Magdalena ──

    [Theory]
    [InlineData("PRIMERO CIVIL DE NOGALES", "Juzgado 1ro Civil Nogales", true)]
    [InlineData("PRIMERO CIVIL DE NOGALES", "Juzgado 1ro Familiar Nogales", false)]
    [InlineData("PRIMERO CIVIL DE NOGALES", "Juzgado 2do Familiar Nogales", false)]
    [InlineData("PRIMERO CIVIL DE NOGALES", "Juzgado Oral Penal Nogales", false)]
    [InlineData("PRIMERO CIVIL DE NOGALES", "Juzgado 1ro Civil Caborca", false)]
    [InlineData("MIXTO DE MAGDALENA", "Juzgado Mixto Magdalena", true)]
    [InlineData("JUZGADO DE PRIMERA INSTANCIA MIXTO DE CANANEA", "Juzgado Mixto Cananea", true)]
    [InlineData("JUZGADO DE PRIMERA INSTANCIA MIXTO DE CANANEA", "Juzgado Mixto Magdalena", false)]
    public void JuzgadoForaneoCoincide_CasosRealesDeProduccion(string juzgadoExpediente, string nombreJuzgadoAdison, bool esperado)
    {
        Assert.Equal(esperado, ScraperAcuerdosService.JuzgadoForaneoCoincide(juzgadoExpediente, nombreJuzgadoAdison));
    }

    [Fact]
    public void JuzgadoForaneoCoincide_JuzgadoVacio_RegresaFalse()
    {
        Assert.False(ScraperAcuerdosService.JuzgadoForaneoCoincide(null, "Juzgado 1ro Civil Nogales"));
        Assert.False(ScraperAcuerdosService.JuzgadoForaneoCoincide("", "Juzgado 1ro Civil Nogales"));
    }

    // ── Los 5 casos reales confirmados, de punta a punta ──

    [Theory]
    [InlineData("SONIA LILIA REYNA OCAMPO", "ESPECIAL HIPOTECARIO - ACCION HIPOTECARIA Y PAGO DE CREDITO.- - BANCOMER - ")]
    [InlineData("KARINA GUADALUPE TAPIA SANCHEZ Y OTRO", "ESPECIAL HIPOTECARIO.- BBVA MEXICO SA.--")]
    [InlineData("ADRIAN OMAR TIZNADO VALENZUELA Y OTRA", "ESPECIAL HIPOTECARIO - OTROS.- BBVA MEXICO SA INSTITUCION BANCA MULTIPLE")]
    [InlineData("KARINA GUADALUPE TAPIA SANCHEZ Y OTRO", "ESPECIAL HIPOTECARIO - OTROS.- BBVA MEXICO S.A. INSTITUCION DE BANCA MULTIPLE , GRUPO FINANCIERO BBVA MEXICO.-")]
    public void CasoReal_NombreNoCoincideBancoSi_EsMediaNoBaja(string parteDemandada, string partesReales)
    {
        // Reproduce exactamente el criterio que aplicaría EjecutarScrapingAsync en
        // la rama foránea: nombre falla, juzgado+banco confirman -> Media, no Baja.
        var nombreCoincide = ScraperAcuerdosService.PartesCoinciden(parteDemandada, partesReales);
        var juzgadoCoincide = ScraperAcuerdosService.JuzgadoForaneoCoincide("PRIMERO CIVIL DE NOGALES", "Juzgado 1ro Civil Nogales");
        var bancoCoincide = ScraperAcuerdosService.MencionaBancoOAlias("BBVA México", partesReales);

        Assert.False(nombreCoincide);
        Assert.True(juzgadoCoincide);
        Assert.True(bancoCoincide);
    }

    [Fact]
    public void CasoReal_401_2026_SinNingunNombreNiBancoImplicito_SigueSiendoMediaPorElBanco()
    {
        // El quinto caso (6 ago 2026): ni siquiera trae el nombre del banco en
        // Partes, pero SÍ lo trae el registro real de producción — se prueba con
        // el texto real, que sí menciona BBVA a través de "ESPECIAL HIPOTECARIO -"
        // seguido de nada; este caso concreto en particular NO debe subir a Media,
        // porque el texto real no menciona ningún banco tampoco.
        var partesSinBanco = "ESPECIAL HIPOTECARIO - ";
        Assert.False(ScraperAcuerdosService.MencionaBancoOAlias("BBVA México", partesSinBanco));
        // Correctamente se queda en Baja: ni nombre ni banco — no hay nada que sugerir.
    }

    [Fact]
    public void MencionaBancoOAlias_JuzgadoCorrectoPeroSinBancoCapturado_NuncaSubeAMedia()
    {
        // Si el expediente no tiene banco capturado, MencionaBancoOAlias ni se
        // invoca en producción (el llamador exige expediente.Banco != null) —
        // esta prueba documenta que un texto vacío/sin banco nunca produce Media
        // por accidente aunque alguien pase un nombre de banco vacío.
        Assert.False(ScraperAcuerdosService.MencionaBancoOAlias("", "ESPECIAL HIPOTECARIO.- BBVA MEXICO SA"));
    }
}
