using DespachoJuridico.API.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace DespachoJuridico.API.DTOs;

public class BancoResponse
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
}

public class UsuarioResponse
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; }

}

public class BitacoraResponse
{
    public int Id { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public DateTime Fecha { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
}

public class EtapaCatalogoResponse
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? TipoJuicio { get; set; }
    public int? TerminoDias { get; set; }
    public bool EsDiasHabiles { get; set; }
    public int Orden { get; set; }
}


public class CrearUsuarioRequest
{
    [Required] public string Nombre { get; set; } = string.Empty;
    [Required] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; } = RolUsuario.Litigante;
}

public class EditarUsuarioRequest
{
    [Required] public string Nombre { get; set; } = string.Empty;
    [Required] public string Email { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; } = RolUsuario.Litigante;
}

public class CambiarActivoRequest
{
    public bool Activo { get; set; }
}