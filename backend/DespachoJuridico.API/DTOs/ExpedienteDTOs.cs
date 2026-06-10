using DespachoJuridico.API.Models.Enums;

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

    // Datos relacionados (aplanados para que React no tenga que navegar objetos anidados)
    public int? BancoId { get; set; }
    public string? BancoNombre { get; set; }

    public int? UsuarioAsignadoId { get; set; }
    public string? UsuarioAsignadoNombre { get; set; }

    public int? ExpedienteRelacionadoId { get; set; }
}

// Lo que se recibe al crear un expediente
public class ExpedienteCreateRequest
{
    public string NumeroExpediente { get; set; } = string.Empty;
    public string ParteDemandada { get; set; } = string.Empty;
    public int? BancoId { get; set; }
    public string? Juzgado { get; set; }
    public string? Materia { get; set; }
    public string? TipoJuicio { get; set; }
    public Prioridad Prioridad { get; set; } = Prioridad.Normal;
    public int? UsuarioAsignadoId { get; set; }
    public int? ExpedienteRelacionadoId { get; set; }
    public string? Notas { get; set; }
}

// Lo que se recibe al editar un expediente
public class ExpedienteUpdateRequest
{
    public string NumeroExpediente { get; set; } = string.Empty;
    public string ParteDemandada { get; set; } = string.Empty;
    public int? BancoId { get; set; }
    public string? Juzgado { get; set; }
    public string? Materia { get; set; }
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