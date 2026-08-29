namespace DespachoJuridico.API.Models;

public class EtapaCatalogo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? TipoJuicio { get; set; }
    public int? TerminoDias { get; set; }
    public bool EsDiasHabiles { get; set; } = true;
    public int Orden { get; set; }

    // Autoreferencia para etapas con submenú (ej. Remate → 1ra/2da/3ra Almoneda,
    // DJ-76). Null = etapa de primer nivel, seleccionable directo. Con valor =
    // subetapa que solo aparece dentro del submenú de su padre. HistorialEtapa
    // siempre apunta a la hoja específica (ej. "1ra Almoneda"), nunca al padre —
    // así que reparentar una etapa existente no afecta ningún registro histórico.
    public int? EtapaPadreId { get; set; }
    public EtapaCatalogo? EtapaPadre { get; set; }
    public ICollection<EtapaCatalogo> Subetapas { get; set; } = [];

    public ICollection<HistorialEtapa> Historial { get; set; } = [];
}