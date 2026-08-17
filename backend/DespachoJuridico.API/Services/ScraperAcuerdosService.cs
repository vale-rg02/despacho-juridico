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
        // Oral Mercantil es un ramo y juzgado distintos al Mercantil tradicional
        // (IdUnidad propio en ADISON, numeración de expedientes independiente) —
        // no agregar sin también validar JuzgadoCoincide, ver nota ahí abajo.
        { 173, "1ro Oral Mercantil Hermosillo" },
        { 300, "2do Oral Mercantil Hermosillo" },
        { 174, "Arrendamiento Hermosillo" },
        { 155, "1ro Familiar Hermosillo" },
        { 156, "2do Familiar Hermosillo" },
        { 157, "3ro Familiar Hermosillo" },
        { 296, "4to Familiar Hermosillo" },
        { 905, "Juzgado Especializado Violencia de Género Hermosillo" },
        { 276, "1er Tribunal Colegiado 1er Circuito" },
        { 277, "2do Tribunal Colegiado 1er Circuito" },
        { 175, "Secretaría General de Acuerdos Hermosillo" },

        // Agregados tras auditoría contra el catálogo oficial de ADISON (agosto
        // 2026) — juzgados de Hermosillo que nunca se habían consultado. No están
        // en JuzgadosHermosillo (más abajo): no hay un patrón de JuzgadoCoincide
        // ya probado para Penal/Laboral/Adolescentes, así que pasan por la ruta
        // foránea (match por número + verificación de Partes) en vez de asumir
        // coincidencia solo por juzgado.
        { 162, "1ro Penal Hermosillo" },
        { 163, "2do Penal Hermosillo" },
        { 164, "3ro Penal Hermosillo" },
        { 166, "5to Penal Hermosillo" },
        { 205, "Juzgado Oral Penal Hermosillo" },
        { 171, "Juzgado Adolescentes Hermosillo" },
        { 178, "Tribunal Unitario Regional Adolescentes/Penal Oral Hermosillo" },
        { 208, "Juzgado Ejecución de Sanciones Hermosillo" },
        { 322, "1er Tribunal Laboral Hermosillo" },
        { 332, "2do Tribunal Laboral Hermosillo" },
        { 333, "3er Tribunal Laboral Hermosillo" },

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
        { 204, "Juzgado 1ro Penal Puerto Peñasco" },

        // ── SAHUARIPA ─────────────────────────────────────────────────────
        { 194, "Juzgado Mixto Sahuaripa" },

        // ── SIN DISTRITO CONFIRMADO ───────────────────────────────────────
        // ADISON no especifica distrito en el nombre oficial; no se confirmó su
        // ubicación, así que no entra a JuzgadosHermosillo (ruta foránea, más segura).
        { 297, "Juzgado Familiar Competencia Especializada" },

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
        161, 174, 175, 276, 277, 296, 905, 173, 300
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
            try
            {
                await EjecutarScrapingAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el ciclo automático de scraping de acuerdos");
            }
        }
    }

    public async Task<ResultadoScrapingResponse> EjecutarScrapingAsync(DateOnly? fechaConsulta = null, bool dryRun = false, IReadOnlySet<int>? idsUnidad = null)
    {
        var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var fecha = fechaConsulta ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaHoraria));
        _logger.LogInformation("Iniciando scraping de acuerdos para {Fecha} (dryRun={DryRun})", fecha, dryRun);

        var resultado = new ResultadoScrapingResponse { Fecha = fecha, DryRun = dryRun };

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
            if (idsUnidad != null && !idsUnidad.Contains(idUnidad)) continue;

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
                        // Juzgados foráneos: match solo por número de expediente
                        // (el juzgado registrado en el expediente es el de Hermosillo, no el
                        // receptor del exhorto). El número por sí solo no basta — se repite
                        // entre juzgados de todo el estado y generó falsos positivos reales
                        // en el backfill de julio 2026. La confianza del match (comparando
                        // Partes) se evalúa más abajo, pero por ahora solo clasifica — no
                        // filtra — mientras se revisa en dry-run (ver Fase 1/Fase 2).
                        expediente = expedientes.FirstOrDefault(e =>
                            NormalizarNumero(e.NumeroExpediente) == NormalizarNumero(acuerdo.NumeroExpediente));
                    }

                    if (expediente == null) continue;

                    var esForaneo = !JuzgadosHermosillo.Contains(idUnidad);
                    string? confianza = null;
                    var oculto = false;

                    if (esForaneo)
                    {
                        // Verificación por Partes: el matching foráneo solo compara número
                        // de expediente (ver comentario arriba), lo que causó falsos positivos
                        // reales en el backfill de julio 2026 (ej. exp. 258/2026 coincidiendo
                        // entre Tribunal Laboral Cajeme y Juzgado Oral Penal Agua Prieta).
                        confianza = PartesCoinciden(expediente.ParteDemandada, acuerdo.Partes) ? "Alta" : "Baja";

                        // Fase 2: los de baja confianza se guardan ocultos (Opción B) — no se
                        // pierden por si el criterio se equivoca (hay casos reales que salen
                        // baja confianza solo por diferencias de formato entre cómo el despacho
                        // captura las partes y cómo las publica ADISON), pero no se notifican
                        // ni se muestran en la sección de Acuerdos hasta revisarlos.
                        oculto = confianza == "Baja";

                        if (dryRun)
                        {
                            resultado.MatchesForaneosEvaluados.Add(new MatchForaneoEvaluado
                            {
                                NumeroExpediente = expediente.NumeroExpediente,
                                Juzgado = nombreJuzgado,
                                ParteDemandadaExpediente = expediente.ParteDemandada,
                                PartesAcuerdo = acuerdo.Partes,
                                Confianza = confianza,
                                Sintesis = acuerdo.Sintesis,
                                FechaAcuerdo = acuerdo.FechaAcuerdo
                            });
                        }
                    }
                    else if (dryRun)
                    {
                        // Hermosillo: no hay verificación de Partes, pero en dry-run se
                        // muestra el texto de ADISON de todos modos como referencia visual.
                        resultado.MatchesHermosilloEvaluados.Add(new MatchHermosilloEvaluado
                        {
                            NumeroExpediente = expediente.NumeroExpediente,
                            Juzgado = nombreJuzgado,
                            ParteDemandadaExpediente = expediente.ParteDemandada,
                            PartesAcuerdo = acuerdo.Partes,
                            Sintesis = acuerdo.Sintesis,
                            FechaAcuerdo = acuerdo.FechaAcuerdo
                        });
                    }

                    if (dryRun)
                    {
                        // Modo de prueba: no se escribe en AcuerdosScrapeados ni se envía correo.
                        continue;
                    }

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
                        EsExhorto = acuerdo.Sintesis.Contains("exhorto", StringComparison.OrdinalIgnoreCase),
                        Confianza = confianza,
                        Oculto = oculto
                    };

                    context.AcuerdosScrapeados.Add(nuevoAcuerdo);
                    await context.SaveChangesAsync();

                    if (!oculto)
                    {
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
                    else
                    {
                        _logger.LogInformation(
                            "Acuerdo oculto (baja confianza) Exp {Numero} en {Juzgado} — no se notifica",
                            expediente.NumeroExpediente, nombreJuzgado);
                    }
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

    // Confianza del match foráneo (no se puede comparar el juzgado ahí, a
    // diferencia de Hermosillo): ¿el texto de "partes" que trae ADISON menciona
    // a la parte demandada del expediente? (comparación laxa: sin acentos ni
    // mayúsculas ni espacios de más, buscada como substring). No es infalible
    // (hay variaciones de captura, ej. "Vanesa" vs "Vanezza"), pero reduce el
    // ruido de coincidencias que son solo por número y no tienen relación real.
    // Solo clasifica (Alta/Baja) — no filtra nada todavía, ver Fase 1/Fase 2.
    private static bool PartesCoinciden(string parteDemandada, string partesScrapeadas)
    {
        if (string.IsNullOrWhiteSpace(parteDemandada) || string.IsNullOrWhiteSpace(partesScrapeadas))
            return false;

        var parteRelevante = QuitarSufijoOtroDemandado(parteDemandada);
        return NormalizarTexto(partesScrapeadas).Contains(NormalizarTexto(parteRelevante));
    }

    // "Y OTRA"/"Y OTRO"/"Y OTROS"/"Y OTRAS" es un sufijo genérico que el despacho
    // usa al capturar ParteDemandada cuando hay codemandados sin nombrar — pero
    // ADISON sí los nombra explícitamente, así que ese sufijo nunca aparece tal
    // cual en el texto de "partes" aunque sea el mismo caso. Comparamos solo lo
    // que viene antes del sufijo (validado con datos reales de julio 2026: 1 de
    // 273 matches foráneos pasó de Baja a Alta con este ajuste, sin generar
    // nuevos falsos positivos en el resto).
    private static string QuitarSufijoOtroDemandado(string parteDemandada)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            parteDemandada, @"^(.*?)\s+Y\s+OTR[OA]S?\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : parteDemandada;
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
        // Oral Mercantil es un juzgado distinto al Mercantil tradicional (mismo
        // número de juzgado, pero ramo y numeración de expedientes independientes)
        // — "oral" nunca debe cruzarse con "no oral", en ningún sentido.
        if (exp.Contains("1ro oral mercantil") || exp.Contains("primero oral mercantil"))
            return scr.Contains("1ro oral mercantil");
        if (exp.Contains("1ro mercantil") || exp.Contains("primero mercantil"))
            return scr.Contains("1ro mercantil") && !scr.Contains("oral");
        if (exp.Contains("2do oral mercantil") || exp.Contains("2do oral mecantil") || exp.Contains("segundo oral mercantil"))
            return scr.Contains("2do oral mercantil");
        if (exp.Contains("2do mercantil") || exp.Contains("segundo mercantil"))
            return scr.Contains("2do mercantil") && !scr.Contains("oral");
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