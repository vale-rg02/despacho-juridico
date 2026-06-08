using DespachoJuridico.API.Models.Enums;

namespace DespachoJuridico.API.Models;

public class Notificacion
{
    public int Id { get; set; }
    public int ExpedienteId { get; set; }
    public int? HistorialEtapaId { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public CanalNotificacion Canal { get; set; }
    public string? DestinatarioEmail { get; set; }
    public bool Enviada { get; set; } = false;
    public DateTime? FechaEnvio { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public Expediente Expediente { get; set; } = null!;
    public HistorialEtapa? HistorialEtapa { get; set; }
}