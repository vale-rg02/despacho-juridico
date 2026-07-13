namespace DespachoJuridico.API.Models;

public class Cita
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public int? ExpedienteId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public DateTime FechaHora { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;
    public Expediente? Expediente { get; set; }
}
