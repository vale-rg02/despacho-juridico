using DespachoJuridico.API.Models.Enums;

namespace DespachoJuridico.API.Models;

public class Expediente
{
    public int Id { get; set; }
    public string NumeroExpediente { get; set; } = string.Empty;
    public string ParteDemandada { get; set; } = string.Empty;
    public int? BancoId { get; set; }

    // DJ-112: municipio donde radica el expediente. Texto plano (como Materia/
    // TipoJuicio), validado contra SedeCatalogo al crear/editar -- no es FK a
    // propósito, para no tocar el código que ya lee Juzgado como texto directo
    // (correos, bitácora, matching de acuerdos). Nullable: expedientes viejos
    // migran vía MigrarSedeYJuzgadoDesdeTextoLibreAsync, y no todos podrán
    // mapearse con confianza (queda null en vez de forzar un valor).
    public string? Sede { get; set; }
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

    // Navegaci�n
    public Banco? Banco { get; set; }
    public Usuario? UsuarioAsignado { get; set; }
    public Usuario? CreadoPor { get; set; }
    public Expediente? ExpedienteRelacionado { get; set; }
    public ICollection<HistorialEtapa> Historial { get; set; } = [];
    public ICollection<Notificacion> Notificaciones { get; set; } = [];
    public ICollection<BitacoraCambio> Bitacora { get; set; } = [];
    public ICollection<ExpedienteAcceso> Accesos { get; set; } = [];
}