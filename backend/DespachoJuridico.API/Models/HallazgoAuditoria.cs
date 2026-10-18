namespace DespachoJuridico.API.Models;

// DJ-140: una fila por hallazgo que un revisor humano registra durante la
// revisión diaria del reporte de auditoría -- S1/S2/S3 son juicio humano, no
// algo que el sistema pueda detectar solo (ver ReporteAuditoriaScraperService).
// Alimenta el conteo de días hábiles sin S1/S2/S3 de este mismo reporte y el
// registro diario que pide DJ-143 (ventana de observación) -- un solo
// registro para los dos, consultado vía GET, nunca duplicado. Nunca contiene
// Partes ni nombres -- mismo guardarraíl que BitacoraDescarteItemizado.
public class HallazgoAuditoria
{
    public int Id { get; set; }

    public DateOnly Fecha { get; set; }

    // "S1" (acuerdo real no notificado u oculto) | "S2" (notificación de un
    // caso ajeno) | "S3" (cosmético/de uso) -- definiciones de DJ-140.
    public string Severidad { get; set; } = string.Empty;

    // Corta -- ej. "oculto", "falso positivo", "cosmético". Nunca texto de Partes.
    public string Categoria { get; set; } = string.Empty;

    // 1-2 palabras, máx 80 caracteres (validado en el endpoint) -- no admite
    // nombres ni texto de Partes.
    public string Descripcion { get; set; } = string.Empty;

    public string? NumeroExpediente { get; set; }
    public string? NombreJuzgado { get; set; }

    public int RegistradoPorId { get; set; }
    public Usuario RegistradoPor { get; set; } = null!;

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
