using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-141: reintento mínimo para acuerdos guardados correctamente (Oculto=false) cuyo
// correo nunca salió -- hallazgo real de la revisión retroactiva (11 casos en 60 días
// sin rastro de la causa exacta, porque la bitácora de excepciones de DJ-139 no existía
// todavía). Si el reintento también falla, se avisa una sola vez al equipo técnico.
public class ReintentoNotificacionesTests
{
    private class EmailServiceQueFalla : IEmailService
    {
        public List<(string Email, string Asunto)> Intentos { get; } = new();
        public Task EnviarAsync(string destinatarioEmail, string destinatarioNombre, string asunto, string cuerpoHtml)
        {
            Intentos.Add((destinatarioEmail, asunto));
            throw new InvalidOperationException("Fallo simulado al enviar notificación");
        }
    }

    // Permite que el primer intento (la notificación normal) falle, pero el segundo
    // (el aviso al equipo) funcione -- ambos pasan por el mismo IEmailService.
    private class EmailServiceQueFallaSoloParaElLitigante : IEmailService
    {
        private readonly HashSet<string> _emailsQueFallan;
        public List<(string Email, string Asunto)> Enviados { get; } = new();

        public EmailServiceQueFallaSoloParaElLitigante(HashSet<string> emailsQueFallan) => _emailsQueFallan = emailsQueFallan;

        public Task EnviarAsync(string destinatarioEmail, string destinatarioNombre, string asunto, string cuerpoHtml)
        {
            if (_emailsQueFallan.Contains(destinatarioEmail))
                throw new InvalidOperationException("Fallo simulado al enviar notificación al litigante");
            Enviados.Add((destinatarioEmail, asunto));
            return Task.CompletedTask;
        }
    }

    private static AcuerdoScrapeado NuevoAcuerdoVisibleSinNotificar(
        bool notificacionOmitida = false, bool alertaReintentoEnviada = false) => new()
    {
        NumeroExpediente = "401/2026",
        NombreJuzgado = "2do Civil Hermosillo",
        Partes = "SIN NOMBRE RECONOCIBLE",
        Sintesis = "Se admite.",
        FechaAcuerdo = new DateOnly(2026, 10, 1),
        Confianza = "Alta",
        Oculto = false,
        NotificacionEnviada = false,
        NotificacionOmitida = notificacionOmitida,
        AlertaReintentoEnviada = alertaReintentoEnviada
    };

    [Fact]
    public void AcuerdoVisibleSinNotificar_EsElegible()
    {
        Assert.True(ReintentoNotificacionesService.EsElegibleParaReintento(NuevoAcuerdoVisibleSinNotificar()));
    }

    [Fact]
    public void AcuerdoYaNotificado_NoEsElegible()
    {
        var acuerdo = NuevoAcuerdoVisibleSinNotificar();
        acuerdo.NotificacionEnviada = true;
        Assert.False(ReintentoNotificacionesService.EsElegibleParaReintento(acuerdo));
    }

    [Fact]
    public void AcuerdoOculto_NoEsElegible()
    {
        var acuerdo = NuevoAcuerdoVisibleSinNotificar();
        acuerdo.Oculto = true;
        Assert.False(ReintentoNotificacionesService.EsElegibleParaReintento(acuerdo));
    }

    [Fact]
    public void AcuerdoDescartadoManualmente_NoEsElegible()
    {
        var acuerdo = NuevoAcuerdoVisibleSinNotificar();
        acuerdo.DescartadoManualmente = true;
        Assert.False(ReintentoNotificacionesService.EsElegibleParaReintento(acuerdo));
    }

    [Fact]
    public void AcuerdoConNotificacionOmitidaAProposito_NuncaEsElegible()
    {
        // Promover?notificar=false -- el litigante ya está al tanto por otro medio.
        Assert.False(ReintentoNotificacionesService.EsElegibleParaReintento(
            NuevoAcuerdoVisibleSinNotificar(notificacionOmitida: true)));
    }

