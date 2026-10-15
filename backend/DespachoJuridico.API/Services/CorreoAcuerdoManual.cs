using System.Net;
using DespachoJuridico.API.Models;

namespace DespachoJuridico.API.Services;

// DJ-129: correo que se manda a los colaboradores cuando alguien registra a mano
// un acuerdo o un exhorto (DJ-108). Mismo armazón visual que el correo de
// acuerdo detectado (PlantillaCorreo), pero deja claro que lo capturó un
// compañero -- no el scraper -- y que no hay verificación automática. A
// diferencia del correo de posible acuerdo (Media), no pide confirmar nada.
internal static class CorreoAcuerdoManual
{
    internal static (string Asunto, string Cuerpo) Construir(
        string destinatarioNombre,
        string? actorNombre,
        Expediente expediente,
        AcuerdoScrapeado acuerdo,
        string frontendBaseUrl)
    {
        // Ambos son masculinos ("Nuevo acuerdo" / "Nuevo exhorto"), así que solo
        // cambia el sustantivo -- ningún otro texto fijo debe mencionarlo.
        var tipo = acuerdo.EsExhorto ? "exhorto" : "acuerdo";
        var tipoCapitalizado = acuerdo.EsExhorto ? "Exhorto" : "Acuerdo";

        var asunto = $"Nuevo {tipo} registrado manualmente — Exp. {expediente.NumeroExpediente}";

        var nombreEnc = WebUtility.HtmlEncode(destinatarioNombre);
        var actorEnc = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(actorNombre) ? "Un compañero" : actorNombre);
        var numeroEnc = WebUtility.HtmlEncode(expediente.NumeroExpediente);
        var demandadaEnc = WebUtility.HtmlEncode(expediente.ParteDemandada);
        var sintesisEnc = WebUtility.HtmlEncode(acuerdo.Sintesis);
        var url = WebUtility.HtmlEncode(ScraperAcuerdosService.ConstruirUrlAcuerdo(frontendBaseUrl, expediente.Id));

        // El registro manual puede no capturar juzgado -- se cae al del expediente.
        var juzgado = !string.IsNullOrWhiteSpace(acuerdo.NombreJuzgado) ? acuerdo.NombreJuzgado : expediente.Juzgado;

        var filas = new List<string>
        {
            Fila("Expediente", numeroEnc),
            Fila("Parte demandada", demandadaEnc),
        };
        if (!string.IsNullOrWhiteSpace(juzgado))
            filas.Add(Fila("Juzgado", WebUtility.HtmlEncode(juzgado)));
        filas.Add(Fila($"Fecha del {tipo}", $"{acuerdo.FechaAcuerdo:dd/MM/yyyy}"));

        // Un exhorto guarda "Exhorto (manual)" como TipoAsunto -- ya lo dice el
        // título, así que ahí lo útil es el destino, no repetir el tipo.
        if (acuerdo.EsExhorto)
        {
            if (!string.IsNullOrWhiteSpace(acuerdo.CiudadDestino))
                filas.Add(Fila("Destino", WebUtility.HtmlEncode(acuerdo.CiudadDestino)));
        }
        else if (!string.IsNullOrWhiteSpace(acuerdo.TipoAsunto))
        {
            filas.Add(Fila("Tipo de trámite", WebUtility.HtmlEncode(acuerdo.TipoAsunto)));
        }

        var contenido = $@"
    <p>Estimado(a) {nombreEnc},</p>
    <p><strong>{actorEnc}</strong> registró manualmente un {tipo} en el expediente <strong>{numeroEnc}</strong>:</p>
    <div class='highlight'>
      {string.Join("\n      ", filas)}
    </div>
    <p><strong>Síntesis del {tipo}:</strong></p>
    <div class='sintesis'>{sintesisEnc}</div>
    <div class='aviso'>Este registro lo capturó a mano un compañero; el sistema no lo detectó ni lo ha verificado automáticamente. No necesita confirmación, pero conviene revisarlo en el expediente.</div>
    <div class='cta'><a href='{url}'>Ver {tipo} en el expediente</a></div>";

        var cuerpo = PlantillaCorreo.Armar($"{tipoCapitalizado} registrado manualmente", contenido);
        return (asunto, cuerpo);
    }

    private static string Fila(string etiqueta, string valorYaCodificado) =>
        $"<p><strong>{etiqueta}:</strong> {valorYaCodificado}</p>";
}
