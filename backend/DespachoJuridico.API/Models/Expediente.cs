using DespachoJuridico.API.Models.Enums;

namespace DespachoJuridico.API.Models;

public class Expediente
{
    public int Id { get; set; }
    public string NumeroExpediente { get; set; } = string.Empty;
    public string ParteDemandada { get; set; } = string.Empty;
    public int? BancoId { get; set; }
    public string? Juzgado { get; set; }
    public string? Materia { get; set; }
    public string? TipoJuicio { get; set; }
    public EstadoExpediente Estado { get; set; } = EstadoExpediente.Abierto;
    public Prioridad Prioridad { get; set; } = Prioridad.Normal;
    public int? UsuarioAsignadoId { get; set; }
    public int? ExpedienteRelacionadoId { get; set; }
    public string? Notas { get; set; }
    public string? EtapaActual { get; set; }
    public string? AccionPendiente { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    public int CreadoPorId { get; set; }

    // Navegación
    public Banco? Banco { get; set; }
    public Usuario? UsuarioAsignado { get; set; }
    public Usuario? CreadoPor { get; set; }
    public Expediente? ExpedienteRelacionado { get; set; }
    public ICollection<HistorialEtapa> Historial { get; set; } = [];
    public ICollection<Notificacion> Notificaciones { get; set; } = [];
    public ICollection<BitacoraCambio> Bitacora { get; set; } = [];
}