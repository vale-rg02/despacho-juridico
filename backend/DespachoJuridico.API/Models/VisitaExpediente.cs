namespace DespachoJuridico.API.Models;

public class VisitaExpediente
{
    public int Id { get; set; }
    public int ExpedienteId { get; set; }
    public int UsuarioId { get; set; }
    public DateTime FechaVisita { get; set; } = DateTime.UtcNow;

    public Expediente Expediente { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}