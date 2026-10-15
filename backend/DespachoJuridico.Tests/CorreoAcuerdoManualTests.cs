using System.Net;
using System.Text.RegularExpressions;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-129: el correo de registro manual (DJ-108) pasó de texto plano a la misma
// plantilla visual que el correo de acuerdo detectado, y ahora distingue entre
// acuerdo y exhorto en asunto, encabezado, cuerpo y botón.
public class CorreoAcuerdoManualTests
{
    private const string BaseUrl = "https://app.acedoehijos.com";

    private static Expediente NuevoExpediente(string? juzgado = "3ro Civil Hermosillo") => new()
    {
        Id = 159,
        NumeroExpediente = "159/2025",
        ParteDemandada = "José Ñandú Peña",
        Juzgado = juzgado,
    };

    private static AcuerdoScrapeado NuevoAcuerdo(bool esExhorto, string? tipoAsunto = null, string juzgado = "1ro Civil Hermosillo") => new()
    {
        Sintesis = "Se tiene por contestada la demanda",
        FechaAcuerdo = new DateOnly(2026, 10, 9),
        NombreJuzgado = juzgado,
        EsExhorto = esExhorto,
        TipoAsunto = tipoAsunto,
        CiudadDestino = esExhorto ? "Guadalajara, Jalisco" : null,
        RegistradoManualmente = true,
    };

    // Texto que ve la persona: sin <style> ni etiquetas ni atributos (el href
    // lleva "#acuerdos", que no cuenta como texto fijo del correo).
    private static string TextoVisible(string html)
    {
        var sinEstilos = Regex.Replace(html, "<style.*?</style>", "", RegexOptions.Singleline);
        return Regex.Replace(sinEstilos, "<[^>]+>", " ");
    }

