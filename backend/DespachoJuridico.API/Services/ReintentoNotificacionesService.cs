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
    private readonly int _antiguedadMaximaDias;

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
        // DJ-141 (incidente real del 18-oct): sin este tope, el reintento le mandó a Mario
        // 25 correos y a Carlos 9 de un backfill histórico de seis semanas de antigüedad --
        // el mismo correo de "nuevo acuerdo" de siempre, sobre algo que ya no era nuevo.
        // Pasado este umbral (sobre FechaAcuerdo, la fecha real del acuerdo, no cuándo se
        // detectó), nunca se reintenta solo -- va directo a avisar al equipo.
        _antiguedadMaximaDias = config.GetValue<int?>("ReintentoNotificaciones:AntiguedadMaximaDias") ?? 7;
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

    // Sobre FechaAcuerdo (la fecha real del acuerdo, no FechaDetectado) -- lo que importa
    // es si el contenido ya se siente viejo para quien lo reciba, no cuándo lo vio el
    // sistema por primera vez.
    internal static bool EsDemasiadoAntiguo(AcuerdoScrapeado acuerdo, DateOnly hoy, int antiguedadMaximaDias) =>
        hoy.DayNumber - acuerdo.FechaAcuerdo.DayNumber > antiguedadMaximaDias;

    // internal (no private): las pruebas de integración lo llaman directo con un
    // IServiceScopeFactory controlado, mismo patrón que EjecutarScrapingAsync. `hoy`
    // es inyectable (igual que `ahoraUtc` en RecordatorioAcuerdosPendientesService) para
    // que las pruebas no dependan del reloj real al evaluar EsDemasiadoAntiguo.
    internal async Task ReintentarPendientesAsync(CancellationToken ct = default, DateOnly? hoy = null)
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

        var hoyEfectivo = hoy ?? DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var acuerdo in candidatos)
        {
            if (EsDemasiadoAntiguo(acuerdo, hoyEfectivo, _antiguedadMaximaDias))
            {
                _logger.LogWarning(
                    "Exp {Numero} supera los {Dias} días de antigüedad -- no se reintenta solo, se avisa directo al equipo",
                    acuerdo.NumeroExpediente, _antiguedadMaximaDias);
                if (!await IntentarAvisarAlEquipoAsync(emailService, acuerdo, antiguo: true)) continue;
                acuerdo.AlertaReintentoEnviada = true;
                await context.SaveChangesAsync(ct);
                continue;
            }

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
                if (!await IntentarAvisarAlEquipoAsync(emailService, acuerdo, antiguo: false)) continue;
                acuerdo.AlertaReintentoEnviada = true;
            }

            // Guardado por acuerdo, no al final del lote -- si el proceso se cae a medio
            // lote (deploy, reinicio), lo ya resuelto no se pierde y no se le reenvía un
            // correo duplicado al litigante en el siguiente ciclo.
            await context.SaveChangesAsync(ct);
        }
    }

    // Regresa false si hasta el aviso falló (ej. el mismo servicio de correo está caído) --
    // en ese caso no se debe marcar AlertaReintentoEnviada ni perder el progreso ya hecho
    // en el lote; se reintenta avisar en el siguiente ciclo.
    private async Task<bool> IntentarAvisarAlEquipoAsync(IEmailService emailService, AcuerdoScrapeado acuerdo, bool antiguo)
    {
        try
        {
            await AvisarAlEquipoAsync(emailService, acuerdo, antiguo);
            return true;
        }
        catch (Exception exAviso)
        {
            _logger.LogError(exAviso,
                "No se pudo avisar al equipo sobre Exp {Numero} -- se reintentará avisar en el siguiente ciclo",
                acuerdo.NumeroExpediente);
            return false;
        }
    }

    private async Task AvisarAlEquipoAsync(IEmailService emailService, AcuerdoScrapeado acuerdo, bool antiguo = false)
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
        var asunto = antiguo
            ? $"Acuerdo antiguo sin notificar, requiere revisión manual — Exp. {acuerdo.NumeroExpediente}"
            : $"Notificación sin poder enviarse (2do intento) — Exp. {acuerdo.NumeroExpediente}";
        var cuerpoHtml = antiguo
            ? $@"
<p>El acuerdo del expediente <strong>{numeroEnc}</strong> ({juzgadoEnc}, {acuerdo.FechaAcuerdo:dd/MM/yyyy}) ya está guardado y visible,
pero nunca se notificó y tiene más de {_antiguedadMaximaDias} días de antigüedad -- el sistema NO le mandó correo automático al
litigante para evitar un aviso tardío y confuso sobre algo que ya no es reciente.</p>
<p>Revisa el caso y decide a mano si notificar directo al litigante, usar <code>Promover?notificar=false</code> si ya está al
tanto por otro medio, o algo distinto.</p>"
            : $@"
<p>El acuerdo del expediente <strong>{numeroEnc}</strong> ({juzgadoEnc}, {acuerdo.FechaAcuerdo:dd/MM/yyyy}) ya está guardado y visible,
pero el correo al litigante falló dos veces -- la primera vez al detectarlo, y ahora en el reintento automático.</p>
<p>No se va a reintentar de nuevo solo. Revisa el correo del litigante asignado y, si hace falta, avísale directamente.</p>";

        foreach (var destinatario in destinatarios)
            await emailService.EnviarAsync(destinatario, "Equipo técnico", asunto, cuerpoHtml);
    }
}