    [Fact]
    public void AcuerdoYaAlertado_NoVuelveASerElegible()
    {
        // Una sola alerta por acuerdo -- de ahí en adelante se resuelve a mano.
        Assert.False(ReintentoNotificacionesService.EsElegibleParaReintento(
            NuevoAcuerdoVisibleSinNotificar(alertaReintentoEnviada: true)));
    }

    [Fact]
    public void AcuerdoDentroDelTope_NoEsDemasiadoAntiguo()
    {
        var acuerdo = NuevoAcuerdoVisibleSinNotificar();
        acuerdo.FechaAcuerdo = new DateOnly(2026, 10, 1);
        Assert.False(ReintentoNotificacionesService.EsDemasiadoAntiguo(acuerdo, new DateOnly(2026, 10, 8), antiguedadMaximaDias: 7));
    }

    [Fact]
    public void AcuerdoPasandoElTope_EsDemasiadoAntiguo()
    {
        // Mismo incidente real del 18-oct: un backfill de seis semanas de antigüedad
        // no debe tratarse como "recién llegado" solo porque el sistema lo vio hoy.
        var acuerdo = NuevoAcuerdoVisibleSinNotificar();
        acuerdo.FechaAcuerdo = new DateOnly(2026, 9, 1);
        Assert.True(ReintentoNotificacionesService.EsDemasiadoAntiguo(acuerdo, new DateOnly(2026, 10, 18), antiguedadMaximaDias: 7));
    }

    // ── Integración (EF InMemory real, igual patrón que SugerirAcuerdoTests) ──

