using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
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

        // En producción: una vez al día. En desarrollo: cada minuto para poder probar.
        var minutos = config.GetValue<int?>("RevisionFechas:IntervaloMinutos") ?? 1440;
        _intervalo = TimeSpan.FromMinutes(minutos);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_intervalo);

        // Ejecuta una vez al iniciar, y luego en cada intervalo
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
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RevisarFechasAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hoy = DateTime.UtcNow.Date;

        var etapasActivas = await context.HistorialEtapas
            .Include(h => h.Expediente)
            .Where(h => h.FechaCompletada == null && h.FechaLimite != null)
            .ToListAsync(ct);

        _logger.LogInformation("Revisión de fechas: {Cantidad} etapas activas con fecha límite", etapasActivas.Count);

        foreach (var historial in etapasActivas)
        {
            var diasRestantes = (historial.FechaLimite!.Value.Date - hoy).Days;

            if (!UmbralesDias.Contains(diasRestantes))
                continue;

            // Evitar duplicados: ¿ya existe una notificación para esta etapa y este umbral?
            var yaExiste = await context.Notificaciones.AnyAsync(n =>
                n.HistorialEtapaId == historial.Id &&
                n.DiasAnticipacion == diasRestantes, ct);

            if (yaExiste)
                continue;

            var mensaje = $"El expediente {historial.Expediente.NumeroExpediente} " +
                          $"tiene la etapa próxima a vencer en {diasRestantes} día(s) " +
                          $"({historial.FechaLimite:dd/MM/yyyy})";

            context.Notificaciones.Add(new Notificacion
            {
                ExpedienteId = historial.ExpedienteId,
                HistorialEtapaId = historial.Id,
                Mensaje = mensaje,
                DiasAnticipacion = diasRestantes,
                Canal = Models.Enums.CanalNotificacion.Sistema, // Historia 18 decide email/WhatsApp
                Enviada = false,
                CreadoEn = DateTime.UtcNow
            });

            _logger.LogInformation("Notificación generada para expediente {Numero}, {Dias} días restantes",
                historial.Expediente.NumeroExpediente, diasRestantes);
        }

        await context.SaveChangesAsync(ct);
    }
}