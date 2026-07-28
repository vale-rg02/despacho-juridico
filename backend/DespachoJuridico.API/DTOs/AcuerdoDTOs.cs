using System.ComponentModel.DataAnnotations;

namespace DespachoJuridico.API.DTOs;

public class AcuerdoResponse
{
    public int Id { get; set; }
    public string NumeroExpediente { get; set; } = string.Empty;
    public string NombreJuzgado { get; set; } = string.Empty;
    public string Partes { get; set; } = string.Empty;
    public string Sintesis { get; set; } = string.Empty;
    public DateOnly FechaAcuerdo { get; set; }
    public DateTime FechaDetectado { get; set; }
    public bool NotificacionEnviada { get; set; }
    public bool Visto { get; set; }
    public bool EsExhorto { get; set; }
    public string? CiudadDestino { get; set; }
}

public class ActualizarDestinoExhortoRequest
{
    [Required(ErrorMessage = "La ciudad destino es obligatoria")]
    [StringLength(150)]
    public string CiudadDestino { get; set; } = string.Empty;
}