    [Fact]
    public void Acuerdo_DiceAcuerdoEnAsuntoEncabezadoCuerpoYBoton()
    {
        var (asunto, cuerpo) = CorreoAcuerdoManual.Construir("Carlos", "Mario Acedo", NuevoExpediente(), NuevoAcuerdo(false, "Contestación"), BaseUrl);

        Assert.Equal("Nuevo acuerdo registrado manualmente — Exp. 159/2025", asunto);
        Assert.Contains("<p>Acuerdo registrado manualmente</p>", cuerpo);
        Assert.Contains("registró manualmente un acuerdo en el expediente", cuerpo);
        Assert.Contains("Ver acuerdo en el expediente</a>", cuerpo);
        Assert.DoesNotContain("exhorto", TextoVisible(cuerpo), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Exhorto_DiceExhortoEnAsuntoEncabezadoCuerpoYBoton_YNingunTextoFijoDiceAcuerdo()
    {
        var (asunto, cuerpo) = CorreoAcuerdoManual.Construir("Carlos", "Mario Acedo", NuevoExpediente(), NuevoAcuerdo(true, "Exhorto (manual)"), BaseUrl);

        Assert.Equal("Nuevo exhorto registrado manualmente — Exp. 159/2025", asunto);
        Assert.Contains("<p>Exhorto registrado manualmente</p>", cuerpo);
        Assert.Contains("registró manualmente un exhorto en el expediente", cuerpo);
        Assert.Contains("Síntesis del exhorto", cuerpo);
        Assert.Contains("Fecha del exhorto", cuerpo);
        Assert.Contains("Ver exhorto en el expediente</a>", cuerpo);
        Assert.DoesNotContain("acuerdo", TextoVisible(cuerpo), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("acuerdo", asunto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Acuerdo_ConTipoDeTramite_LoMuestraEnLaTarjeta()
    {
        var (_, cuerpo) = CorreoAcuerdoManual.Construir("Carlos", "Mario", NuevoExpediente(), NuevoAcuerdo(false, "Contestación"), BaseUrl);

        Assert.Contains("<strong>Tipo de trámite:</strong> Contestación", WebUtility.HtmlDecode(cuerpo));
    }

    [Fact]
    public void Acuerdo_SinTipoDeTramite_OmiteEsaFilaSinRomperElCorreo()
    {
        var (_, cuerpo) = CorreoAcuerdoManual.Construir("Carlos", "Mario", NuevoExpediente(), NuevoAcuerdo(false, tipoAsunto: null), BaseUrl);

        Assert.DoesNotContain("Tipo de trámite", cuerpo);
        Assert.Contains("<strong>Expediente:</strong> 159/2025", cuerpo);
        Assert.Contains("<strong>Fecha del acuerdo:</strong> 09/10/2026", cuerpo);
    }

    [Fact]
    public void Exhorto_MuestraElDestinoEnLugarDelTipoDeTramite()
    {
        var (_, cuerpo) = CorreoAcuerdoManual.Construir("Carlos", "Mario", NuevoExpediente(), NuevoAcuerdo(true, "Exhorto (manual)"), BaseUrl);

        Assert.Contains("<strong>Destino:</strong> Guadalajara, Jalisco", cuerpo);
        Assert.DoesNotContain("Tipo de trámite", cuerpo);
    }

    [Fact]
    public void SinJuzgadoEnElRegistro_CaeAlJuzgadoDelExpediente()
    {
        var (_, cuerpo) = CorreoAcuerdoManual.Construir("Carlos", "Mario", NuevoExpediente("2do Familiar Hermosillo"), NuevoAcuerdo(false, "Contestación", juzgado: ""), BaseUrl);

        Assert.Contains("<strong>Juzgado:</strong> 2do Familiar Hermosillo", cuerpo);
    }

    [Fact]
    public void SinJuzgadoEnNingunLado_OmiteLaFilaDeJuzgado()
    {
        var (_, cuerpo) = CorreoAcuerdoManual.Construir("Carlos", "Mario", NuevoExpediente(juzgado: null), NuevoAcuerdo(false, "Contestación", juzgado: ""), BaseUrl);

        Assert.DoesNotContain("Juzgado:", cuerpo);
    }

    [Fact]
    public void NombresConAcentos_SeConservanEnSaludoYQuienRegistro()
    {
        var (_, cuerpo) = CorreoAcuerdoManual.Construir("María Peña", "Ángel Ñúñez", NuevoExpediente(), NuevoAcuerdo(false, "Contestación"), BaseUrl);

        // HtmlEncode escribe los acentos como entidades (&#237;): el cliente de correo
        // las muestra bien, así que se compara el texto ya decodificado.
        var visible = WebUtility.HtmlDecode(cuerpo);
        Assert.Contains("Estimado(a) María Peña,", visible);
        Assert.Contains("<strong>Ángel Ñúñez</strong> registró manualmente", visible);
        Assert.Contains("<strong>Parte demandada:</strong> José Ñandú Peña", visible);
    }

    [Fact]
    public void TextoDeUsuario_SeEscapaComoHtml()
    {
        var acuerdo = NuevoAcuerdo(false, "Contestación");
        acuerdo.Sintesis = "<script>alert('x')</script> & más";

        var (_, cuerpo) = CorreoAcuerdoManual.Construir("<b>Carlos</b>", "Mario", NuevoExpediente(), acuerdo, BaseUrl);

        Assert.DoesNotContain("<script>", cuerpo);
        Assert.Contains("&lt;script&gt;", cuerpo);
        Assert.DoesNotContain("<b>Carlos</b>", cuerpo);
    }

    [Fact]
    public void DejaClaroQueFueManualYSinVerificacion_SinPedirConfirmar()
    {
        var (_, cuerpo) = CorreoAcuerdoManual.Construir("Carlos", "Mario Acedo", NuevoExpediente(), NuevoAcuerdo(false, "Contestación"), BaseUrl);

        Assert.Contains("<strong>Mario Acedo</strong>", cuerpo);
        Assert.Contains("no lo detectó ni lo ha verificado automáticamente", cuerpo);
        // Distinto del correo de posible acuerdo (Media), que sí pide decidir.
        Assert.DoesNotContain("Confirmar", cuerpo);
        Assert.DoesNotContain("Descartar", cuerpo);
    }

    [Fact]
    public void QuienRegistroSinNombre_UsaUnTextoNeutro()
    {
        var (_, cuerpo) = CorreoAcuerdoManual.Construir("Carlos", null, NuevoExpediente(), NuevoAcuerdo(false, "Contestación"), BaseUrl);

        Assert.Contains("<strong>Un compañero</strong> registró manualmente", WebUtility.HtmlDecode(cuerpo));
    }

    [Fact]
    public void ElBoton_UsaElMismoEnlaceDirectoAAcuerdosQueLosDemasCorreos()
    {
        var (_, cuerpo) = CorreoAcuerdoManual.Construir("Carlos", "Mario", NuevoExpediente(), NuevoAcuerdo(false, "Contestación"), BaseUrl);

        Assert.Contains($"<a href='{ScraperAcuerdosService.ConstruirUrlAcuerdo(BaseUrl, 159)}'>", cuerpo);
    }

    [Fact]
    public void UsaLaPlantillaCompartida_ConEncabezadoNavyBotonYFirma()
    {
        var (_, cuerpo) = CorreoAcuerdoManual.Construir("Carlos", "Mario", NuevoExpediente(), NuevoAcuerdo(false, "Contestación"), BaseUrl);

        Assert.Contains(PlantillaCorreo.Estilos, cuerpo);
        Assert.Contains("<h1>Despacho Jurídico Acedo e Hijos</h1>", cuerpo);
        Assert.Contains("class='highlight'", cuerpo);
        Assert.Contains("class='sintesis'", cuerpo);
        Assert.Contains("class='cta'", cuerpo);
        Assert.Contains("Atentamente,<br><strong>Despacho Jurídico Acedo e Hijos</strong>", cuerpo);
        Assert.Contains("<html lang='es'>", cuerpo);
        Assert.Contains("name='viewport'", cuerpo);
    }
}
