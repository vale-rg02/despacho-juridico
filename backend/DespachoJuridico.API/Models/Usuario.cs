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

    public NivelAcceso NivelAcceso { get; set; } = NivelAcceso.Estandar;

    public bool EsCuentaSoporte { get; set; } = false;

    // DJ-102: estado del botón "Actualizar expedientes" (scraper manual acotado a
    // los expedientes propios del litigante). ScraperIniciadoEn permite tratar un
    // ScraperEnProgreso=true "viejo" (>10 min) como obsoleto si el proceso murió a
    // medio de una corrida (ej. redeploy de Railway) sin dejar al usuario bloqueado
    // para siempre -- ver EvaluarCooldown en ScraperController.
    public bool ScraperEnProgreso { get; set; } = false;
    public DateTime? ScraperIniciadoEn { get; set; }
    public DateTime? UltimaConsultaScraperEn { get; set; }

    public ICollection<Expediente> ExpedientesAsignados { get; set; } = [];
    public ICollection<Expediente> ExpedientesCreados { get; set; } = [];
    public ICollection<HistorialEtapa> HistorialRegistrado { get; set; } = [];
    public ICollection<BitacoraCambio> Bitacora { get; set; } = [];
}