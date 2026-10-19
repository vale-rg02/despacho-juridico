using DespachoJuridico.API.Services;
using Microsoft.Extensions.Configuration;
using Xunit;
using static DespachoJuridico.API.Services.FrasesGenericasCandidatasService;

namespace DespachoJuridico.Tests;

// DJ-133 (implementa el diseño de Parte 2 de DJ-131 -- ver
// docs/investigacion-frases-genericas.md). Estas pruebas cubren CalcularCandidatos,
// que es el núcleo de decisión 100% puro de la clase (sin EF, sin IO) -- la
// consulta/scoping con EF vive en GenerarReporteAsync y se verifica aparte con
// datos reales (ver verificación manual de la historia), igual que
// ReevaluarOcultosAsync.
public class FrasesGenericasCandidatasServiceTests
{
    private static AcuerdoParaCandidato Fila(
        string numeroExpediente, string nombreJuzgado, string candidato, DateTime? fecha = null,
        int? bancoId = null, string? nombreBanco = null) => new(
        numeroExpediente, nombreJuzgado, candidato, fecha ?? new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        bancoId, nombreBanco);

    [Fact]
    public void DosExpedientesDistintos_NoDisparaElUmbralDeTres()
    {
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("100/2026", "1ro Civil Hermosillo", "GARANTIA HIPOTECARIA NUEVA"),
            Fila("200/2026", "2do Civil Hermosillo", "GARANTIA HIPOTECARIA NUEVA")
        };

        var resultado = CalcularCandidatos(filas, Array.Empty<string>(), umbralExpedientes: 3);

