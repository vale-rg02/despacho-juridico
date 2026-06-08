namespace DespachoJuridico.API.Models;

public class EtapaCatalogo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? TipoJuicio { get; set; }
    public int? TerminoDias { get; set; }
    public bool EsDiasHabiles { get; set; } = true;
    public int Orden { get; set; }

    public ICollection<HistorialEtapa> Historial { get; set; } = [];
}