using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Services;

// Recordatorio pasivo para acuerdos "Media" (DJ-122: sugerencia banco+juzgado sin
// nombre del demandado) que el litigante nunca confirmó ni descartó. El silencio
// no es evidencia de que la sugerencia sea correcta, así que NUNCA se oculta ni se
// sube a "Alta" solo por el paso del tiempo (eso sería inventar certeza que no
// existe) — lo único que hace este servicio es reforzar el aviso por correo,
// agrupando en un solo mensaje todo lo que un litigante tiene pendiente de revisar
// desde hace más de N días. El acuerdo sigue viviendo normal en la sección de
// Acuerdos del expediente durante todo este proceso.
//
// No existe hoy ningún correo periódico/digest al que enganchar esto (los otros
// 4 mecanismos de correo del sistema son uno-por-evento, ver ScraperAcuerdosService,
// RevisionFechasService, RecordatorioCitasService, ExpedientesController) — este es
// el primer job dedicado a agrupar varios items en un solo correo, clonando el
// patrón periódico de RevisionFechasService/RecordatorioCitasService en vez del
// patrón "un correo por item" de ScraperAcuerdosService.
public class RecordatorioAcuerdosPendientesService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecordatorioAcuerdosPendientesService> _logger;
    private readonly TimeSpan _intervalo;
    private readonly int _diasUmbral;

    public RecordatorioAcuerdosPendientesService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecordatorioAcuerdosPendientesService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var minutos = config.GetValue<int?>("RecordatorioAcuerdosPendientes:IntervaloMinutos") ?? 1440;
        _intervalo = TimeSpan.FromMinutes(minutos);
        _diasUmbral = config.GetValue<int?>("RecordatorioAcuerdosPendientes:DiasUmbral") ?? 3;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_intervalo);

        do
        {
            try
            {
                await RevisarPendientesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la revisión de acuerdos Media pendientes de confirmar/descartar");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // Un acuerdo "Media" sin resolver hace >= diasUmbral días desde que se detectó:
    // ni confirmado (eso lo sube a "Alta", ver AcuerdosController.Confirmar), ni
    // descartado (DescartadoManualmente=true), ni ya recordado antes (una sola vez
    // por acuerdo — ver comentario de RecordatorioPendienteEnviado en el modelo).
    internal static bool EsElegibleParaRecordatorio(AcuerdoScrapeado acuerdo, DateTime ahoraUtc, int diasUmbral)
    {
        if (acuerdo.Confianza != "Media") return false;
        if (acuerdo.DescartadoManualmente) return false;
        if (acuerdo.RecordatorioPendienteEnviado) return false;

        return (ahoraUtc - acuerdo.FechaDetectado).TotalDays >= diasUmbral;
    }

    private async Task RevisarPendientesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var ahoraUtc = DateTime.UtcNow;

        var candidatos = await context.AcuerdosScrapeados
            .Include(a => a.Expediente).ThenInclude(e => e.UsuarioAsignado)
            .Where(a => a.Confianza == "Media" && !a.DescartadoManualmente && !a.RecordatorioPendienteEnviado)
            .ToListAsync(ct);

        var pendientes = candidatos
            .Where(a => EsElegibleParaRecordatorio(a, ahoraUtc, _diasUmbral))
            .Where(a => a.Expediente.UsuarioAsignado != null
                     && a.Expediente.UsuarioAsignado.Activo
                     && !a.Expediente.UsuarioAsignado.EsCuentaSoporte)
            .ToList();

        if (pendientes.Count == 0) return;

        _logger.LogInformation("Recordatorio de pendientes: {Cantidad} acuerdos Media sin resolver hace {Dias}+ días",
            pendientes.Count, _diasUmbral);

        foreach (var grupo in pendientes.GroupBy(a => a.Expediente.UsuarioAsignado!))
        {
            var litigante = grupo.Key;
            var acuerdos = grupo.OrderBy(a => a.FechaDetectado).ToList();

            await EnviarRecordatorioAsync(emailService, litigante, acuerdos);

            foreach (var acuerdo in acuerdos)
                acuerdo.RecordatorioPendienteEnviado = true;

            _logger.LogInformation("Recordatorio enviado a {Nombre} ({Cantidad} acuerdos pendientes)",
                litigante.Nombre, acuerdos.Count);
        }

        await context.SaveChangesAsync(ct);
    }

    private static async Task EnviarRecordatorioAsync(IEmailService emailService, Usuario litigante, List<AcuerdoScrapeado> acuerdos)
    {
        var nombreEnc = System.Net.WebUtility.HtmlEncode(litigante.Nombre);
        var asunto = acuerdos.Count == 1
            ? "Tienes 1 sugerencia pendiente de revisar"
            : $"Tienes {acuerdos.Count} sugerencias pendientes de revisar";

        var filas = string.Join("\n", acuerdos.Select(a =>
        {
            var numeroEnc = System.Net.WebUtility.HtmlEncode(a.Expediente.NumeroExpediente);
            var juzgadoEnc = System.Net.WebUtility.HtmlEncode(a.NombreJuzgado);
            var sintesisEnc = System.Net.WebUtility.HtmlEncode(a.Sintesis);
            return $@"
    <div class='item'>
      <p><strong>Expediente {numeroEnc}</strong> — {juzgadoEnc}</p>
      <p class='sintesis'>{sintesisEnc}</p>
    </div>";
        }));

        // Mismo esqueleto visual que el resto de correos del sistema, pero con
        // acento ámbar (en vez del dorado normal) para que se distinga a simple
        // vista de una notificación nueva — esto es un recordatorio de algo que
        // ya se avisó antes, no una alerta nueva.
        var cuerpoHtml = $@"
<!DOCTYPE html><html><head><meta charset='UTF-8'>
<style>
  body{{font-family:Georgia,'Times New Roman',serif;background:#f7f5f0;margin:0;padding:0;}}
  .container{{max-width:580px;margin:40px auto;background:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);}}
  .header{{background:#1c2b4a;padding:32px 40px;text-align:center;}}
  .header h1{{color:#ffffff;font-size:20px;margin:0;font-weight:normal;letter-spacing:1px;}}
  .header p{{color:#c47f17;font-size:13px;margin:6px 0 0;letter-spacing:2px;text-transform:uppercase;}}
  .body{{padding:40px;color:#333333;}}
  .body p{{font-size:15px;line-height:1.7;margin:0 0 16px;}}
  .item{{background:#fff8ec;border-left:4px solid #c47f17;padding:14px 20px;margin:16px 0;border-radius:0 6px 6px 0;}}
  .item p{{margin:4px 0;font-size:14px;color:#444;}}
  .item strong{{color:#1c2b4a;}}
  .item .sintesis{{color:#666;font-style:italic;}}
  .footer{{background:#f7f5f0;padding:20px 40px;text-align:center;border-top:1px solid #e0ddd6;}}
  .footer p{{font-size:12px;color:#888;margin:0;}}
</style></head>
<body><div class='container'>
  <div class='header'>
    <h1>Despacho Jurídico Acedo e Hijos</h1>
    <p>Recordatorio de sugerencias pendientes</p>
  </div>
  <div class='body'>
    <p>Estimado(a) {nombreEnc},</p>
    <p>Hace unos días le avisamos de {(acuerdos.Count == 1 ? "un posible acuerdo" : "los siguientes posibles acuerdos")} que
    coinciden en número de expediente y juzgado con sus casos, pero cuyo texto no menciona al demandado por nombre — sigue{(acuerdos.Count == 1 ? "" : "n")}
    sin confirmar o descartar:</p>
    {filas}
    <p>Entre al sistema y use ""Confirmar"" si el caso es suyo, o ""Descartar"" si no lo es — este es un recordatorio único, no se le volverá a avisar de {(acuerdos.Count == 1 ? "este" : "estos")} en particular.</p>
    <p>Atentamente,<br><strong>Despacho Jurídico Acedo e Hijos</strong></p>
  </div>
  <div class='footer'>
    <p>Este es un mensaje automático del Sistema de Gestión de Expedientes.</p>
    <p>Por favor no responda a este correo.</p>
  </div>
</div></body></html>";

        await emailService.EnviarAsync(litigante.Email, litigante.Nombre, asunto, cuerpoHtml);
    }
}
