using System.ComponentModel.DataAnnotations;

namespace DespachoJuridico.API.DTOs;

public class ExhortoResponse
{
    public int Id { get; set; }
    public string NumeroExhorto { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
    public string? Notas { get; set; }
    public DateTime CreadoEn { get; set; }
    public string RegistradoPorNombre { get; set; } = string.Empty;
}

public class CrearExhortoRequest
{
    [Required(ErrorMessage = "El número de exhorto es obligatorio")]
    public string NumeroExhorto { get; set; } = string.Empty;

    [Required(ErrorMessage = "La ciudad es obligatoria")]
    public string Ciudad { get; set; } = string.Empty;

    public string? Notas { get; set; }
}

public class EditarExhortoRequest
{
    [Required(ErrorMessage = "El número de exhorto es obligatorio")]
    public string NumeroExhorto { get; set; } = string.Empty;

    [Required(ErrorMessage = "La ciudad es obligatoria")]
    public string Ciudad { get; set; } = string.Empty;

    public string? Notas { get; set; }
}
