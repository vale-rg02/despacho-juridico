using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;


namespace DespachoJuridico.API.Services;

public class ScraperAcuerdosService : BackgroundService
{
    private const int UmbralFallosConsecutivos = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScraperAcuerdosService> _logger;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<int, int> _fallosConsecutivosPorJuzgado = new();

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
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
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

        _logger.LogInformation("Expedientes activos consultados: {Count}", expedientes.Count);

        foreach (var (idUnidad, nombreJuzgado) in Juzgados)
        {
            List<(string NumeroExpediente, string Partes, string Sintesis, DateOnly FechaAcuerdo)> acuerdos;

            try
            {
                acuerdos = await ScrapearJuzgadoAsync(idUnidad, nombreJuzgado, fecha);
                _fallosConsecutivosPorJuzgado[idUnidad] = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scrapeando juzgado {IdUnidad}", idUnidad);

                var fallos = _fallosConsecutivosPorJuzgado.GetValueOrDefault(idUnidad) + 1;
                _fallosConsecutivosPorJuzgado[idUnidad] = fallos;

                if (fallos >= UmbralFallosConsecutivos)
                {
                    _logger.LogCritical(
                        "El juzgado {IdUnidad} ({Nombre}) no responde desde hace {Fallos} intentos consecutivos",
                        idUnidad, nombreJuzgado, fallos);
                }

                await Task.Delay(TimeSpan.FromSeconds(2));
                continue;
            }

            foreach (var acuerdo in acuerdos)
            {
                try
                {
                    // Buscar match con expedientes del despacho
                    var expediente = expedientes.FirstOrDefault(e =>
                        NormalizarNumero(e.NumeroExpediente) == NormalizarNumero(acuerdo.NumeroExpediente) &&
                        JuzgadoCoincide(e.Juzgado ?? "", nombreJuzgado));

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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error procesando acuerdo del expediente {Numero} en juzgado {Juzgado}",
                        acuerdo.NumeroExpediente, nombreJuzgado);
                }
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

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("Resultado", out var resultadoArray)) return resultado;
        if (resultadoArray.ValueKind != JsonValueKind.Array) return resultado;


        foreach (var item in resultadoArray.EnumerateArray())
        {
            var asunto = item.TryGetProperty("Asunto", out var a) ? a.GetString() ?? "" : "";
            var anio = item.TryGetProperty("Anio", out var an) ? an.GetString() ?? "" : "";
            var partes = item.TryGetProperty("Partes", out var p) ? p.GetString() ?? "" : "";
            var sintesis = item.TryGetProperty("Sintesis", out var s) ? s.GetString() ?? "" : "";

            // Construir número de expediente completo
            var numeroExpediente = string.IsNullOrWhiteSpace(anio) ? asunto : $"{asunto}/{anio}";

            if (string.IsNullOrWhiteSpace(asunto)) continue;

            resultado.Add((numeroExpediente, partes, sintesis, fecha));
        }

            _logger.LogInformation("Juzgado {IdUnidad}: {Count} acuerdos encontrados", idUnidad, resultado.Count);
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
        var partes = numero.Trim().Replace(" ", "").Split('/');
        if (partes.Length == 2)
        {
            var num = partes[0].TrimStart('0');
            var anio = partes[1].Trim();
            return $"{num}/{anio}";
        }
        return numero.Trim().ToUpperInvariant().TrimStart('0');
    }

    private static bool JuzgadoCoincide(string juzgadoExpediente, string nombreJuzgadoScrapeado)
    {
        if (string.IsNullOrWhiteSpace(juzgadoExpediente)) return false;

        var exp = juzgadoExpediente.Trim().ToLowerInvariant();
        var scr = nombreJuzgadoScrapeado.Trim().ToLowerInvariant();

        // Mapeo directo
        if (exp.Contains("1ro civil") || exp.Contains("primero civil"))
            return scr.Contains("1ro civil");
        if (exp.Contains("2do civil") || exp.Contains("segundo civil"))
            return scr.Contains("2do civil");
        if (exp.Contains("3ro civil") || exp.Contains("tercero civil"))
            return scr.Contains("3ro civil");
        if (exp.Contains("1ro mercantil") || exp.Contains("primero mercantil") || exp.Contains("1ro oral mercantil"))
            return scr.Contains("1ro mercantil");
        if (exp.Contains("2do mercantil") || exp.Contains("segundo mercantil") || exp.Contains("2do oral mecantil") || exp.Contains("2do oral mercantil"))
            return scr.Contains("2do mercantil");
        if (exp.Contains("3ro mercantil") || exp.Contains("tercero mercantil"))
            return scr.Contains("3ro mercantil");
        if (exp.Contains("4to mercantil") || exp.Contains("cuarto mercantil"))
            return scr.Contains("4to mercantil");
        if (exp.Contains("1ro familiar") || exp.Contains("primero familiar"))
            return scr.Contains("1ro familiar");
        if (exp.Contains("2do familiar") || exp.Contains("segundo familiar"))
            return scr.Contains("2do familiar");
        if (exp.Contains("3ro familiar") || exp.Contains("tercero familiar"))
            return scr.Contains("3ro familiar");
        if (exp.Contains("4to familiar") || exp.Contains("cuarto familiar"))
            return scr.Contains("4to familiar");
        if (exp.Contains("arrendamiento"))
            return scr.Contains("arrendamiento");
        if (exp.Contains("1er tribunal colegiado") || exp.Contains("primer tribunal colegiado") || exp.Contains("1ro tribunal colegiado"))
            return scr.Contains("1er tribunal colegiado");
        if (exp.Contains("2do tribunal colegiado") || exp.Contains("segundo tribunal colegiado"))
            return scr.Contains("2do tribunal colegiado");
        if (exp.Contains("secretaría general") || exp.Contains("secretaria general"))
            return scr.Contains("secretaría general");

        return false;
    }
}