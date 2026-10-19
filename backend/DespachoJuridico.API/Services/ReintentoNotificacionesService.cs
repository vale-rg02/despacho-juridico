using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Services;

// DJ-141: reintento mínimo para acuerdos que se guardaron correctamente (Oculto=false,
// la clasificación acertó) pero cuyo correo nunca salió -- hallazgo real de la revisión
// retroactiva: 11 casos en 60 días sin ningún rastro de la causa exacta (la bitácora de
// excepciones de DJ-139 no existía todavía cuando pasaron). El diseño ya contemplaba que
// la notificación podía fallar sin tumbar el guardado (ver Promover/Sugerir/
// EjecutarScrapingAsync), pero nunca había un segundo intento -- se quedaban así para
// siempre, en silencio, sin que nadie se enterara salvo que el litigante reclamara.
//
// Primer intento al arrancar (igual que RecordatorioAcuerdosPendientesService) para que
// un deploy resuelva de inmediato cualquier backlog ya guardado. Si el reintento TAMBIÉN
// falla, se avisa una sola vez al equipo técnico (mismos destinatarios que el reporte
// diario de auditoría, Auditoria:Destinatarios) y se deja de tocar ese acuerdo -- de ahí
// en adelante es una decisión humana (confirmar la causa, y resolverlo a mano con
// Promover/Sugerir), no algo para seguir reintentando indefinidamente sin supervisión.
public class ReintentoNotificacionesService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScraperAcuerdosService _scraper;
    private readonly ILogger<ReintentoNotificacionesService> _logger;
    private readonly IConfiguration _config;
    private readonly TimeSpan _intervalo;

    public ReintentoNotificacionesService(
        IServiceScopeFactory scopeFactory,
        ScraperAcuerdosService scraper,
        ILogger<ReintentoNotificacionesService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _scraper = scraper;
        _logger = logger;
        _config = config;

        var minutos = config.GetValue<int?>("ReintentoNotificaciones:IntervaloMinutos") ?? 60;
        _intervalo = TimeSpan.FromMinutes(minutos);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_intervalo);

        do
        {
            try
            {
                await ReintentarPendientesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el reintento de notificaciones fallidas");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // Un acuerdo elegible para reintento: visible (no oculto), nunca se notificó,
    // nadie lo descartó, no fue un silencio a propósito (Promover?notificar=false),
    // y todavía no se avisó al equipo de que el reintento también falló.
    internal static bool EsElegibleParaReintento(AcuerdoScrapeado acuerdo) =>
        !acuerdo.Oculto && !acuerdo.NotificacionEnviada && !acuerdo.DescartadoManualmente
        && !acuerdo.NotificacionOmitida && !acuerdo.AlertaReintentoEnviada;

    // internal (no private): las pruebas de integración lo llaman directo con un
    // IServiceScopeFactory controlado, mismo patrón que EjecutarScrapingAsync.
    internal async Task ReintentarPendientesAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var candidatos = await context.AcuerdosScrapeados
            .Include(a => a.Expediente).ThenInclude(e => e.UsuarioAsignado)
            .Where(a => !a.Oculto && !a.NotificacionEnviada && !a.DescartadoManualmente
                     && !a.NotificacionOmitida && !a.AlertaReintentoEnviada)
            .ToListAsync(ct);

        if (candidatos.Count == 0) return;

        _logger.LogInformation("Reintento de notificaciones: {Cantidad} acuerdos pendientes", candidatos.Count);

        foreach (var acuerdo in candidatos)
        {
            try
            {
                // Sin litigante asignado, EnviarNotificacionAsync regresa en silencio sin
                // mandar nada ni lanzar -- sin este chequeo, contaría como éxito y nadie
                // se enteraría jamás de que en realidad no salió ningún correo.
                if (acuerdo.Expediente.UsuarioAsignado == null)
                    throw new InvalidOperationException("El expediente no tiene litigante asignado");

                await _scraper.EnviarNotificacionManualAsync(emailService, acuerdo.Expediente, acuerdo);
                acuerdo.NotificacionEnviada = true;
                _logger.LogInformation(
                    "Reintento de notificación exitoso: Exp {Numero}", acuerdo.NumeroExpediente);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Reintento de notificación también falló: Exp {Numero} -- avisando al equipo",
                    acuerdo.NumeroExpediente);
                try
                {
                    await AvisarAlEquipoAsync(emailService, acuerdo);
                }
                catch (Exception exAviso)
                {
                    // Si hasta el aviso falla (ej. el mismo servicio de correo está caído),
                    // no se debe perder el progreso ya hecho en este lote -- se guarda igual
                    // abajo y se reintenta avisar en el siguiente ciclo (AlertaReintentoEnviada
                    // se queda en false).
                    _logger.LogError(exAviso,
                        "No se pudo avisar al equipo sobre Exp {Numero} -- se reintentará avisar en el siguiente ciclo",
                        acuerdo.NumeroExpediente);
                    continue;
                }
                acuerdo.AlertaReintentoEnviada = true;
            }

            // Guardado por acuerdo, no al final del lote -- si el proceso se cae a medio
            // lote (deploy, reinicio), lo ya resuelto no se pierde y no se le reenvía un
            // correo duplicado al litigante en el siguiente ciclo.
            await context.SaveChangesAsync(ct);
        }
    }

    private async Task AvisarAlEquipoAsync(IEmailService emailService, AcuerdoScrapeado acuerdo)
    {
        var destinatarios = (_config["Auditoria:Destinatarios"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (destinatarios.Length == 0)
        {
            _logger.LogWarning(
                "Reintento de notificación falló para Exp {Numero} pero no hay destinatarios configurados (Auditoria:Destinatarios) -- no se avisa a nadie.",
                acuerdo.NumeroExpediente);
            return;
        }

        var numeroEnc = System.Net.WebUtility.HtmlEncode(acuerdo.NumeroExpediente);
        var juzgadoEnc = System.Net.WebUtility.HtmlEncode(acuerdo.NombreJuzgado);
        var asunto = $"Notificación sin poder enviarse (2do intento) — Exp. {acuerdo.NumeroExpediente}";
        var cuerpoHtml = $@"
<p>El acuerdo del expediente <strong>{numeroEnc}</strong> ({juzgadoEnc}, {acuerdo.FechaAcuerdo:dd/MM/yyyy}) ya está guardado y visible,
pero el correo al litigante falló dos veces -- la primera vez al detectarlo, y ahora en el reintento automático.</p>
<p>No se va a reintentar de nuevo solo. Revisa el correo del litigante asignado y, si hace falta, avísale directamente.</p>";

        foreach (var destinatario in destinatarios)
            await emailService.EnviarAsync(destinatario, "Equipo técnico", asunto, cuerpoHtml);
    }
}
