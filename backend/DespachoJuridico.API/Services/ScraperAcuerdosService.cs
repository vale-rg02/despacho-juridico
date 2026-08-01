using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
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
        // ── HERMOSILLO ─────────────────────────────────────────────────────
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
        { 905, "Juzgado Especializado Violencia de Género Hermosillo" },
        { 276, "1er Tribunal Colegiado 1er Circuito" },
        { 277, "2do Tribunal Colegiado 1er Circuito" },
        { 175, "Secretaría General de Acuerdos Hermosillo" },

        // ── CAJEME (Ciudad Obregón) ────────────────────────────────────────
        { 135, "1ro Civil Cajeme" },
        { 136, "2do Civil Cajeme" },
        { 137, "3ro Civil Cajeme" },
        { 138, "4to Civil Cajeme" },
        { 139, "1ro Familiar Cajeme" },
        { 140, "2do Familiar Cajeme" },
        { 141, "3ro Familiar Cajeme" },
        { 901, "Juzgado Especial Familiar Cajeme" },
        { 142, "1ro Penal Cajeme" },
        { 232, "Juzgado Oral Penal Cajeme" },
        { 146, "2do Mixto Cajeme" },
        { 278, "1ro Mixto Cajeme" },
        { 275, "1er Tribunal Colegiado 2do Circuito" },
        { 324, "Tribunal Laboral Cajeme" },

        // ── AGUA PRIETA ───────────────────────────────────────────────────
        { 127, "Juzgado 1ro Civil/Mercantil/Penal Agua Prieta" },
        { 128, "Juzgado 1ro Mixto Agua Prieta" },
        { 230, "Juzgado Oral Penal Agua Prieta" },

        // ── ÁLAMOS ────────────────────────────────────────────────────────
        { 129, "Juzgado Mixto Álamos" },

        // ── CABORCA ───────────────────────────────────────────────────────
        { 131, "Juzgado 1ro Civil Caborca" },
        { 231, "Juzgado Oral Penal Caborca" },
        { 299, "Juzgado Mixto Especializado Caborca" },
        { 274, "1er Tribunal Colegiado Caborca" },

        // ── CANANEA ───────────────────────────────────────────────────────
        { 133, "Juzgado Mixto Cananea" },
        { 904, "Sala Oral Penal Cananea" },

        // ── CUMPAS ────────────────────────────────────────────────────────
        { 147, "Juzgado Mixto Cumpas" },

        // ── GUAYMAS ───────────────────────────────────────────────────────
        { 148, "Juzgado 1ro Civil Guaymas" },
        { 150, "Juzgado Civil/Familiar Especializado Guaymas" },
        { 207, "Juzgado 1ro Familiar Guaymas" },
        { 233, "Juzgado Oral Penal Guaymas" },
        { 325, "Tribunal Laboral Guaymas" },

        // ── HUATABAMPO ────────────────────────────────────────────────────
        { 179, "Juzgado 1ro Civil Huatabampo" },
        { 180, "Juzgado 1ro Penal Huatabampo" },
        { 262, "Sala Oral Penal Huatabampo" },

        // ── MAGDALENA ─────────────────────────────────────────────────────
        { 183, "Juzgado Mixto Magdalena" },

        // ── NAVOJOA ───────────────────────────────────────────────────────
        { 184, "Juzgado 1ro Civil Navojoa" },
        { 185, "Juzgado 1ro Familiar Navojoa" },
        { 234, "Juzgado Oral Penal Navojoa" },
        { 326, "Tribunal Laboral Navojoa" },

        // ── NOGALES ───────────────────────────────────────────────────────
        { 188, "Juzgado 1ro Civil Nogales" },
        { 189, "Juzgado 1ro Familiar Nogales" },
        { 190, "Juzgado 2do Familiar Nogales" },
        { 235, "Juzgado Oral Penal Nogales" },
        { 327, "Tribunal Laboral Nogales" },

        // ── PUERTO PEÑASCO ────────────────────────────────────────────────
        { 203, "Juzgado 1ro Civil Puerto Peñasco" },
        { 264, "Sala Oral Penal Puerto Peñasco" },
        { 328, "Tribunal Laboral Puerto Peñasco" },

        // ── SAHUARIPA ─────────────────────────────────────────────────────
        { 194, "Juzgado Mixto Sahuaripa" },

        // ── SAN LUIS RÍO COLORADO ─────────────────────────────────────────
        { 195, "Juzgado 1ro Civil San Luis Río Colorado" },
        { 206, "Juzgado Familiar San Luis Río Colorado" },
        { 302, "Juzgado Penal/Familiar San Luis Río Colorado" },
        { 236, "Juzgado Oral Penal San Luis Río Colorado" },
        { 329, "Tribunal Laboral San Luis Río Colorado" },

        // ── URES ──────────────────────────────────────────────────────────
        { 200, "Juzgado Mixto Ures" },
    };

    // IdUnidad de los juzgados de Hermosillo — donde viven los expedientes del
    // despacho. El resto son juzgados foráneos (reciben exhortos): ahí el
    // "Juzgado" registrado en el expediente no corresponde al juzgado que
    // publica el acuerdo, así que el match se hace solo por número de expediente.
    private static readonly HashSet<int> JuzgadosHermosillo = new()
    {
        152, 153, 154, 155, 156, 157, 158, 159, 160,
        161, 174, 175, 276, 277, 296, 905
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

    public async Task<ResultadoScrapingResponse> EjecutarScrapingAsync(DateOnly? fechaConsulta = null)
    {
        var fecha = fechaConsulta ?? DateOnly.FromDateTime(DateTime.Now);
        _logger.LogInformation("Iniciando scraping de acuerdos para {Fecha}", fecha);

        var resultado = new ResultadoScrapingResponse { Fecha = fecha };

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        // Obtener todos los números de expediente activos
        var expedientes = await context.Expedientes
            .Include(e => e.UsuarioAsignado)
            .Where(e => e.Estado != Models.Enums.EstadoExpediente.Cerrado)
            .ToListAsync();

        resultado.ExpedientesConsultados = expedientes.Count;
        _logger.LogInformation("Expedientes activos consultados: {Count}", expedientes.Count);

        foreach (var (idUnidad, nombreJuzgado) in Juzgados)
        {
            List<(string NumeroExpediente, string Partes, string Sintesis, DateOnly FechaAcuerdo, string? TipoAsunto)> acuerdos;

            try
            {
                acuerdos = await ScrapearJuzgadoAsync(idUnidad, nombreJuzgado, fecha);
                _fallosConsecutivosPorJuzgado[idUnidad] = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scrapeando juzgado {IdUnidad}", idUnidad);
                resultado.JuzgadosConError.Add(nombreJuzgado);

                var fallos = _fallosConsecutivosPorJuzgado.GetValueOrDefault(idUnidad) + 1;
                _fallosConsecutivosPorJuzgado[idUnidad] = fallos;

                if (fallos >= UmbralFallosConsecutivos)
                {
                    _logger.LogCritical(
                        "El juzgado {IdUnidad} ({Nombre}) no responde desde hace {Fallos} intentos consecutivos",
                        idUnidad, nombreJuzgado, fallos);
                }

                await Task.Delay(TimeSpan.FromSeconds(5));
                continue;
            }

            foreach (var acuerdo in acuerdos)
            {
                try
                {
                    // Buscar match con expedientes del despacho
                    Expediente? expediente;
                    if (JuzgadosHermosillo.Contains(idUnidad))
                    {
                        // Juzgados de Hermosillo: match por número Y juzgado
                        expediente = expedientes.FirstOrDefault(e =>
                            NormalizarNumero(e.NumeroExpediente) == NormalizarNumero(acuerdo.NumeroExpediente) &&
                            JuzgadoCoincide(e.Juzgado ?? "", nombreJuzgado));
                    }
                    else
                    {
                        // Juzgados foráneos: el "Juzgado" del expediente es el de origen
                        // (Hermosillo), no el que recibe el exhorto, así que no podemos
                        // validarlo aquí como con Hermosillo. El número de expediente por
                        // sí solo no basta — se repite entre juzgados de todo el estado y
                        // generaba falsos positivos (acuerdos de casos ajenos emparejados
                        // con expedientes del despacho). Exigimos además que las partes
                        // scrapeadas mencionen a la parte demandada del expediente.
                        expediente = expedientes.FirstOrDefault(e =>
                            NormalizarNumero(e.NumeroExpediente) == NormalizarNumero(acuerdo.NumeroExpediente) &&
                            PartesCoinciden(e.ParteDemandada, acuerdo.Partes));
                    }

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
                        NotificacionEnviada = false,
                        TipoAsunto = acuerdo.TipoAsunto,
                        // ADISON no expone el estado/ciudad destino de un exhorto — solo
                        // detectamos que hubo actividad relacionada por la síntesis; el
                        // destino se captura manualmente después si aplica
                        EsExhorto = acuerdo.Sintesis.Contains("exhorto", StringComparison.OrdinalIgnoreCase)
                    };

                    context.AcuerdosScrapeados.Add(nuevoAcuerdo);
                    await context.SaveChangesAsync();

                    // Enviar notificación por correo
                    await EnviarNotificacionAsync(emailService, expediente, nuevoAcuerdo);
                    nuevoAcuerdo.NotificacionEnviada = true;
                    await context.SaveChangesAsync();

                    resultado.AcuerdosDetectados.Add(new AcuerdoDetectadoResumen
                    {
                        NumeroExpediente = expediente.NumeroExpediente,
                        Juzgado = nombreJuzgado,
                        Sintesis = acuerdo.Sintesis,
                        FechaAcuerdo = acuerdo.FechaAcuerdo
                    });

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
        return resultado;
    }

    private async Task<List<(string NumeroExpediente, string Partes, string Sintesis, DateOnly FechaAcuerdo, string? TipoAsunto)>> ScrapearJuzgadoAsync(
        int idUnidad, string nombreJuzgado, DateOnly fecha)
    {
        var resultado = new List<(string, string, string, DateOnly, string?)>();

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
            var tipoAsunto = item.TryGetProperty("TipoAsunto", out var t) ? t.GetString() : null;

            // Construir número de expediente completo
            var numeroExpediente = string.IsNullOrWhiteSpace(anio) ? asunto : $"{asunto}/{anio}";

            if (string.IsNullOrWhiteSpace(asunto)) continue;

            resultado.Add((numeroExpediente, partes, sintesis, fecha, tipoAsunto));
        }

            _logger.LogInformation("Juzgado {IdUnidad}: {Count} acuerdos encontrados", idUnidad, resultado.Count);
        return resultado;
    }

    private async Task EnviarNotificacionAsync(IEmailService emailService, Expediente expediente, AcuerdoScrapeado acuerdo)
    {
        if (expediente.UsuarioAsignado == null) return;

        var asunto = $"Nuevo acuerdo judicial — Exp. {expediente.NumeroExpediente}";

        var nombreEnc = System.Net.WebUtility.HtmlEncode(expediente.UsuarioAsignado.Nombre);
        var numeroExpedienteEnc = System.Net.WebUtility.HtmlEncode(expediente.NumeroExpediente);
        var parteDemandadaEnc = System.Net.WebUtility.HtmlEncode(expediente.ParteDemandada);
        var juzgadoEnc = System.Net.WebUtility.HtmlEncode(acuerdo.NombreJuzgado);
        var partesEnc = System.Net.WebUtility.HtmlEncode(acuerdo.Partes);
        var sintesisEnc = System.Net.WebUtility.HtmlEncode(acuerdo.Sintesis);

        var cuerpo = $@"
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
  .sintesis{{background:#fffdf7;border:1px solid #e0ddd6;padding:16px 20px;margin:16px 0;border-radius:6px;font-size:14px;line-height:1.6;color:#555;}}
  .footer{{background:#f7f5f0;padding:20px 40px;text-align:center;border-top:1px solid #e0ddd6;}}
  .footer p{{font-size:12px;color:#888;margin:0;}}
</style></head>
<body><div class='container'>
  <div class='header'>
    <h1>Despacho Jurídico Acedo e Hijos</h1>
    <p>Nuevo acuerdo judicial detectado</p>
  </div>
  <div class='body'>
    <p>Estimado(a) {nombreEnc},</p>
    <p>El sistema ha detectado un nuevo acuerdo publicado por el <strong>{juzgadoEnc}</strong>
    correspondiente al siguiente expediente a su cargo:</p>
    <div class='highlight'>
      <p><strong>Expediente:</strong> {numeroExpedienteEnc}</p>
      <p><strong>Parte demandada:</strong> {parteDemandadaEnc}</p>
      <p><strong>Juzgado:</strong> {juzgadoEnc}</p>
      <p><strong>Fecha del acuerdo:</strong> {acuerdo.FechaAcuerdo:dd/MM/yyyy}</p>
      <p><strong>Partes:</strong> {partesEnc}</p>
    </div>
    <p><strong>Síntesis del acuerdo:</strong></p>
    <div class='sintesis'>{sintesisEnc}</div>
    <p>Le recomendamos revisar el expediente en el sistema para tomar las acciones correspondientes.</p>
    <p>Atentamente,<br><strong>Despacho Jurídico Acedo e Hijos</strong></p>
  </div>
  <div class='footer'>
    <p>Este es un mensaje automático del Sistema de Gestión de Expedientes.</p>
    <p>Por favor no responda a este correo.</p>
  </div>
</div></body></html>";

        await emailService.EnviarAsync(
            expediente.UsuarioAsignado.Email,
            expediente.UsuarioAsignado.Nombre,
            asunto,
            cuerpo);
    }

    private static string NormalizarNumero(string numero)
    {
        var partes = numero.Trim().Replace(" ", "").Split('/', '-');
        if (partes.Length == 2)
        {
            var num = partes[0].TrimStart('0');
            var anio = partes[1].Trim();
            return $"{num}/{anio}";
        }
        return numero.Trim().ToUpperInvariant().TrimStart('0');
    }

    // Segunda validación para juzgados foráneos, donde no se puede comparar el
    // juzgado. Verifica que el texto de "partes" que trae ADISON mencione a la
    // parte demandada del expediente (comparación laxa: sin acentos/mayúsculas
    // ni espacios de más, buscada como substring dentro de las partes).
    private static bool PartesCoinciden(string parteDemandada, string partesScrapeadas)
    {
        if (string.IsNullOrWhiteSpace(parteDemandada) || string.IsNullOrWhiteSpace(partesScrapeadas))
            return false;

        return NormalizarTexto(partesScrapeadas).Contains(NormalizarTexto(parteDemandada));
    }

    private static string NormalizarTexto(string texto)
    {
        var normalizado = texto.Trim().ToUpperInvariant();
        var sinAcentos = string.Concat(
            normalizado.Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark));
        return System.Text.RegularExpressions.Regex.Replace(sinAcentos, @"\s+", " ");
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