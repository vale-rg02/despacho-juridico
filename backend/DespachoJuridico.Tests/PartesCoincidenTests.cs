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
    [InlineData("SEGUNDO ORAL DE LO MERCANTIL", "2do Oral Mercantil Hermosillo", true)]
    [InlineData("Segundo Oral de la Mercantil", "2do Oral Mercantil Hermosillo", true)] // variante "de la", no solo "de lo"
    [InlineData("segundo   oral   de lo   mercantil", "2do Oral Mercantil Hermosillo", true)] // espacios de más alrededor del relleno
    [InlineData("SEGUNDO ORAL DE LO MERCANTIL", "1ro Oral Mercantil Hermosillo", false)] // sigue sin cruzar 1ro con 2do
    public void JuzgadoCoincide_QuitaRellenoDeLoDeLa(string juzgadoDespacho, string juzgadoAdison, bool esperado)
    {
        // Caso real, 1 de septiembre de 2026: 28 expedientes activos de Mario tenían
        // el juzgado capturado como "SEGUNDO ORAL DE LO MERCANTIL" — el "DE LO" de
        // más rompía el match contra "2do Oral Mercantil Hermosillo" en ADISON, así
        // que ningún acuerdo de ese juzgado se guardaba nunca (ni siquiera oculto):
        // el reporte de Mario fue justo por dos de esos 28 (534/2025 y 242/2026),
        // ambos con acuerdos reales publicados ese día que nunca llegaron a notificarse.
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

    [Theory]
    [InlineData("Exh.", true)]
    [InlineData("exh.", true)]
    [InlineData("Cuad.", true)]
    [InlineData("cuad.", true)]
    [InlineData("Exp.", false)]
    [InlineData("C.P.", false)]
    [InlineData("Cadol.", false)]
    // Abreviaturas encontradas pero sin trato especial por decisión deliberada — no
    // se identificó un beneficio claro que justifique tratarlas distinto todavía
    // (ver docs/mecanica-legal-sonora.md #2). Si el despacho confirma su significado
    // y se decide darles trato especial, este test debe actualizarse junto con el código.
    [InlineData("Toca", false)]
    [InlineData("Leg.", false)]
    [InlineData("Amp.", false)]
    [InlineData("J.Amp.", false)]
    [InlineData("Pre.", false)]
    [InlineData("Req.", false)]
    [InlineData("EXP. C.", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EsSerieAuxiliar_DetectaExhortoYCuadernilloUnicamente(string? tipoAsunto, bool esperado)
    {
        Assert.Equal(esperado, ScraperAcuerdosService.EsSerieAuxiliar(tipoAsunto));
    }

    // ── DJ-79: varios nombres unidos por "Y" cuando ADISON usa comas ──

    [Fact]
    public void PartesCoinciden_DJ79_YvsComaMismoOrden_RegresaTrue()
    {
        var partes = "ORAL MERCANTIL - ACCIÓN PAGO.- BBVA VS Juan Perez, Maria Lopez";
        Assert.True(ScraperAcuerdosService.PartesCoinciden("Juan Perez Y Maria Lopez", partes));
    }

    [Fact]
    public void PartesCoinciden_DJ79_YvsComaOrdenInvertido_RegresaTrue()
    {
        // Caso real que motivó DJ-79: con el criterio anterior (comparar el texto
        // completo de un jalón) invertir el orden hacía caer la similitud de 0.92
        // a 0.58 y perdía el match aunque fueran las mismas dos personas.
        var partes = "ORAL MERCANTIL - ACCIÓN PAGO.- BBVA VS Maria Lopez, Juan Perez";
        Assert.True(ScraperAcuerdosService.PartesCoinciden("Juan Perez Y Maria Lopez", partes));
    }

    [Fact]
    public void PartesCoinciden_DJ79_TresNombresConComas_RegresaTrue()
    {
        var partes = "ORAL MERCANTIL - ACCIÓN PAGO.- BBVA VS Pedro Ramirez, Sofia Cruz, Luis Ortiz";
        Assert.True(ScraperAcuerdosService.PartesCoinciden("Pedro Ramirez Y Sofia Cruz Y Luis Ortiz", partes));
    }

    [Fact]
    public void PartesCoinciden_DJ79_YOtraConSegundoNombreRealSeparadoPorComaEnADISON_RegresaTrue()
    {
        // Combina el sufijo genérico "Y Otra" (QuitarSufijoOtroDemandado, sin
        // cambios) con el bug de separador: el segundo nombre real sí aparece en
        // ADISON, separado por coma.
        var partes = "ORAL MERCANTIL - ACCIÓN PAGO.- BBVA VS Gerardo Ortega Martinez, Maria Elena Sosa";
        Assert.True(ScraperAcuerdosService.PartesCoinciden("Gerardo Ortega Martinez Y Otra", partes));
    }

    [Fact]
    public void PartesCoinciden_DJ79_SegundoNombreCortoUnidoPorY_NoSeCuelaPorFragmentoCorto()
    {
        // "Ana" sola haría match trivial (substring exacto de "LILIANA") si se
        // confiara en cualquier fragmento suelto — se exige 2+ palabras.
        var partes = "EJECUTIVO MERCANTIL - ACCIÓN CAMBIARIA DIRECTA.- JOSET CEBREROS ROLDAN VS LILIANA PATRICIA LOPEZ ACOSTA";
        Assert.False(ScraperAcuerdosService.PartesCoinciden("Christian Emmanuel Ramos Vidrios Y Ana", partes, umbralSimilitud: 0.8));
    }

    [Fact]
    public void PartesCoinciden_DJ79_ApellidoSueltoUnidoPorY_NoSeCuelaPorFragmentoCorto()
    {
        var partes = "DIVORCIO INCAUSADO.- EMILIA CUEVAS IBARRA VS JOSE ANGEL OCHOA ESQUER";
        Assert.False(ScraperAcuerdosService.PartesCoinciden("Ricardo Martinez Solano Y Ochoa", partes, umbralSimilitud: 0.8));
    }

    [Fact]
    public void PartesCoinciden_DJ79_AmbosNombresDeUnaSolaPalabra_CaeAComparacionDeTextoCompleto()
    {
        // Ningún fragmento tiene 2+ palabras -> cae a comparar todo el texto
        // unido, igual que antes de DJ-79 (no mejora este caso, pero tampoco
        // empeora: un nombre de pila suelto nunca es confiable por sí solo).
        var partesQueSiCoincide = "ORAL MERCANTIL - ACCIÓN PAGO.- BBVA VS Ana, Luis";
        var partesQueNoCoincide = "ORAL MERCANTIL - ACCIÓN PAGO.- BBVA VS Pedro, Rosa";

        Assert.True(ScraperAcuerdosService.PartesCoinciden("Ana Y Luis", partesQueSiCoincide, umbralSimilitud: 0.8));
        Assert.False(ScraperAcuerdosService.PartesCoinciden("Ana Y Luis", partesQueNoCoincide, umbralSimilitud: 0.8));
    }

    // 19 ParteDemandada reales de producción con dos nombres completos unidos por
    // "Y" (de 74 expedientes reales que usan este patrón) — validación con datos
    // reales, no solo casos construidos a mano.
    private static readonly string[] NombresRealesDJ79 =
    {
        "José Alfredo Morales Ivich Y Dulce Maria Enrique Ramirez",
        "Ruben Bustamante Moran Y Dulce Judith Saavedra Moreno",
        "Luis Antonio Aldana Peraza Y Marisa Galaz Ramos",
        "Rey David Yañez Murrieta Y Estibalis Valdez Quintero",
        "Rosrigo Zazueta Alcantar y Reyna Elizabeth Quintero Valdez",
        "Carlos Alejandro Cordova Nuñez Y Karla Maria López Quintana",
        "Luis Raúl Siller Montaño y Helen Alicia Galvez Diaz",
        "Aarón Alejandro Molina Corona Y Melissa Figueroa Valdez",
        "Mario Luis Gallegos Prieto Y Sandra Iliana Ibarra Sanchez Alvarez",
        "Francisco Alfonso Galaz Martinez Y Maria Laura Reyes Villanueva",
        "Francisco Rascón Madrid Y Josefina Rascon Madrid",
        "Jesús Ernesto Anaya García Y Luz Elena Gonzalez Vazquez",
        "Luz Minerva Payan Moteya Y Julio Cesar Gonzalez Duarte",
        "Ramon Koinoor Cadia Covarrubias Y Reina Modesta Arvizu Peralta",
        "Alejandro Moreno Torres y Sara María Contreras",
        "Juan Pablo MC Laurien Castillón Y Rocío Ortega Gonzalez",
        "Avelina Cota Marquez Y Melissa Figueroa Valdez",
        "Carlos David Montijo Juvera y Gloria Lorena Vejar Ramirez",
        "Luis Alberto Miranda Cota Y Guadalupe Leon Salazar",
    };

    // 19 textos reales de Partes (AcuerdosScrapeados de producción), sin ninguna
    // relación con los nombres de arriba — distractores para medir falsos positivos.
    private static readonly string[] DistractoresRealesDJ79 =
    {
        "ORAL MERCANTIL - ACCIÓN PAGO DE PESOS.- BBVA MEXICO, S.A.",
        "ORDINARIO FAMILIAR - NULIDAD DE ACTO JURIDICO.",
        "ACREDITACIÓN DE HECHOS DE IDENTIDAD - ACREDITAR IDENTIDAD.- PEDRO SAENZ MORALES",
        "ORDINARIO - INDEMNIZACIÓN CONSTITUCIONAL.- ALMA ANGELICA ARANDA BOJORQUEZ VS CSCP, S.A. DE C.V.",
        "ORAL MERCANTIL .- AQUA DUX SA DE CV  VS SECRETO",
        "ORAL MERCANTIL - ACCIÓN PAGO DE PESOS.- BBVA MEXICO, S.A.  VS HUGO ALBERTO BORGO HERNANDEZ",
        "SUCESORIO TESTAMENTARIO.- LUZ MARIA LOPEZ FRANCO.",
        "JURISDICCIÓN VOLUNTARIA CIVIL - ACCIÓN DECLARATIVO DE PROPIEDAD.- KARLA FERNANDA VALENZUELA MARTINEZ",
        "ORAL MERCANTIL - FINVAY, S.A. DE C.V. SOFOM ENR.  VS HERMINIA HERNANDEZ URQUIJO, M. YSABEL GUADALUPE SOLIZ PERAZA",
        "DIVORCIO INCAUSADO.- RICARDO RODRIGUEZ PEREZ VS ANTONIA SOMOZA ZORRILLA",
        "EJECUTIVO MERCANTIL - PROMOVIDO POR JOSE RAMON BOJORQUEZ APODACA VS EDNA PATRICIA CALDERON GRAJEDA, JOSE LUIS CALDERON LOPEZ",
        "KARLA GUADALUPE PADILLA ORTEGA.",
        "ESPECIAL HIPOTECARIO.- BANCOMER VS ANGELICA FAUSTO RENDON, MIGUEL ANGEL BOJORQUEZ MORENO",
        "SUMARIO CIVIL -- MARIA ICELA MORENO CELAYA VS INMOBILIARIA EL CRESTON S.A",
        "DIVORCIO INCAUSADO.- FELIZARDO BARRON CORONADO VS ALBA ELIZABETH SOSA DELGADO",
        "ORDINARIO - INDEMNIZACIÓN CONSTITUCIONAL.- ROSA VICTORIA LOPEZ ARMENTA VS GRUPO MORSA DE MEXICO, S.A. DE C.V.",
        "EJECUTIVO MERCANTIL - ACCIÓN CAMBIARIA DIRECTA.- KEVIN GERARDO AYALA LLANEZ",
        "ORDINARIO CIVIL .- LUZ MARÍA PARRA CECEÑA VS CLAUDIA AMPARO BURRUEL QUINTERO.-",
        "ORAL FAMILIAR - ORAL DE ALIMENTOS.- YESENIA EKATHERINE SOSA AGUILAR, JOSE ABRAHAM CUETO REYES",
    };

    private static (string nombre1, string nombre2) DividirEnDosDJ79(string nombreCompleto)
    {
        var partes = System.Text.RegularExpressions.Regex.Split(nombreCompleto, @"\s+[Yy]\s+");
        return (partes[0].Trim(), partes[1].Trim());
    }

    [Fact]
    public void PartesCoinciden_DJ79_NombresRealesConYVsComaEnADISON_CoincidenSinImportarOrden()
    {
        foreach (var nombre in NombresRealesDJ79)
        {
            var (n1, n2) = DividirEnDosDJ79(nombre);
            var mismoOrden = $"ORAL MERCANTIL - ACCIÓN PAGO DE PESOS.- BBVA MEXICO S.A. VS {n1}, {n2}";
            var ordenInvertido = $"ORAL MERCANTIL - ACCIÓN PAGO DE PESOS.- BBVA MEXICO S.A. VS {n2}, {n1}";

            Assert.True(ScraperAcuerdosService.PartesCoinciden(nombre, mismoOrden), $"Falló mismo orden: {nombre}");
            Assert.True(ScraperAcuerdosService.PartesCoinciden(nombre, ordenInvertido), $"Falló orden invertido: {nombre}");
        }
    }

    [Fact]
    public void PartesCoinciden_DJ79_NombresRealesContraDistractoresReales_NoDanFalsoPositivo()
    {
        for (var i = 0; i < NombresRealesDJ79.Length; i++)
        {
            var distractor = DistractoresRealesDJ79[i % DistractoresRealesDJ79.Length];
            Assert.False(ScraperAcuerdosService.PartesCoinciden(NombresRealesDJ79[i], distractor),
                $"Falso positivo: '{NombresRealesDJ79[i]}' vs '{distractor}'");
        }
    }

    [Fact]
    public void EsSerieAuxiliar_CasoReal476_2026_ConfirmaTipoAsuntoExhorto()
    {
        // Caso real del 28 de agosto de 2026: el acuerdo "476/2026" del Juzgado Oral
        // Penal de San Luis Río Colorado coincidió por número con un expediente del
        // despacho. Al revisar el dato crudo de ADISON, su TipoAsunto real es "Exh." —
        // es el número del exhorto en el sistema de ese juzgado, no el expediente
        // original. Ese juzgado ya era foráneo (así que ya pasaba por verificación de
        // Partes), pero confirma el riesgo general que EsSerieAuxiliar corrige: si el
        // mismo patrón (TipoAsunto="Exh."/"Cuad.") ocurriera en un juzgado de
        // Hermosillo, antes de este ajuste se habría confiado solo por número+juzgado,
        // sin verificar Partes — ver docs/mecanica-legal-sonora.md #2.
        Assert.True(ScraperAcuerdosService.EsSerieAuxiliar("Exh."));

        var partesPublicadas = "En cuadernillo formado con motivo del exhorto 719/2026. Se devuelve diligenciado.";
        Assert.False(ScraperAcuerdosService.PartesCoinciden("Alguna Parte Demandada Real", partesPublicadas));
    }
}
