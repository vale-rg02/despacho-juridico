using DespachoJuridico.API.Models.Enums;

namespace DespachoJuridico.API.Models;

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; } = RolUsuario.Litigante;
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public ICollection<Expediente> ExpedientesAsignados { get; set; } = [];
    public ICollection<Expediente> ExpedientesCreados { get; set; } = [];
    public ICollection<HistorialEtapa> HistorialRegistrado { get; set; } = [];
    public ICollection<BitacoraCambio> Bitacora { get; set; } = [];
}