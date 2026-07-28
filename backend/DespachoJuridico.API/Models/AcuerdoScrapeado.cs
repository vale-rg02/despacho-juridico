namespace DespachoJuridico.API.Models;

public class AcuerdoScrapeado
{
    public int Id { get; set; }
    public int ExpedienteId { get; set; }
    public string NumeroExpediente { get; set; } = string.Empty;
    public int IdUnidad { get; set; }
    public string NombreJuzgado { get; set; } = string.Empty;
    public string Partes { get; set; } = string.Empty;
    public string Sintesis { get; set; } = string.Empty;
    public DateOnly FechaAcuerdo { get; set; }
    public DateTime FechaDetectado { get; set; } = DateTime.UtcNow;
    public bool NotificacionEnviada { get; set; } = false;

    public bool Visto { get; set; } = false;

    // Clasificación cruda que trae ADISON (ej. "Exp.", "Exh.", "Amp.", "Toca")
    public string? TipoAsunto { get; set; }

    // true cuando la síntesis del acuerdo menciona un exhorto (envío/recepción/
    // diligenciación); ADISON no expone el estado/ciudad destino, así que ese
    // dato se captura manualmente en CiudadDestino
    public bool EsExhorto { get; set; } = false;
    public string? CiudadDestino { get; set; }

    // true cuando el usuario lo capturó a mano (el scraper no lo detectó);
    // solo estos registros se pueden eliminar desde la UI
    public bool RegistradoManualmente { get; set; } = false;

    public Expediente Expediente { get; set; } = null!;
}