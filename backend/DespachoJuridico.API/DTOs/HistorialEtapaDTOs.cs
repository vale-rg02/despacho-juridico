using System.ComponentModel.DataAnnotations;

namespace DespachoJuridico.API.DTOs;

public class EtapaHistorialResponse
{
    public int Id { get; set; }
    public int? EtapaCatalogoId { get; set; }
    public string? EtapaNombre { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaLimite { get; set; }
    public DateTime? FechaCompletada { get; set; }
    public bool Atendido { get; set; }
    public List<NotaEtapaResponse> Notas { get; set; } = new();
    public string RegistradoPorNombre { get; set; } = string.Empty;
}

public class NotaEtapaResponse
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; }
    public string CreadoPorNombre { get; set; } = string.Empty;
}

public class AgregarNotaEtapaRequest
{
    [Required(ErrorMessage = "El texto de la nota es obligatorio")]
    public string Texto { get; set; } = string.Empty;
}

public class RegistrarEtapaRequest
{
    [Required(ErrorMessage = "Debes seleccionar una etapa del catálogo")]
    public int EtapaCatalogoId { get; set; }

    [Required(ErrorMessage = "La fecha de inicio es obligatoria")]
    public DateTime FechaInicio { get; set; }

    // Si viene null, se calcula automáticamente con el catálogo
    public DateTime? FechaLimite { get; set; }

    // Hora opcional; si no se manda, la fecha queda a medianoche (comportamiento anterior)
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraLimite { get; set; }

    // Nota inicial opcional -- si viene con texto, se guarda como la primera
    // NotaEtapa del historial (no como campo plano, ver RegistrarEtapa).
    public string? Notas { get; set; }
}

public class CompletarEtapaRequest
{
    public DateTime? FechaCompletada { get; set; }
}

// Historial de notas por etapa: editar una etapa ya no toca notas (eso pasó a
// ser un flujo aparte, ver AgregarNotaEtapaRequest) -- este request solo
// cubre metadata (etapa del catálogo, fechas).
public class EditarEtapaRequest
{
    [Required(ErrorMessage = "Debes seleccionar una etapa del catálogo")]
    public int EtapaCatalogoId { get; set; }

    [Required(ErrorMessage = "La fecha de inicio es obligatoria")]
    public DateTime FechaInicio { get; set; }

    public DateTime? FechaLimite { get; set; }

    // Hora opcional; si no se manda, la fecha queda a medianoche (comportamiento anterior)
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraLimite { get; set; }
}

public class AlertaResponse
{
    public int EtapaHistorialId { get; set; }
    public int ExpedienteId { get; set; }
    public string NumeroExpediente { get; set; } = string.Empty;
    public string? EtapaNombre { get; set; }
    public DateTime FechaLimite { get; set; }
    public int DiasRestantes { get; set; }
    public bool Vencida { get; set; }
    public int? UsuarioAsignadoId { get; set; }
    public string? UsuarioAsignadoNombre { get; set; }
}