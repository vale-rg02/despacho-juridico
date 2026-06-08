namespace DespachoJuridico.API.Models;

public class HistorialEtapa
{
    public int Id { get; set; }
    public int ExpedienteId { get; set; }
    public int? EtapaCatalogoId { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaLimite { get; set; }
    public DateTime? FechaCompletada { get; set; }
    public bool Atendido { get; set; } = false;
    public int RegistradoPorId { get; set; }
    public string? Notas { get; set; }

    public Expediente Expediente { get; set; } = null!;
    public EtapaCatalogo? EtapaCatalogo { get; set; }
    public Usuario RegistradoPor { get; set; } = null!;
    public ICollection<Notificacion> Notificaciones { get; set; } = [];
}