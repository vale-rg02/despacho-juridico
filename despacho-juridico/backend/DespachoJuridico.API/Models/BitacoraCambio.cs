namespace DespachoJuridico.API.Models;

public class BitacoraCambio
{
    public int Id { get; set; }
    public int ExpedienteId { get; set; }
    public int UsuarioId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public Expediente Expediente { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}