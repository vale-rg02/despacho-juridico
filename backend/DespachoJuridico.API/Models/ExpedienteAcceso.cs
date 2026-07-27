namespace DespachoJuridico.API.Models;

public class ExpedienteAcceso
{
    public int Id { get; set; }
    public int ExpedienteId { get; set; }
    public int UsuarioId { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public Expediente Expediente { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}
