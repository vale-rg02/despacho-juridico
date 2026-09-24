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

    // Historial de notas por etapa: campo viejo (una sola nota, se sobreescribía
    // en cada edición) renombrado y dejado como artefacto inerte -- la app ya no
    // lo lee ni lo escribe, solo existe para no perder el dato. La fuente de
    // verdad ahora es Notas (abajo). Ver DbSeeder.MigrarNotasEtapaLegadoAsync.
    public string? NotasLegado { get; set; }

    public Expediente Expediente { get; set; } = null!;
    public EtapaCatalogo? EtapaCatalogo { get; set; }
    public Usuario RegistradoPor { get; set; } = null!;
    public ICollection<Notificacion> Notificaciones { get; set; } = [];
    public ICollection<NotaEtapa> Notas { get; set; } = [];
}