    private static AppDbContext CrearContextoEnMemoria(string nombreBD) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreBD).Options);

    private static ScraperAcuerdosService CrearScraperService(string nombreBD)
    {
        var servicios = new ServiceCollection();
        servicios.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(nombreBD));
        var proveedor = servicios.BuildServiceProvider();
        return new ScraperAcuerdosService(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ScraperAcuerdosService>.Instance,
            new ConfigurationBuilder().Build());
    }

    private static async Task<(Usuario litigante, Expediente expediente, AcuerdoScrapeado acuerdo)> SembrarAcuerdoVisibleSinNotificarAsync(AppDbContext context)
    {
        var litigante = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.Add(litigante);
        await context.SaveChangesAsync();

        var expediente = new Expediente
        {
            NumeroExpediente = "401/2026", ParteDemandada = "SIN IMPORTANCIA", Juzgado = "2do Civil Hermosillo",
            UsuarioAsignadoId = litigante.Id, CreadoPorId = litigante.Id
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        var acuerdo = NuevoAcuerdoVisibleSinNotificar();
        acuerdo.ExpedienteId = expediente.Id;
        context.AcuerdosScrapeados.Add(acuerdo);
        await context.SaveChangesAsync();

        return (litigante, expediente, acuerdo);
    }

    private static ReintentoNotificacionesService CrearServicio(
        string nombreBD, ScraperAcuerdosService scraper, IEmailService emailService, string? destinatarios = "equipo@despacho.com")
    {
        var servicios = new ServiceCollection();
        servicios.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(nombreBD));
        servicios.AddSingleton(emailService);
        var proveedor = servicios.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(destinatarios == null ? new Dictionary<string, string?>() : new Dictionary<string, string?>
            {
                ["Auditoria:Destinatarios"] = destinatarios
            })
            .Build();

        return new ReintentoNotificacionesService(
            proveedor.GetRequiredService<IServiceScopeFactory>(), scraper, NullLogger<ReintentoNotificacionesService>.Instance, config);
    }

    [Fact]
    public async Task ReintentoExitoso_MarcaNotificacionEnviada()
    {
        var nombreBD = nameof(ReintentoExitoso_MarcaNotificacionEnviada);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (litigante, _, acuerdo) = await SembrarAcuerdoVisibleSinNotificarAsync(context);
        var email = new FakeEmailService();
        var servicio = CrearServicio(nombreBD, CrearScraperService(nombreBD), email);

        await servicio.ReintentarPendientesAsync(hoy: new DateOnly(2026, 10, 2)); // dentro del tope de antigüedad por defecto

        var enviado = Assert.Single(email.Enviados);
        Assert.Equal(litigante.Email, enviado.Email);
        context.ChangeTracker.Clear(); // el servicio escribió con su propio DbContext interno
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.True(actualizado!.NotificacionEnviada);
        Assert.False(actualizado.AlertaReintentoEnviada);
    }

    [Fact]
    public async Task ReintentoFalla_AvisaAlEquipoYMarcaAlertaEnviada()
    {
        var nombreBD = nameof(ReintentoFalla_AvisaAlEquipoYMarcaAlertaEnviada);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (litigante, _, acuerdo) = await SembrarAcuerdoVisibleSinNotificarAsync(context);
        var email = new EmailServiceQueFallaSoloParaElLitigante(new HashSet<string> { litigante.Email });
        var servicio = CrearServicio(nombreBD, CrearScraperService(nombreBD), email);

        await servicio.ReintentarPendientesAsync(hoy: new DateOnly(2026, 10, 2)); // dentro del tope de antigüedad por defecto

        var aviso = Assert.Single(email.Enviados); // el único que SÍ se logra enviar es el aviso al equipo
        Assert.Equal("equipo@despacho.com", aviso.Email);
        Assert.Contains(acuerdo.NumeroExpediente, aviso.Asunto);
        context.ChangeTracker.Clear();
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.False(actualizado!.NotificacionEnviada);
        Assert.True(actualizado.AlertaReintentoEnviada);
    }

    [Fact]
    public async Task ReintentoFalla_SinDestinatariosConfigurados_NoTruenaYMarcaAlertaIgual()
    {
        var nombreBD = nameof(ReintentoFalla_SinDestinatariosConfigurados_NoTruenaYMarcaAlertaIgual);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (_, _, acuerdo) = await SembrarAcuerdoVisibleSinNotificarAsync(context);
        var email = new EmailServiceQueFalla();
        var servicio = CrearServicio(nombreBD, CrearScraperService(nombreBD), email, destinatarios: null);

        await servicio.ReintentarPendientesAsync(hoy: new DateOnly(2026, 10, 2)); // dentro del tope de antigüedad por defecto

        // Solo el intento fallido de notificar al litigante -- sin destinatarios
        // configurados, nunca se intenta un segundo envío para el aviso.
        var intento = Assert.Single(email.Intentos);
        Assert.NotEqual("equipo@despacho.com", intento.Email);
        context.ChangeTracker.Clear();
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.True(actualizado!.AlertaReintentoEnviada);
    }

    [Fact]
    public async Task AcuerdoConNotificacionOmitida_NuncaSeToca()
    {
        var nombreBD = nameof(AcuerdoConNotificacionOmitida_NuncaSeToca);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (litigante, expediente, _) = await SembrarAcuerdoVisibleSinNotificarAsync(context);
        var acuerdoOmitido = await context.AcuerdosScrapeados.FirstAsync();
        acuerdoOmitido.NotificacionOmitida = true;
        await context.SaveChangesAsync();
        var email = new FakeEmailService();
        var servicio = CrearServicio(nombreBD, CrearScraperService(nombreBD), email);

        await servicio.ReintentarPendientesAsync(hoy: new DateOnly(2026, 10, 2)); // dentro del tope de antigüedad por defecto

        Assert.Empty(email.Enviados); // nunca se le manda nada al litigante que ya está al tanto
    }

    [Fact]
    public async Task AcuerdoYaAlertado_NoVuelveAIntentarNiAAvisar()
    {
        var nombreBD = nameof(AcuerdoYaAlertado_NoVuelveAIntentarNiAAvisar);
        using var context = CrearContextoEnMemoria(nombreBD);
        await SembrarAcuerdoVisibleSinNotificarAsync(context);
        var acuerdoYaAlertado = await context.AcuerdosScrapeados.FirstAsync();
        acuerdoYaAlertado.AlertaReintentoEnviada = true;
        await context.SaveChangesAsync();
        var email = new EmailServiceQueFalla();
        var servicio = CrearServicio(nombreBD, CrearScraperService(nombreBD), email);

        await servicio.ReintentarPendientesAsync(hoy: new DateOnly(2026, 10, 2)); // dentro del tope de antigüedad por defecto

        Assert.Empty(email.Intentos); // no se vuelve a intentar, no se vuelve a avisar
    }

    [Fact]
    public async Task SinLitiganteAsignado_CuentaComoFallaYAvisaAlEquipo_NoComoExito()
    {
        // EnviarNotificacionAsync regresa en silencio sin mandar nada cuando no hay
        // litigante asignado -- sin el chequeo explícito, esto se contaría como éxito.
        var nombreBD = nameof(SinLitiganteAsignado_CuentaComoFallaYAvisaAlEquipo_NoComoExito);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (_, _, acuerdo) = await SembrarAcuerdoVisibleSinNotificarAsync(context);
        var expediente = await context.Expedientes.FirstAsync();
        expediente.UsuarioAsignadoId = null;
        await context.SaveChangesAsync();
        var email = new FakeEmailService();
        var servicio = CrearServicio(nombreBD, CrearScraperService(nombreBD), email);

        await servicio.ReintentarPendientesAsync(hoy: new DateOnly(2026, 10, 2)); // dentro del tope de antigüedad por defecto

        var aviso = Assert.Single(email.Enviados);
        Assert.Equal("equipo@despacho.com", aviso.Email);
        context.ChangeTracker.Clear();
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.False(actualizado!.NotificacionEnviada); // nunca se mandó nada real
        Assert.True(actualizado.AlertaReintentoEnviada);
    }

    [Fact]
    public async Task SiHastaElAvisoFalla_NoPierdeElProgresoYaHechoEnElLote()
    {
        // Dos acuerdos en el mismo lote: el primero se resuelve con éxito, el segundo
        // falla y, además, falla el propio aviso al equipo (mismo servicio de correo
        // caído). El guardado por-acuerdo debe proteger el progreso del primero.
        var nombreBD = nameof(SiHastaElAvisoFalla_NoPierdeElProgresoYaHechoEnElLote);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (litiganteQueSiFunciona, _, acuerdoQueSiFunciona) = await SembrarAcuerdoVisibleSinNotificarAsync(context);

        var litiganteQueFalla = new Usuario { Nombre = "Carlos Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.Add(litiganteQueFalla);
        await context.SaveChangesAsync();
        var expedienteQueFalla = new Expediente
        {
            NumeroExpediente = "999/2026", ParteDemandada = "SIN IMPORTANCIA", Juzgado = "2do Civil Hermosillo",
            UsuarioAsignadoId = litiganteQueFalla.Id, CreadoPorId = litiganteQueFalla.Id
        };
        context.Expedientes.Add(expedienteQueFalla);
        await context.SaveChangesAsync();
        var acuerdoQueFalla = NuevoAcuerdoVisibleSinNotificar();
        acuerdoQueFalla.ExpedienteId = expedienteQueFalla.Id;
        acuerdoQueFalla.NumeroExpediente = "999/2026";
        context.AcuerdosScrapeados.Add(acuerdoQueFalla);
        await context.SaveChangesAsync();

        // Falla para el litigante del segundo Y para el correo del equipo -- "equipo
        // caído por completo", el peor caso.
        var email = new EmailServiceQueFallaSoloParaElLitigante(new HashSet<string>
        {
            litiganteQueFalla.Email, "equipo@despacho.com"
        });
        var servicio = CrearServicio(nombreBD, CrearScraperService(nombreBD), email);

        await servicio.ReintentarPendientesAsync(hoy: new DateOnly(2026, 10, 2)); // dentro del tope de antigüedad por defecto

        context.ChangeTracker.Clear();
        var primero = await context.AcuerdosScrapeados.FindAsync(acuerdoQueSiFunciona.Id);
        Assert.True(primero!.NotificacionEnviada); // no se perdió aunque el segundo tronara después

        var segundo = await context.AcuerdosScrapeados.FindAsync(acuerdoQueFalla.Id);
        Assert.False(segundo!.NotificacionEnviada);
        Assert.False(segundo.AlertaReintentoEnviada); // el aviso también falló -- se reintenta el próximo ciclo
    }

    [Fact]
    public async Task AcuerdoDemasiadoAntiguo_NuncaIntentaNotificarAlLitigante_AvisaDirectoAlEquipo()
    {
        // Reproduce el incidente real del 18-oct: un acuerdo con semanas de antigüedad
        // (backfill histórico) nunca debe generar un intento de correo al litigante --
        // va directo al aviso del equipo, sin pasar por EnviarNotificacionManualAsync.
        var nombreBD = nameof(AcuerdoDemasiadoAntiguo_NuncaIntentaNotificarAlLitigante_AvisaDirectoAlEquipo);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (litigante, _, acuerdo) = await SembrarAcuerdoVisibleSinNotificarAsync(context);
        var acuerdoViejo = await context.AcuerdosScrapeados.FirstAsync();
        acuerdoViejo.FechaAcuerdo = new DateOnly(2026, 9, 1); // seis semanas antes del "hoy" de la prueba
        await context.SaveChangesAsync();
        var email = new FakeEmailService();
        var servicio = CrearServicio(nombreBD, CrearScraperService(nombreBD), email);

        await servicio.ReintentarPendientesAsync(hoy: new DateOnly(2026, 10, 18));

        var unico = Assert.Single(email.Enviados);
        Assert.Equal("equipo@despacho.com", unico.Email); // nunca el correo del litigante
        Assert.NotEqual(litigante.Email, unico.Email);
        Assert.Contains("revisión manual", unico.Asunto, StringComparison.OrdinalIgnoreCase);
        context.ChangeTracker.Clear();
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.False(actualizado!.NotificacionEnviada);
        Assert.True(actualizado.AlertaReintentoEnviada);
    }
}

