using DespachoJuridico.API.Services;
using Xunit;

namespace DespachoJuridico.Tests;

public class PartesCoincidenTests
{
    [Theory]
    [InlineData("---", false)]
    [InlineData(".-", false)]
    [InlineData("ESPECIAL HIPOTECARIO -", false)]
    [InlineData("ORAL MERCANTIL -", false)]
    [InlineData("SUCESORIO INTESTAMENTARIO.-", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("ESPECIAL HIPOTECARIO - ACCIÓN PERSONAL Y REAL.- BBVA BANCOMER, S.A. VS JUAN RAMON CORONADO AVILA", true)]
    [InlineData("SUCESORIO INTESTAMENTARIO.- JESUS IGNACIO VILLA GRACIA", true)]
    public void PartesTieneNombre_DetectaCorrectamente(string? partes, bool esperado)
    {
        Assert.Equal(esperado, ScraperAcuerdosService.PartesTieneNombre(partes ?? string.Empty));
    }

    [Fact]
    public void PartesCoinciden_CoincidenciaExacta_RegresaTrue()
    {
        var partes = "ORAL MERCANTIL - ACCIÓN PAGO DE PESOS.- BBVA MEXICO, S.A.  VS MELISSA LEON LORTA";

        Assert.True(ScraperAcuerdosService.PartesCoinciden("MELISSA LEON LORTA", partes));
    }

    [Fact]
    public void PartesCoinciden_VariacionDeOrtografiaDentroDelUmbral_RegresaTrue()
    {
        // Caso real encontrado el 20 de agosto 2026, exp. 127/2023: ADISON publicó
        // "CORONADO", el despacho tiene capturado "CORONA" — antes de este cambio,
        // esto quedaba oculto como falso negativo.
        var partes = "ESPECIAL HIPOTECARIO.- BBVA BANCOMER VS JUAN RAMON CORONADO AVILA";

        Assert.True(ScraperAcuerdosService.PartesCoinciden("JUAN RAMON CORONA AVILA", partes, umbralSimilitud: 0.8));
    }

    [Fact]
    public void PartesCoinciden_NombreCompletamenteDistinto_RegresaFalse()
    {
        // Caso real del mismo día, exp. 150/2023: coincidencia de número con un
        // caso ajeno — debe seguir rechazándose aunque se relaje el umbral.
        var partes = "SUMARIO CIVIL - ACCIÓN PAGO DE HONORARIOS.- ISMAEL DE JESUS CASTRO OQUITA VS INFONAVIT";

        Assert.False(ScraperAcuerdosService.PartesCoinciden("Roberto Sánchez Mena", partes, umbralSimilitud: 0.8));
    }

    [Fact]
    public void PartesTieneNombre_CasoReal150_2023_DetectaQueSiHayNombreParaVerificar()
    {
        // Confirma que, con la extensión a Hermosillo, este caso real (que antes
        // se guardaba sin ninguna verificación) ahora sí entraría a PartesCoinciden
        // en vez de confiarse ciegamente en número+juzgado.
        var partes = "SUMARIO CIVIL - ACCIÓN PAGO DE HONORARIOS.- ISMAEL DE JESUS CASTRO OQUITA VS INFONAVIT";

        Assert.True(ScraperAcuerdosService.PartesTieneNombre(partes));
        Assert.False(ScraperAcuerdosService.PartesCoinciden("Roberto Sánchez Mena", partes));
    }

    [Fact]
    public void PartesCoinciden_UmbralMasAlto_YaNoAceptaLaVariacionDeOrtografia()
    {
        // Con un umbral más estricto (95%), la variación "Corona"/"Coronado" ya
        // no debe pasar — confirma que el parámetro sí es efectivo, no decorativo.
        var partes = "ESPECIAL HIPOTECARIO.- BBVA BANCOMER VS JUAN RAMON CORONADO AVILA";

        Assert.False(ScraperAcuerdosService.PartesCoinciden("JUAN RAMON CORONA AVILA", partes, umbralSimilitud: 0.95));
    }

    [Fact]
    public void PartesCoinciden_ApellidosComunesEnPersonasDistintas_RegresaFalse()
    {
        // Caso real del 20 de agosto 2026, exp. 57/2026: "IBARRA" y "OCHOA"
        // aparecen en el texto, pero pegados a dos personas distintas de un
        // divorcio ajeno — no deben combinarse para simular el nombre buscado.
        var partes = "DIVORCIO INCAUSADO.- EMILIA CUEVAS IBARRA VS JOSE ANGEL OCHOA ESQUER";

        Assert.False(ScraperAcuerdosService.PartesCoinciden("RICARDO IBARRA OCHOA", partes, umbralSimilitud: 0.8));
    }

    [Fact]
    public void PartesCoinciden_FragmentoCortoComunLILIANAvsANA_RegresaFalse()
    {
        // Caso real del mismo día, exp. 149/2026: "ANA" es substring de
        // "LILIANA" por coincidencia — no debe contar como que el nombre
        // "Ana Sofia Pesqueira Enriquez" aparece en el texto.
        var partes = "EJECUTIVO MERCANTIL - ACCIÓN CAMBIARIA DIRECTA.- JOSET CEBREROS ROLDAN VS LILIANA PATRICIA LOPEZ ACOSTA";

        Assert.False(ScraperAcuerdosService.PartesCoinciden("Christian Emmanuel Ramos Vidrios y Ana Sofia Pesqueira Enriquez", partes, umbralSimilitud: 0.8));
    }

    [Fact]
    public void PartesCoinciden_SinNombreEnPartes_RegresaFalse()
    {
        var partes = "ORAL MERCANTIL -";

        Assert.False(ScraperAcuerdosService.PartesCoinciden("GERARDO ORTEGA MARTINEZ Y OTRA", partes));
    }

    [Theory]
    [InlineData("MARIA GONZALEZ", "MARIA GONZALEZ", 1.0)]
    [InlineData("MARIA GONZALEZ", "MARIA GONZALES", 13.0 / 14)] // 1 letra distinta de 14
    [InlineData("ABC", "XYZ", 0.0)]
    public void Similitud_CasosBasicos(string a, string b, double esperado)
    {
        Assert.Equal(esperado, ScraperAcuerdosService.Similitud(a, b), precision: 4);
    }

    [Fact]
    public void SimilitudMaximaSubcadena_EncuentraElMejorFragmento()
    {
        // "CORONA AVILA" (12) debe encontrar su mejor coincidencia en el fragmento
        // "CORONADO AVILA" recortado a 12 caracteres, no en el resto del texto.
        var texto = "BBVA BANCOMER VS JUAN RAMON CORONADO AVILA";
        var patron = "CORONA AVILA";

        var similitud = ScraperAcuerdosService.SimilitudMaximaSubcadena(patron, texto);

        Assert.True(similitud > 0.8, $"Se esperaba > 0.8, se obtuvo {similitud}");
    }

    [Theory]
    [InlineData("Jurisdiccion Voluntaria", true)]
    [InlineData("Jurisdicción Voluntaria", true)]
    [InlineData("JURISDICCION VOLUNTARIA", true)]
    [InlineData("  jurisdiccion   voluntaria  ", true)]
    [InlineData("Ejecutivo Mercantil", false)]
    [InlineData("Especial Hipotecario", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EsJurisdiccionVoluntaria_ToleraMayusculasYAcentos(string? tipoJuicio, bool esperado)
    {
        Assert.Equal(esperado, ScraperAcuerdosService.EsJurisdiccionVoluntaria(tipoJuicio));
    }

    [Fact]
    public void EvaluarJurisdiccionVoluntaria_SoloBancoCoincide_ConfianzaAltaYVisible()
    {
        // Caso real: exp. 434/2026, Mario/BBVA — el texto de ADISON en la radicación
        // solo nombra al banco promovente, nunca a la parte capturada como demandada.
        var partes = "JURISDICCIÓN VOLUNTARIA CIVIL - OTROS.- BBVA MEXICO SA INSTITUCION DE BANCA MULTILPLE GRUPO FINANCIERO BBVA MEXICO";

        var (confianza, oculto) = ScraperAcuerdosService.EvaluarJurisdiccionVoluntaria("Patricia Yanet Contreras Martinez", "BBVA México", partes);

        Assert.Equal("Alta", confianza);
        Assert.False(oculto);
    }

    [Fact]
    public void EvaluarJurisdiccionVoluntaria_SoloParteDemandadaCoincide_ConfianzaAltaYVisible()
    {
        // Cuando ADISON sí trae el nombre de la parte (no siempre pasa lo de 434/2026),
        // ese nombre por sí solo también debe bastar, aunque el banco no aparezca.
        var partes = "JURISDICCIÓN VOLUNTARIA CIVIL - NOTIFICACIÓN JUDICIAL.- SE NOTIFICA A PATRICIA YANET CONTRERAS MARTINEZ";

        var (confianza, oculto) = ScraperAcuerdosService.EvaluarJurisdiccionVoluntaria("Patricia Yanet Contreras Martinez", "BBVA México", partes);

        Assert.Equal("Alta", confianza);
        Assert.False(oculto);
    }

    [Fact]
    public void EvaluarJurisdiccionVoluntaria_NingunoCoincide_ConfianzaBajaYOculto()
    {
        // Caso real que motivó este ajuste: exp. 368/2026, colisión de número con un
        // caso de concubinato totalmente ajeno en Cajeme — ni la parte demandada
        // (Juan Pablo Valle Jimenez) ni el banco (BBVA México) aparecen en el texto.
        // Con la versión anterior (Oculto siempre false) esto se notificó por error.
        var partes = "ACREDITACIÓN DE HECHOS DE CONCUBINATO - ACREDITAR CONCUBINATO.- GILBERTA ELISA LUCERO CARRIZOZA";

        var (confianza, oculto) = ScraperAcuerdosService.EvaluarJurisdiccionVoluntaria("Juan Pablo Valle Jimenez Y Otra", "BBVA México", partes);

        Assert.Equal("Baja", confianza);
        Assert.True(oculto);
    }

    [Fact]
    public void EvaluarJurisdiccionVoluntaria_SinBancoCapturado_UsaSoloParteDemandada()
    {
        var partes = "JURISDICCIÓN VOLUNTARIA CIVIL - NOTIFICACIÓN JUDICIAL.- SE NOTIFICA A PATRICIA YANET CONTRERAS MARTINEZ";

        var (confianza, oculto) = ScraperAcuerdosService.EvaluarJurisdiccionVoluntaria("Patricia Yanet Contreras Martinez", null, partes);

        Assert.Equal("Alta", confianza);
        Assert.False(oculto);
    }

    [Fact]
    public void PartesCoinciden_TipoJuicioNormal_SigueUsandoParteDemandadaSinTocarBanco()
    {
        // Confirma que la rama de Jurisdicción Voluntaria no reemplaza el criterio
        // normal para el resto de tipos de juicio: sigue comparando contra
        // ParteDemandada exactamente igual que antes de este cambio.
        var partes = "ORAL MERCANTIL - ACCIÓN PAGO DE PESOS.- BBVA MEXICO, S.A.  VS MELISSA LEON LORTA";

        Assert.True(ScraperAcuerdosService.PartesCoinciden("MELISSA LEON LORTA", partes));
    }

    [Theory]
    [InlineData("1ro Penal", "1ro Penal Hermosillo", true)]
    [InlineData("1ro Penal", "Juzgado Oral Penal Hermosillo", false)] // oral no cruza con no-oral
    [InlineData("2do Penal", "2do Penal Hermosillo", true)]
    [InlineData("Juzgado Oral Penal", "Juzgado Oral Penal Hermosillo", true)]
    [InlineData("Juzgado Ejecución de Sanciones", "Juzgado Ejecución de Sanciones Hermosillo", true)]
    [InlineData("1er Tribunal Laboral", "1er Tribunal Laboral Hermosillo", true)]
    [InlineData("2do Tribunal Laboral", "3er Tribunal Laboral Hermosillo", false)]
    [InlineData("Juzgado Adolescentes", "Juzgado Adolescentes Hermosillo", true)]
    [InlineData("Juzgado Adolescentes", "Tribunal Unitario Regional Adolescentes/Penal Oral Hermosillo", false)]
    [InlineData("Tribunal Unitario Regional Adolescentes", "Tribunal Unitario Regional Adolescentes/Penal Oral Hermosillo", true)]
    public void JuzgadoCoincide_RamasNuevasPenalLaboralAdolescentesEjecucion(string juzgadoDespacho, string juzgadoAdison, bool esperado)
    {
        Assert.Equal(esperado, ScraperAcuerdosService.JuzgadoCoincide(juzgadoDespacho, juzgadoAdison));
    }

    [Theory]
    [InlineData("1ro Civil", "1er Tribunal Laboral Hermosillo")]
    [InlineData("1ro Oral Mercantil", "Juzgado Ejecución de Sanciones Hermosillo")]
    [InlineData("3ro Civil", "1ro Penal Hermosillo")]
    public void JuzgadoCoincide_MateriaCivilMercantilNuncaCruzaConPenalLaboral(string juzgadoDespacho, string juzgadoAdison)
    {
        // Caso real que motivó extender JuzgadoCoincide a estas ramas: expedientes
        // Civil/Mercantil del despacho coincidían por número con juzgados Laboral/Penal
        // de Hermosillo que antes no tenían patrón propio y pasaban por la ruta
        // foránea — el 100% de esos matches históricos eran falsos positivos.
        Assert.False(ScraperAcuerdosService.JuzgadoCoincide(juzgadoDespacho, juzgadoAdison));
    }
}
