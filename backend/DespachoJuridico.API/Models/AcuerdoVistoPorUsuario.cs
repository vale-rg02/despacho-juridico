namespace DespachoJuridico.API.Models;

// Hallazgo docs/hallazgo-visto-global-acuerdos.md: "visto" es un estado por
// usuario, no un flag compartido del acuerdo -- antes AcuerdosScrapeados.Visto
// era un solo booleano, así que el primer colaborador que abría la página del
// expediente lo marcaba visto para TODOS los demás con acceso, aunque nunca lo
// hubieran visto. Una fila aquí = este usuario ya vio este acuerdo; su ausencia
// = no visto para él, sin afectar a nadie más.
public class AcuerdoVistoPorUsuario
{
    public int Id { get; set; }
    public int AcuerdoId { get; set; }
    public int UsuarioId { get; set; }
    public DateTime FechaVisto { get; set; } = DateTime.UtcNow;

    public AcuerdoScrapeado Acuerdo { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}
