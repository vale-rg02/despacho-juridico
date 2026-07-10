using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using System.Xml;

namespace DespachoJuridico.API.Services;

public class ScraperAcuerdosService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScraperAcuerdosService> _logger;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    private static readonly Dictionary<int, string> Juzgados = new()
    {
        { 152, "1ro Civil Hermosillo" },
        { 153, "2do Civil Hermosillo" },
        { 154, "3ro Civil Hermosillo" },
        { 158, "1ro Mercantil Hermosillo" },
        { 159, "2do Mercantil Hermosillo" },
        { 160, "3ro Mercantil Hermosillo" },
        { 161, "4to Mercantil Hermosillo" },
        { 174, "Arrendamiento Hermosillo" },
        { 155, "1ro Familiar Hermosillo" },
        { 156, "2do Familiar Hermosillo" },
        { 157, "3ro Familiar Hermosillo" },
        { 296, "4to Familiar Hermosillo" },
        { 276, "1er Tribunal Colegiado" },
        { 277, "2do Tribunal Colegiado" },
        { 175, "Secretaría General de Acuerdos" },
    };

    public ScraperAcuerdosService(
        IServiceScopeFactory scopeFactory,
        ILogger<ScraperAcuerdosService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; DespachoBot/1.0)");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervaloMinutos = _config.GetValue<int>("ScraperAcuerdos:IntervaloMinutos", 1440);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervaloMinutos));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await EjecutarScrapingAsync();
        }
    }

    public async Task EjecutarScrapingAsync()
    {
        var fecha = DateOnly.FromDateTime(DateTime.Now);
        _logger.LogInformation("Iniciando scraping de acuerdos para {Fecha}", fecha);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        // Obtener todos los números de expediente activos
        var expedientes = await context.Expedientes
            .Include(e => e.UsuarioAsignado)
            .Where(e => e.Estado != Models.Enums.EstadoExpediente.Cerrado)
            .ToListAsync();

        foreach (var (idUnidad, nombreJuzgado) in Juzgados)
        {
            try
            {
                var acuerdos = await ScrapearJuzgadoAsync(idUnidad, nombreJuzgado, fecha);

                foreach (var acuerdo in acuerdos)
                {
                    // Buscar match con expedientes del despacho
                    var expediente = expedientes.FirstOrDefault(e =>
                        NormalizarNumero(e.NumeroExpediente) == NormalizarNumero(acuerdo.NumeroExpediente) &&
                        e.Juzgado != null &&
                        NormalizarJuzgado(e.Juzgado) == NormalizarJuzgado(nombreJuzgado));

                    if (expediente == null) continue;

                    // Verificar si ya existe este acuerdo
                    var yaExiste = await context.AcuerdosScrapeados.AnyAsync(a =>
                        a.ExpedienteId == expediente.Id &&
                        a.FechaAcuerdo == acuerdo.FechaAcuerdo &&
                        a.Sintesis == acuerdo.Sintesis);

                    if (yaExiste) continue;

                    // Guardar acuerdo
                    var nuevoAcuerdo = new AcuerdoScrapeado
                    {
                        ExpedienteId = expediente.Id,
                        NumeroExpediente = expediente.NumeroExpediente,
                        IdUnidad = idUnidad,
                        NombreJuzgado = nombreJuzgado,
                        Partes = acuerdo.Partes,
                        Sintesis = acuerdo.Sintesis,
                        FechaAcuerdo = fecha,
                        NotificacionEnviada = false
                    };

                    context.AcuerdosScrapeados.Add(nuevoAcuerdo);
                    await context.SaveChangesAsync();

                    // Enviar notificación por correo
                    await EnviarNotificacionAsync(emailService, expediente, nuevoAcuerdo);
                    nuevoAcuerdo.NotificacionEnviada = true;
                    await context.SaveChangesAsync();

                    _logger.LogInformation("Acuerdo detectado: Exp {Numero} en {Juzgado}", expediente.NumeroExpediente, nombreJuzgado);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scrapeando juzgado {IdUnidad}", idUnidad);
            }

            // Pausa entre juzgados para no sobrecargar el servidor
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        _logger.LogInformation("Scraping completado para {Fecha}", fecha);
    }

    private async Task<List<(string NumeroExpediente, string Partes, string Sintesis, DateOnly FechaAcuerdo)>> ScrapearJuzgadoAsync(
        int idUnidad, string nombreJuzgado, DateOnly fecha)
    {
        var resultado = new List<(string, string, string, DateOnly)>();

        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Accion", "Publicacion|ListaAcuerdosController|BuscarByFecha"),
            new KeyValuePair<string, string>("IdUnidad", idUnidad.ToString()),
            new KeyValuePair<string, string>("Fecha", fecha.ToString("yyyy-MM-dd")),
        });

        var response = await _httpClient.PostAsync(
            "https://adison.stjsonora.gob.mx/Controller/ActionController.php",
            formData);

        if (!response.IsSuccessStatusCode) return resultado;

        var html = await response.Content.ReadAsStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Parsear filas de la tabla de acuerdos
        var filas = doc.DocumentNode.SelectNodes("//tr[td]");
        if (filas == null) return resultado;

        foreach (var fila in filas)
        {
            var celdas = fila.SelectNodes("td");
            if (celdas == null || celdas.Count < 3) continue;

            var asunto = celdas[0].InnerText.Trim();
            var partes = celdas[1].InnerText.Trim();
            var sintesis = celdas[2].InnerText.Trim();

            if (string.IsNullOrWhiteSpace(asunto) || string.IsNullOrWhiteSpace(sintesis)) continue;

            resultado.Add((asunto, partes, sintesis, fecha));
        }

        return resultado;
    }

    private async Task EnviarNotificacionAsync(IEmailService emailService, Expediente expediente, AcuerdoScrapeado acuerdo)
    {
        if (expediente.UsuarioAsignado == null) return;

        var asunto = $"Nuevo acuerdo: Exp. {expediente.NumeroExpediente} — {acuerdo.NombreJuzgado}";
        var cuerpo = $@"
            <h2>Nuevo acuerdo detectado</h2>
            <p><strong>Expediente:</strong> {expediente.NumeroExpediente}</p>
            <p><strong>Parte demandada:</strong> {expediente.ParteDemandada}</p>
            <p><strong>Juzgado:</strong> {acuerdo.NombreJuzgado}</p>
            <p><strong>Fecha:</strong> {acuerdo.FechaAcuerdo:dd/MM/yyyy}</p>
            <p><strong>Partes:</strong> {acuerdo.Partes}</p>
            <p><strong>Síntesis:</strong> {acuerdo.Sintesis}</p>
        ";

        await emailService.EnviarAsync(
            expediente.UsuarioAsignado.Email,
            expediente.UsuarioAsignado.Nombre,
            asunto,
            cuerpo);
    }

    private static string NormalizarNumero(string numero)
    {
        return numero.Trim().ToUpperInvariant()
            .Replace(" ", "")
            .Replace("-", "/");
    }

    private static string NormalizarJuzgado(string juzgado)
    {
        return juzgado.Trim().ToLowerInvariant()
            .Replace("primero", "1ro").Replace("1ro", "1")
            .Replace("segundo", "2do").Replace("2do", "2")
            .Replace("tercero", "3ro").Replace("3ro", "3")
            .Replace("cuarto", "4to").Replace("4to", "4")
            .Replace("civil", "").Replace("mercantil", "")
            .Replace("familiar", "").Replace(" ", "");
    }
}