using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Services;

public class RevisionFechasService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RevisionFechasService> _logger;
    private readonly TimeSpan _intervalo;

    // Umbrales de notificación en días, separados por canal
    private static readonly int[] UmbralesSistema = { 15, 7, 3, 1 };
    private static readonly int[] UmbralesEmail = { 1 };

    public RevisionFechasService(
        IServiceScopeFactory scopeFactory,
        ILogger<RevisionFechasService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var minutos = config.GetValue<int?>("RevisionFechas:IntervaloMinutos") ?? 1440;
        _intervalo = TimeSpan.FromMinutes(minutos);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_intervalo);

        do
        {
            try
            {
                await RevisarFechasAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la revisión de fechas próximas");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RevisarFechasAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var hoy = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaHoraria).Date;

        var etapasActivas = await context.HistorialEtapas
            .Include(h => h.Expediente).ThenInclude(e => e.UsuarioAsignado)
            .Include(h => h.EtapaCatalogo)
            .Where(h => h.FechaCompletada == null
                     && h.FechaLimite != null
                     && h.Expediente.Estado != EstadoExpediente.Cerrado
                     // Excluir expedientes de prueba asignados a cuentas de soporte
                     && (h.Expediente.UsuarioAsignado == null || !h.Expediente.UsuarioAsignado.EsCuentaSoporte))
            .ToListAsync(ct);

        _logger.LogInformation("Revisión de fechas: {Cantidad} etapas activas con fecha límite", etapasActivas.Count);

        // Socios activos (para notificar expedientes Urgentes)
        var socios = await context.Usuarios
            .Where(u => u.Rol == RolUsuario.Socio && u.Activo)
            .ToListAsync(ct);

        foreach (var historial in etapasActivas)
        {
            var diasRestantes = (historial.FechaLimite!.Value.Date - hoy).Days;
            var enUmbralSistema = UmbralesSistema.Contains(diasRestantes);
            var enUmbralEmail = UmbralesEmail.Contains(diasRestantes);
            if (!enUmbralSistema && !enUmbralEmail)
                continue;


            var expediente = historial.Expediente;
            var etapaNombre = historial.EtapaCatalogo?.Nombre ?? "Etapa";

            var mensaje = $"El expediente {expediente.NumeroExpediente} " +
                          $"tiene la etapa '{etapaNombre}' próxima a vencer en {diasRestantes} día(s) " +
                          $"({historial.FechaLimite:dd/MM/yyyy})";

            // ── Notificación interna (panel del sistema) ──
            if (enUmbralSistema)
            {
                var yaExisteSistema = await context.Notificaciones.AnyAsync(n =>
                    n.HistorialEtapaId == historial.Id &&
                    n.DiasAnticipacion == diasRestantes &&
                    n.Canal == CanalNotificacion.Sistema, ct);

                if (!yaExisteSistema)
                {
                    context.Notificaciones.Add(new Notificacion
                    {
                        ExpedienteId = historial.ExpedienteId,
                        HistorialEtapaId = historial.Id,
                        Mensaje = mensaje,
                        DiasAnticipacion = diasRestantes,
                        Canal = CanalNotificacion.Sistema,
                        Enviada = false,
                        CreadoEn = DateTime.UtcNow
                    });

                    _logger.LogInformation("Notificación de sistema generada para expediente {Numero}, {Dias} días restantes",
                        expediente.NumeroExpediente, diasRestantes);
                }
            }

            // ── Destinatarios de correo ──
            if (enUmbralEmail)
            {
                var destinatarios = new List<(string Nombre, string Email)>();

                if (expediente.UsuarioAsignado != null)
                    destinatarios.Add((expediente.UsuarioAsignado.Nombre, expediente.UsuarioAsignado.Email));

                if (expediente.Prioridad == Prioridad.Urgente)
                {
                    foreach (var socio in socios)
                        destinatarios.Add((socio.Nombre, socio.Email));
                }

                var diasTexto = diasRestantes == 1 ? "mañana" : $"en {diasRestantes} días";
                var asunto = $"Recordatorio — Exp. {expediente.NumeroExpediente}: etapa '{etapaNombre}' vence {diasTexto}";

                var numeroExpedienteEnc = System.Net.WebUtility.HtmlEncode(expediente.NumeroExpediente);
                var parteDemandadaEnc = System.Net.WebUtility.HtmlEncode(expediente.ParteDemandada);
                var etapaNombreEnc = System.Net.WebUtility.HtmlEncode(etapaNombre);

                foreach (var (nombre, email) in destinatarios.DistinctBy(d => d.Email))
                {
                    var nombreEnc = System.Net.WebUtility.HtmlEncode(nombre);
                    var cuerpoHtml = $@"
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
    <p>Recordatorio de vencimiento</p>
  </div>
  <div class='body'>
    <p>Estimado(a) {nombreEnc},</p>
    <p>Le recordamos que la etapa procesal <strong>{etapaNombreEnc}</strong> del siguiente expediente
    vence <strong>{diasTexto}</strong>, el día <strong>{historial.FechaLimite:dd/MM/yyyy}</strong>.</p>
    <div class='highlight'>
      <p><strong>Expediente:</strong> {numeroExpedienteEnc}</p>
      <p><strong>Parte demandada:</strong> {parteDemandadaEnc}</p>
      <p><strong>Etapa:</strong> {etapaNombreEnc}</p>
      <p><strong>Fecha límite:</strong> {historial.FechaLimite:dd/MM/yyyy}</p>
    </div>
    <p>Le recomendamos revisar el estado del caso y tomar las acciones necesarias
    antes de que venza el plazo.</p>
    <p>Atentamente,<br><strong>Despacho Jurídico Acedo e Hijos</strong></p>
  </div>
  <div class='footer'>
    <p>Este es un mensaje automático del Sistema de Gestión de Expedientes.</p>
    <p>Por favor no responda a este correo.</p>
  </div>
</div></body></html>";

                    var yaExisteEmail = await context.Notificaciones.AnyAsync(n =>
                        n.HistorialEtapaId == historial.Id &&
                        n.DiasAnticipacion == diasRestantes &&
                        n.Canal == CanalNotificacion.Email &&
                        n.DestinatarioEmail == email, ct);

                    if (yaExisteEmail)
                        continue;

                    var notificacionEmail = new Notificacion
                    {
                        ExpedienteId = historial.ExpedienteId,
                        HistorialEtapaId = historial.Id,
                        Mensaje = mensaje,
                        DiasAnticipacion = diasRestantes,
                        Canal = CanalNotificacion.Email,
                        DestinatarioEmail = email,
                        Enviada = false,
                        CreadoEn = DateTime.UtcNow
                    };

                    try
                    {
                        await emailService.EnviarAsync(email, nombre, asunto, cuerpoHtml);
                        notificacionEmail.Enviada = true;
                        notificacionEmail.FechaEnvio = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "No se pudo enviar correo a {Email} para expediente {Numero}",
                            email, expediente.NumeroExpediente);
                    }

                    context.Notificaciones.Add(notificacionEmail);
                }
            }
        }

        await context.SaveChangesAsync(ct);
    }
}