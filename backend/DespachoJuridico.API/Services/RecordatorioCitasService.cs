using DespachoJuridico.API.Data;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Services;

public class RecordatorioCitasService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecordatorioCitasService> _logger;
    private readonly TimeSpan _intervalo;

    public RecordatorioCitasService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecordatorioCitasService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var minutos = config.GetValue<int?>("RecordatorioCitas:IntervaloMinutos") ?? 1440;
        _intervalo = TimeSpan.FromMinutes(minutos);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_intervalo);

        do
        {
            try
            {
                await EnviarRecordatoriosAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el envío de recordatorios de citas");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EnviarRecordatoriosAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var hoy = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaHoraria).Date;
        var manana = DateTime.SpecifyKind(hoy.AddDays(1), DateTimeKind.Utc);

        var citas = await context.Citas
            .Include(c => c.Usuario)
            .Include(c => c.Expediente)
            .Where(c => !c.ReminderEnviado && c.FechaHora.Date == manana)
            .ToListAsync(ct);

        _logger.LogInformation("Recordatorio de citas: {Cantidad} citas para mañana", citas.Count);

        foreach (var cita in citas)
        {
            try
            {
                var asunto = $"Recordatorio — Cita mañana: {cita.Titulo}";
                var cuerpo = ConstruirCuerpo(cita);

                await emailService.EnviarAsync(cita.Usuario.Email, cita.Usuario.Nombre, asunto, cuerpo);

                cita.ReminderEnviado = true;
                await context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar recordatorio de la cita {CitaId} a {Email}",
                    cita.Id, cita.Usuario.Email);
            }
        }
    }

    private static string ConstruirCuerpo(Models.Cita cita)
    {
        var nombreEnc = System.Net.WebUtility.HtmlEncode(cita.Usuario.Nombre);
        var tituloEnc = System.Net.WebUtility.HtmlEncode(cita.Titulo);

        var expedienteInfo = cita.Expediente != null
            ? $"<p><strong>Expediente relacionado:</strong> {System.Net.WebUtility.HtmlEncode(cita.Expediente.NumeroExpediente)} — {System.Net.WebUtility.HtmlEncode(cita.Expediente.ParteDemandada)}</p>"
            : "";

        var notasInfo = string.IsNullOrWhiteSpace(cita.Descripcion)
            ? ""
            : $"<p><strong>Notas:</strong> {System.Net.WebUtility.HtmlEncode(cita.Descripcion)}</p>";

        return $@"
<!DOCTYPE html><html><head><meta charset='UTF-8'>
<style>
  body{{font-family:Georgia,'Times New Roman',serif;background:#f7f5f0;margin:0;padding:0;}}
  .container{{max-width:580px;margin:40px auto;background:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);}}
  .header{{background:#1c2b4a;padding:32px 40px;text-align:center;}}
  .header h1{{color:#ffffff;font-size:20px;margin:0;font-weight:normal;letter-spacing:1px;}}
  .header p{{color:#9a7c3c;font-size:13px;margin:6px 0 0;letter-spacing:2px;text-transform:uppercase;}}
  .body{{padding:40px;color:#333333;}}
  .body p{{font-size:15px;line-height:1.7;margin:0 0 16px;}}
  .highlight{{background:#f0f4fa;border-left:4px solid #9a7c3c;padding:16px 20px;margin:24px 0;border-radius:0 6px 6px 0;}}
  .highlight p{{margin:4px 0;font-size:14px;color:#444;}}
  .highlight strong{{color:#1c2b4a;}}
  .footer{{background:#f7f5f0;padding:20px 40px;text-align:center;border-top:1px solid #e0ddd6;}}
  .footer p{{font-size:12px;color:#888;margin:0;}}
</style></head>
<body><div class='container'>
  <div class='header'>
    <h1>Despacho Jurídico Acedo e Hijos</h1>
    <p>Recordatorio de cita</p>
  </div>
  <div class='body'>
    <p>Estimado(a) {nombreEnc},</p>
    <p>Le recordamos que mañana tiene agendada la siguiente cita en su calendario:</p>
    <div class='highlight'>
      <p><strong>Título:</strong> {tituloEnc}</p>
      <p><strong>Fecha y hora:</strong> {cita.FechaHora:dd/MM/yyyy} a las {cita.FechaHora:HH:mm}</p>
      {expedienteInfo}
      {notasInfo}
    </div>
    <p>Atentamente,<br><strong>Despacho Jurídico Acedo e Hijos</strong></p>
  </div>
  <div class='footer'>
    <p>Este es un mensaje automático del Sistema de Gestión de Expedientes.</p>
    <p>Por favor no responda a este correo.</p>
  </div>
</div></body></html>";
    }
}
