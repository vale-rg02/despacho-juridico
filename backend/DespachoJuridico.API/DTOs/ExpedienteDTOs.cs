using DespachoJuridico.API.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace DespachoJuridico.API.DTOs;

// Lo que se muestra en el listado y detalle
public class ExpedienteResponse
{
    public int Id { get; set; }
    public string NumeroExpediente { get; set; } = string.Empty;
    public string ParteDemandada { get; set; } = string.Empty;
    public string? Juzgado { get; set; }
    public string? Materia { get; set; }
    public string? TipoJuicio { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Prioridad { get; set; } = string.Empty;
    public string? Notas { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }

    public int? BancoId { get; set; }
    public string? BancoNombre { get; set; }

    public int? UsuarioAsignadoId { get; set; }
    public string? UsuarioAsignadoNombre { get; set; }

    public int? ExpedienteRelacionadoId { get; set; }
}

// Lo que se recibe al crear un expediente
public class ExpedienteCreateRequest
{
    [Required(ErrorMessage = "El número de expediente es obligatorio")]
    [StringLength(50, ErrorMessage = "El número de expediente no puede exceder 50 caracteres")]
    public string NumeroExpediente { get; set; } = string.Empty;

    [Required(ErrorMessage = "La parte demandada es obligatoria")]
    [StringLength(200, ErrorMessage = "La parte demandada no puede exceder 200 caracteres")]
    public string ParteDemandada { get; set; } = string.Empty;

    public int? BancoId { get; set; }

    [StringLength(100)]
    public string? Juzgado { get; set; }

    [StringLength(50)]
    public string? Materia { get; set; }

    [StringLength(50)]
    public string? TipoJuicio { get; set; }

    public Prioridad Prioridad { get; set; } = Prioridad.Normal;
    public int? UsuarioAsignadoId { get; set; }
    public int? ExpedienteRelacionadoId { get; set; }
    public string? Notas { get; set; }
}

// Lo que se recibe al editar un expediente
public class ExpedienteUpdateRequest
{
    [Required(ErrorMessage = "El número de expediente es obligatorio")]
    [StringLength(50, ErrorMessage = "El número de expediente no puede exceder 50 caracteres")]
    public string NumeroExpediente { get; set; } = string.Empty;

    [Required(ErrorMessage = "La parte demandada es obligatoria")]
    [StringLength(200, ErrorMessage = "La parte demandada no puede exceder 200 caracteres")]
    public string ParteDemandada { get; set; } = string.Empty;

    public int? BancoId { get; set; }

    [StringLength(100)]
    public string? Juzgado { get; set; }

    [StringLength(50)]
    public string? Materia { get; set; }

    [StringLength(50)]
    public string? TipoJuicio { get; set; }

    public int? UsuarioAsignadoId { get; set; }
    public int? ExpedienteRelacionadoId { get; set; }
    public string? Notas { get; set; }
}

// Para cambiar solo el estado
public class CambiarEstadoRequest
{
    public EstadoExpediente Estado { get; set; }
}

// Para cambiar solo la prioridad
public class CambiarPrioridadRequest
{
    public Prioridad Prioridad { get; set; }
}

public class EtapaCatalogoCreateRequest
{
    [Required]
    public string Nombre { get; set; } = string.Empty;
    public string? TipoJuicio { get; set; }
    public int? TerminoDias { get; set; }
    public bool EsDiasHabiles { get; set; } = true;
    public int Orden { get; set; }
}