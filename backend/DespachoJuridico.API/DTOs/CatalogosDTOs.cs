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

// DJ-105
public class CrearBancoRequest
{
    [Required] public string Nombre { get; set; } = string.Empty;
}

public class UsuarioResponse
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public string NivelAcceso { get; set; } = string.Empty;

}

// Versión reducida para usuarios sin permisos de administrador (ej. dropdown "Asignado a")
public class UsuarioBasicoResponse
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
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

    // Null = etapa de primer nivel. Con valor = subetapa (ej. "1ra Almoneda")
    // que solo debe ofrecerse dentro del submenú de su padre (ej. "Remate") — DJ-76.
    public int? EtapaPadreId { get; set; }
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
    public NivelAcceso NivelAcceso { get; set; } = NivelAcceso.Estandar;

}

public class CambiarActivoRequest
{
    public bool Activo { get; set; }
}

// DJ-112
public class SedeCatalogoResponse
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class CrearSedeRequest
{
    [Required] public string Nombre { get; set; } = string.Empty;
}

// DJ-87
public class JuzgadoCatalogoResponse
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int SedeId { get; set; }
    public string SedeNombre { get; set; } = string.Empty;
}

public class CrearJuzgadoRequest
{
    [Required] public string Nombre { get; set; } = string.Empty;
    [Required] public int SedeId { get; set; }
}

// DJ-112/DJ-87: resultado de MigracionSedeJuzgadoService -- separa lo mapeado
// con confianza de lo que quedó sin mapear (nunca forzado), para revisión
// humana antes de aplicar en firme (dryRun=false).
public class ResultadoMigracionSedeJuzgado
{
    public bool DryRun { get; set; }
    public int TotalEvaluados { get; set; }
    public List<MapeoSedeJuzgado> Mapeados { get; set; } = new();
    public List<MapeoSedeJuzgado> SinMapear { get; set; } = new();
}

public class MapeoSedeJuzgado
{
    public int ExpedienteId { get; set; }
    public string NumeroExpediente { get; set; } = string.Empty;
    public string JuzgadoOriginal { get; set; } = string.Empty;
    public string? SedeAsignada { get; set; }
    public string? JuzgadoCanonico { get; set; }
}