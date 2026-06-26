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

    // Umbrales de notificación en días
    private static readonly int[] UmbralesDias = { 15, 7, 3 };

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
            .Where(h => h.FechaCompletada == null && h.FechaLimite != null)
            .ToListAsync(ct);

        _logger.LogInformation("Revisión de fechas: {Cantidad} etapas activas con fecha límite", etapasActivas.Count);

        // Socios activos (para notificar expedientes Urgentes)
        var socios = await context.Usuarios
            .Where(u => u.Rol == RolUsuario.Socio && u.Activo)
            .ToListAsync(ct);

        foreach (var historial in etapasActivas)
        {
            var diasRestantes = (historial.FechaLimite!.Value.Date - hoy).Days;
            _logger.LogInformation("Expediente {Numero}: FechaLimite={FechaLimite}, Hoy={Hoy}, DiasRestantes={Dias}",
                historial.Expediente.NumeroExpediente,
                historial.FechaLimite.Value.Date,
                hoy,
                diasRestantes);
            if (!UmbralesDias.Contains(diasRestantes))
                continue;


            var expediente = historial.Expediente;
            var etapaNombre = historial.EtapaCatalogo?.Nombre ?? "Etapa";

            var mensaje = $"El expediente {expediente.NumeroExpediente} " +
                          $"tiene la etapa '{etapaNombre}' próxima a vencer en {diasRestantes} día(s) " +
                          $"({historial.FechaLimite:dd/MM/yyyy})";

            // ── Notificación interna (panel del sistema) ──
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

            // ── Destinatarios de correo ──
            var destinatarios = new List<(string Nombre, string Email)>();

            if (expediente.UsuarioAsignado != null)
                destinatarios.Add((expediente.UsuarioAsignado.Nombre, expediente.UsuarioAsignado.Email));

            if (expediente.Prioridad == Prioridad.Urgente)
            {
                foreach (var socio in socios)
                    destinatarios.Add((socio.Nombre, socio.Email));
            }

            var asunto = $"Aviso: '{etapaNombre}' del expediente {expediente.NumeroExpediente} vence en {diasRestantes} día(s)";
            var cuerpoHtml = $@"
                <h3>Despacho Jurídico - Aviso de vencimiento</h3>
                <p><strong>Expediente:</strong> {expediente.NumeroExpediente}</p>
                <p><strong>Parte demandada:</strong> {expediente.ParteDemandada}</p>
                <p><strong>Etapa:</strong> {etapaNombre}</p>
                <p><strong>Fecha límite:</strong> {historial.FechaLimite:dd/MM/yyyy}</p>
                <p><strong>Días restantes:</strong> {diasRestantes}</p>";

            foreach (var (nombre, email) in destinatarios.DistinctBy(d => d.Email))
            {
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

        await context.SaveChangesAsync(ct);
    }
}