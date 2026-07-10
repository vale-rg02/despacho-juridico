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

    public Expediente Expediente { get; set; } = null!;
}