        Assert.Empty(resultado.CandidatosNuevos);
    }

    [Fact]
    public void TresExpedientesDistintos_SiDisparaElUmbralDeTres()
    {
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("100/2026", "1ro Civil Hermosillo", "GARANTIA HIPOTECARIA NUEVA"),
            Fila("200/2026", "2do Civil Hermosillo", "GARANTIA HIPOTECARIA NUEVA"),
            Fila("300/2026", "3ro Civil Hermosillo", "GARANTIA HIPOTECARIA NUEVA")
        };

        var resultado = CalcularCandidatos(filas, Array.Empty<string>(), umbralExpedientes: 3);

        var candidato = Assert.Single(resultado.CandidatosNuevos);
        Assert.Equal("GARANTIA HIPOTECARIA NUEVA", candidato.Frase);
        Assert.Equal(3, candidato.ExpedientesDistintos);
    }

    [Fact]
    public void MismoNumeroEnDosJuzgadosDistintos_CuentaComoDosExpedientesDistintos()
    {
        // Caso real de DJ-130/DJ-131: 363/2026 existe en los dos juzgados Oral
        // Mercantil de Hermosillo -- el número solo no basta para deduplicar.
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("363/2026", "1ro Oral Mercantil Hermosillo", "ACCION NUEVA SIN APROBAR"),
            Fila("363/2026", "2do Oral Mercantil Hermosillo", "ACCION NUEVA SIN APROBAR"),
            Fila("500/2026", "1ro Oral Mercantil Hermosillo", "ACCION NUEVA SIN APROBAR")
        };

        var resultado = CalcularCandidatos(filas, Array.Empty<string>(), umbralExpedientes: 3);

        var candidato = Assert.Single(resultado.CandidatosNuevos);
        Assert.Equal(3, candidato.ExpedientesDistintos);
    }

    [Fact]
    public void MismoNumeroYMismoJuzgadoRepetido_NoInflaElConteo()
    {
        // Dos acuerdos distintos del mismo expediente+juzgado (ej. dos fechas) no
        // deben contarse como dos expedientes distintos.
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("363/2026", "1ro Oral Mercantil Hermosillo", "ACCION NUEVA SIN APROBAR", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)),
            Fila("363/2026", "1ro Oral Mercantil Hermosillo", "ACCION NUEVA SIN APROBAR", new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc)),
            Fila("500/2026", "1ro Oral Mercantil Hermosillo", "ACCION NUEVA SIN APROBAR")
        };

        var resultado = CalcularCandidatos(filas, Array.Empty<string>(), umbralExpedientes: 3);

        Assert.Empty(resultado.CandidatosNuevos);
    }

    [Fact]
    public void FraseYaEnFrasesGenericasConocidas_SeExcluyeYSeCuenta()
    {
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("100/2026", "1ro Civil Hermosillo", "JUICIO ORAL"),
            Fila("200/2026", "2do Civil Hermosillo", "JUICIO ORAL"),
            Fila("300/2026", "3ro Civil Hermosillo", "JUICIO ORAL")
        };

        var resultado = CalcularCandidatos(filas, ScraperAcuerdosService.FrasesGenericasConocidas, umbralExpedientes: 3);

        Assert.Empty(resultado.CandidatosNuevos);
        Assert.Equal(1, resultado.ExcluidosPorYaAprobada);
    }

    [Fact]
    public void CandidatoConUnSoloBanco_SeEtiquetaComoPosibleNombreDeBanco()
    {
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("100/2026", "1ro Civil Hermosillo", "BANCO EXTRA S.A.", bancoId: 1, nombreBanco: "Banco Extra"),
            Fila("200/2026", "2do Civil Hermosillo", "BANCO EXTRA S.A.", bancoId: 1, nombreBanco: "Banco Extra"),
            Fila("300/2026", "3ro Civil Hermosillo", "BANCO EXTRA S.A.", bancoId: 1, nombreBanco: "Banco Extra")
        };

        var resultado = CalcularCandidatos(filas, Array.Empty<string>(), umbralExpedientes: 3);

        var candidato = Assert.Single(resultado.CandidatosNuevos);
        Assert.True(candidato.PosibleNombreDeBanco);
        Assert.Equal(1, candidato.BancosDistintos);
        Assert.Equal(new[] { "Banco Extra" }, candidato.NombresBanco);
    }

    [Fact]
    public void CandidatoConVariosBancosDistintos_NoSeEtiquetaComoBancoUnico()
    {
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("100/2026", "1ro Civil Hermosillo", "GARANTIA GENERICA", bancoId: 1, nombreBanco: "Banco A"),
            Fila("200/2026", "2do Civil Hermosillo", "GARANTIA GENERICA", bancoId: 2, nombreBanco: "Banco B"),
            Fila("300/2026", "3ro Civil Hermosillo", "GARANTIA GENERICA", bancoId: 3, nombreBanco: "Banco C")
        };

        var resultado = CalcularCandidatos(filas, Array.Empty<string>(), umbralExpedientes: 3);

        var candidato = Assert.Single(resultado.CandidatosNuevos);
        Assert.False(candidato.PosibleNombreDeBanco);
        Assert.Equal(3, candidato.BancosDistintos);
    }

    // DJ-137: candidato ya revisado y descartado (nombre real, no jerga) -- no
    // debe repetirse en el reporte. Cadena inventada.
    [Fact]
    public void CandidatoYaDescartado_SeExcluyeYSeCuenta()
    {
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("100/2026", "1ro Civil Hermosillo", "FRASE DE PRUEBA INVENTADA"),
            Fila("200/2026", "2do Civil Hermosillo", "FRASE DE PRUEBA INVENTADA"),
            Fila("300/2026", "3ro Civil Hermosillo", "FRASE DE PRUEBA INVENTADA")
        };

        var resultado = CalcularCandidatos(
            filas, Array.Empty<string>(), new[] { "FRASE DE PRUEBA INVENTADA" }, umbralExpedientes: 3);

        Assert.Empty(resultado.CandidatosNuevos);
        Assert.Equal(1, resultado.ExcluidosPorYaDescartado);
    }

    [Fact]
    public void CandidatoSinRevisar_NoAfectaElContadorDeDescartados()
    {
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("100/2026", "1ro Civil Hermosillo", "OTRA FRASE INVENTADA"),
            Fila("200/2026", "2do Civil Hermosillo", "OTRA FRASE INVENTADA"),
            Fila("300/2026", "3ro Civil Hermosillo", "OTRA FRASE INVENTADA")
        };

        var resultado = CalcularCandidatos(filas, Array.Empty<string>(), Array.Empty<string>(), umbralExpedientes: 3);

        Assert.Single(resultado.CandidatosNuevos);
        Assert.Equal(0, resultado.ExcluidosPorYaDescartado);
    }

    // DJ-137: si por algún motivo un candidato terminara marcado como aprobado Y
    // descartado a la vez (no debería pasar -- el endpoint lo valida), Aprobado
    // gana -- se cuenta como ExcluidosPorYaAprobada, no como descartado.
    [Fact]
    public void CandidatoAprobadoYDescartadoALaVez_GanaElAprobado()
    {
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("100/2026", "1ro Civil Hermosillo", "FRASE AMBIGUA INVENTADA"),
            Fila("200/2026", "2do Civil Hermosillo", "FRASE AMBIGUA INVENTADA"),
            Fila("300/2026", "3ro Civil Hermosillo", "FRASE AMBIGUA INVENTADA")
        };

        var resultado = CalcularCandidatos(
            filas, new[] { "FRASE AMBIGUA INVENTADA" }, new[] { "FRASE AMBIGUA INVENTADA" }, umbralExpedientes: 3);

        Assert.Empty(resultado.CandidatosNuevos);
        Assert.Equal(1, resultado.ExcluidosPorYaAprobada);
        Assert.Equal(0, resultado.ExcluidosPorYaDescartado);
    }

    [Fact]
    public void CandidatoDeUnaSolaPalabra_SeExcluyeDeCandidatosNuevosYSeReportaAparte()
    {
        // DJ-134: "GARANTIA" sola, sin más palabras -- nunca se mezcla con
        // CandidatosNuevos (no pasó por el mismo filtro que un candidato de
        // 2+ palabras), pero sí se reporta en CandidatosPalabraUnica para
        // revisión manual del equipo técnico.
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("100/2026", "1ro Civil Hermosillo", "GARANTIA"),
            Fila("200/2026", "2do Civil Hermosillo", "GARANTIA"),
            Fila("300/2026", "3ro Civil Hermosillo", "GARANTIA")
        };

        var resultado = CalcularCandidatos(filas, Array.Empty<string>(), umbralExpedientes: 3);

        Assert.Empty(resultado.CandidatosNuevos);
        Assert.Equal(1, resultado.ExcluidosPorPalabraUnica);
        var candidato = Assert.Single(resultado.CandidatosPalabraUnica);
        Assert.Equal("GARANTIA", candidato.Frase);
        Assert.Equal(3, candidato.ExpedientesDistintos);
    }

    [Fact]
    public void CandidatoDeDosPalabras_NoSeExcluyePorPalabraUnica()
    {
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("100/2026", "1ro Civil Hermosillo", "ACCION CAUSAL NUEVA"),
            Fila("200/2026", "2do Civil Hermosillo", "ACCION CAUSAL NUEVA"),
            Fila("300/2026", "3ro Civil Hermosillo", "ACCION CAUSAL NUEVA")
        };

        var resultado = CalcularCandidatos(filas, Array.Empty<string>(), umbralExpedientes: 3);

        Assert.Single(resultado.CandidatosNuevos);
        Assert.Equal(0, resultado.ExcluidosPorPalabraUnica);
        Assert.Empty(resultado.CandidatosPalabraUnica);
    }

    [Fact]
    public void CandidatoDeUnaSolaPalabraYaDescartado_SeExcluyeDeAmbasListas()
    {
        // DJ-134 + DJ-137: una palabra suelta ya revisada y marcada "Descartado"
        // (vía POST /api/scraper/frases-genericas/revisiones) no debe reaparecer
        // en CandidatosPalabraUnica -- el chequeo de yaDescartados corre ANTES
        // de la rama de EsPalabraUnica (ver CalcularCandidatos), así que ni
        // siquiera debe sumar a ExcluidosPorPalabraUnica.
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("100/2026", "1ro Civil Hermosillo", "GARANTIA"),
            Fila("200/2026", "2do Civil Hermosillo", "GARANTIA"),
            Fila("300/2026", "3ro Civil Hermosillo", "GARANTIA")
        };

        var resultado = CalcularCandidatos(
            filas, Array.Empty<string>(), new[] { "GARANTIA" }, umbralExpedientes: 3);

        Assert.Empty(resultado.CandidatosNuevos);
        Assert.Empty(resultado.CandidatosPalabraUnica);
        Assert.Equal(1, resultado.ExcluidosPorYaDescartado);
        Assert.Equal(0, resultado.ExcluidosPorPalabraUnica);
    }

    [Fact]
    public void NoHayCandidatos_NoSeArmaNadaQueEnviar()
    {
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("100/2026", "1ro Civil Hermosillo", "ALGO SIN REPETIR")
        };

        var resultado = CalcularCandidatos(filas, Array.Empty<string>(), umbralExpedientes: 3);

        Assert.Empty(resultado.CandidatosNuevos);
    }

    [Theory]
    [InlineData("acción causal nueva", "ACCION CAUSAL NUEVA")]
    [InlineData("  ACCIÓN   CAUSAL   NUEVA  ", "ACCION CAUSAL NUEVA")]
    [InlineData("Acción Causal Nueva.", "ACCION CAUSAL NUEVA")]
    [InlineData("ACCIÓN CAUSAL NUEVA,", "ACCION CAUSAL NUEVA")]
    public void NormalizarCandidato_QuitaAcentosMayusculasEspaciosYPuntuacionFinal(string crudo, string esperado)
    {
        Assert.Equal(esperado, FrasesGenericasCandidatasService.NormalizarCandidato(crudo));
    }

    [Fact]
    public void EsPalabraUnica_UnaSolaPalabraEsTrueDosOMasEsFalse()
    {
        Assert.True(EsPalabraUnica("GARANTIA"));
        Assert.False(EsPalabraUnica("GARANTIA HIPOTECARIA"));
    }

    // DJ-137 (Paso 1 -- evidencia, no supuesto): CalcularCandidatos no tiene ningún
    // concepto de "ya revisado" ni ventana de tiempo -- es puro y recalcula desde cero
    // cada vez. Un candidato DESCARTADO (nombre real que coincidió por casualidad, no
    // agregado a FrasesGenericasConocidas) reaparece exactamente igual en una segunda
    // corrida, como si nunca se hubiera revisado. Cadena inventada, no texto real de
    // ningún candidato real del despacho.
    [Fact]
    public void SinRegistroDeDescartados_UnCandidatoDescartadoReapareceEnLaSiguienteCorrida()
    {
        var filas = new List<AcuerdoParaCandidato>
        {
            Fila("100/2026", "1ro Civil Hermosillo", "FRASE DE PRUEBA INVENTADA"),
            Fila("200/2026", "2do Civil Hermosillo", "FRASE DE PRUEBA INVENTADA"),
            Fila("300/2026", "3ro Civil Hermosillo", "FRASE DE PRUEBA INVENTADA")
        };

        // "Corrida 1": alguien lo revisa y decide que es un nombre real (lo descarta) --
        // pero como no hay ningún lugar donde registrar esa decisión, no hay nada que
        // pasarle a la "Corrida 2" distinto de lo que ya se le pasó a la Corrida 1.
        var corrida1 = CalcularCandidatos(filas, ScraperAcuerdosService.FrasesGenericasConocidas, umbralExpedientes: 3);
        var corrida2 = CalcularCandidatos(filas, ScraperAcuerdosService.FrasesGenericasConocidas, umbralExpedientes: 3);

        Assert.Single(corrida1.CandidatosNuevos);
        Assert.Single(corrida2.CandidatosNuevos);
        Assert.Equal(corrida1.CandidatosNuevos[0].Frase, corrida2.CandidatosNuevos[0].Frase);
    }

    [Fact]
    public void ObtenerDestinatarios_SinConfigurar_RegresaListaVacia()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Empty(FrasesGenericasCandidatasService.ObtenerDestinatarios(config));
    }

    [Fact]
    public void ObtenerDestinatarios_ConfiguradaComoListaSeparadaPorComas_SeParseaCorrectamente()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FrasesGenericas:Destinatarios"] = "a@equipo.com, b@equipo.com ,c@equipo.com"
            })
            .Build();

        var destinatarios = FrasesGenericasCandidatasService.ObtenerDestinatarios(config);

        Assert.Equal(new[] { "a@equipo.com", "b@equipo.com", "c@equipo.com" }, destinatarios);
    }
}
