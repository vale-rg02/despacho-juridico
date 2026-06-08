namespace DespachoJuridico.API.Models;

public class Banco
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public ICollection<Expediente> Expedientes { get; set; } = [];
}