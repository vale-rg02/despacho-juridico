namespace DespachoJuridico.API.Models;

public class Exhorto
{
    public int Id { get; set; }
    public int ExpedienteId { get; set; }
    public string NumeroExhorto { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
    public string? Notas { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public int RegistradoPorId { get; set; }

    public Expediente Expediente { get; set; } = null!;
    public Usuario RegistradoPor { get; set; } = null!;
}
