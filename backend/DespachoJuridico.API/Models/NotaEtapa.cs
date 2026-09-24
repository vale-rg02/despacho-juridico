namespace DespachoJuridico.API.Models;

// Historial de notas por etapa (varias en el tiempo, no una que se
// sobreescribe). La nota que ya existía en HistorialEtapa.NotasLegado antes
// de este cambio se migra como la primera fila (ver DbSeeder.MigrarNotasEtapaLegadoAsync).
public class NotaEtapa
{
    public int Id { get; set; }
    public int HistorialEtapaId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public int CreadoPorId { get; set; }

    public HistorialEtapa HistorialEtapa { get; set; } = null!;
    public Usuario CreadoPor { get; set; } = null!;
}
