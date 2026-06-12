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
    public string? Notas { get; set; }
    public string RegistradoPorNombre { get; set; } = string.Empty;
}

public class RegistrarEtapaRequest
{
    [Required(ErrorMessage = "Debes seleccionar una etapa del catálogo")]
    public int EtapaCatalogoId { get; set; }

    [Required(ErrorMessage = "La fecha de inicio es obligatoria")]
    public DateTime FechaInicio { get; set; }

    // Si viene null, se calcula automáticamente con el catálogo
    public DateTime? FechaLimite { get; set; }

    public string? Notas { get; set; }
}

public class CompletarEtapaRequest
{
    public DateTime? FechaCompletada { get; set; }
}