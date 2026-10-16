namespace DespachoJuridico.API.Models;

// DJ-139: una fila por descarte "interesante" (no por el descarte masivo de
// casos ajenos al despacho, ver DescartadoSinCandidato en
// BitacoraEvaluacionScraper). Nunca contiene Partes ni nombres de partes --
// solo número de expediente, juzgado y razón, que es justo lo que las reglas
// de esta historia permiten citar.
public class BitacoraDescarteItemizado
{
    public int Id { get; set; }

    public Guid CorridaId { get; set; }
    public DateTime FechaHoraCorrida { get; set; } = DateTime.UtcNow;
    public int IdUnidad { get; set; }
    public string NombreJuzgado { get; set; } = string.Empty;
    public string NumeroExpediente { get; set; } = string.Empty;

    // "NUMERO_COINCIDE_JUZGADO_NO" | "EXPEDIENTE_CERRADO" |
    // "FUERA_DE_ALCANCE_FILTRO" | "EXCEPCION"
    public string Razon { get; set; } = string.Empty;

    // Solo cuando Razon="EXCEPCION": "antes_de_guardar" | "notificacion" --
    // distingue si el acuerdo ya se había guardado cuando falló, sin cambiar
    // qué pasa con el acuerdo ni con la notificación (ver
    // docs/propuestas-tecnicas.md #7 para el hueco de resiliencia relacionado,
    // fuera de alcance de esta historia).
    public string? EtapaFalla { get; set; }
}