// DJ-106: antes el ciclo dependía de la hora exacta de arranque de la app
// (PeriodicTimer anclado al deploy) -- esto prueba que, sin importar a qué
// minuto arrancó, la siguiente corrida cae siempre en un múltiplo exacto del
// intervalo contado desde medianoche.
public class ProximaCorridaAlineadaTests
{
    [Fact]
    public void ConIntervaloDeUnaHora_CaeEnLaProximaHoraEnPunto()
    {
        var ahora = new DateTime(2026, 10, 22, 14, 23, 7);
        var resultado = ReintentoNotificacionesService.ProximaCorridaAlineada(ahora, TimeSpan.FromMinutes(60));

        Assert.Equal(new DateTime(2026, 10, 22, 15, 0, 0), resultado);
    }

    [Fact]
    public void JustoEnPunto_ProgramaLaSiguiente_NoLaMisma()
    {
        var ahora = new DateTime(2026, 10, 22, 15, 0, 0);
        var resultado = ReintentoNotificacionesService.ProximaCorridaAlineada(ahora, TimeSpan.FromMinutes(60));

        Assert.Equal(new DateTime(2026, 10, 22, 16, 0, 0), resultado);
    }

    [Fact]
    public void ConIntervaloDeMediaHora_CaeEnElSiguienteMultiploDeslaMedianoche()
    {
        var ahora = new DateTime(2026, 10, 22, 14, 23, 7);
        var resultado = ReintentoNotificacionesService.ProximaCorridaAlineada(ahora, TimeSpan.FromMinutes(30));

        Assert.Equal(new DateTime(2026, 10, 22, 14, 30, 0), resultado);
    }
}
