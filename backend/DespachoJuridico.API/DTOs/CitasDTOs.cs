using System.ComponentModel.DataAnnotations;

namespace DespachoJuridico.API.DTOs;

public class CitaResponse
{
    public int Id { get; set; }
    public int? ExpedienteId { get; set; }
    public string? NumeroExpediente { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public DateTime FechaHora { get; set; }
}

public class CrearCitaRequest
{
    [Required] public string Titulo { get; set; } = string.Empty;
    public int? ExpedienteId { get; set; }
    public string? Descripcion { get; set; }
    [Required] public DateTime FechaHora { get; set; }
}

public class EditarCitaRequest
{
    [Required] public string Titulo { get; set; } = string.Empty;
    public int? ExpedienteId { get; set; }
    public string? Descripcion { get; set; }
    [Required] public DateTime FechaHora { get; set; }
}
