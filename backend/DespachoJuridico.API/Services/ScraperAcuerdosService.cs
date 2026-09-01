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
        // 2026) — juzgados de Hermosillo que nunca se habían consultado. Sí están
        // en JuzgadosHermosillo (más abajo): tienen su propio patrón de
        // JuzgadoCoincide (Penal/Laboral/Adolescentes/Ejecución de Sanciones), igual
        // que Civil/Familiar/Mercantil — antes de esto pasaban por la ruta foránea
        // (match solo por número), lo que generaba ruido constante: la materia de un
        // expediente no cambia nunca, así que un civil/mercantil del despacho jamás
        // debería "coincidir" de verdad con un juzgado Laboral o Penal, y en la
        // práctica el 100% de esos matches (102 registros históricos) eran falsos
        // positivos — ver docs/mecanica-legal-sonora.md.
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
        161, 174, 175, 276, 277, 296, 905, 173, 300,
        // Penal, Adolescentes, Ejecución de Sanciones y Laboral — agregados junto
        // con su patrón de JuzgadoCoincide (ver comentario en el diccionario Juzgados).
        162, 163, 164, 166, 205, 171, 178, 208, 322, 332, 333
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

    // Corre varias veces al día en vez de una sola vez — ADISON sigue publicando
    // acuerdos durante el día (no todo de golpe a medianoche), así que una sola
    // corrida diaria se queda con lo que ya estaba listo esa madrugada y nunca
    // vuelve a revisar el día anterior. Horario configurable, por defecto cada
    // 2 horas de 00:05 a 18:05 hora de Hermosillo — fuera de esa ventana no corre,
    // para no pegarle a ADISON de madrugada sin actividad real.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var intervaloHoras = _config.GetValue<int>("ScraperAcuerdos:IntervaloHorasDiurno", 2);
        var horaInicio = _config.GetValue<int>("ScraperAcuerdos:HoraInicioLocal", 0);
        var horaFin = _config.GetValue<int>("ScraperAcuerdos:HoraFinLocal", 18);

        while (!stoppingToken.IsCancellationRequested)
        {
            var ahoraLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaHoraria);
            var proximaCorridaLocal = ProximaCorridaProgramada(ahoraLocal, horaInicio, horaFin, intervaloHoras);
            var proximaCorridaUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(proximaCorridaLocal, DateTimeKind.Unspecified), zonaHoraria);
            var espera = proximaCorridaUtc - DateTime.UtcNow;

            if (espera > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(espera, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

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

    // Calcula la siguiente hora programada (cada intervaloHoras, entre horaInicio
    // y horaFin, minuto :05) a partir de "ahora" en hora local de Hermosillo. Si
    // ya pasaron todas las corridas de hoy, programa la primera de mañana.
    internal static DateTime ProximaCorridaProgramada(DateTime ahoraLocal, int horaInicio, int horaFin, int intervaloHoras)
    {
        var hoy = ahoraLocal.Date;

        for (var hora = horaInicio; hora <= horaFin; hora += intervaloHoras)
        {
            var candidato = hoy.AddHours(hora).AddMinutes(5);
            if (candidato > ahoraLocal)
                return candidato;
        }

        return hoy.AddDays(1).AddHours(horaInicio).AddMinutes(5);
    }

    // notificar=false guarda los acuerdos detectados (con su Confianza/Oculto reales,
    // sin alterar esa lógica) pero no envía el correo — pensado para backfills de
    // fechas atrasadas, donde escribir el registro histórico correcto no debe
    // traducirse en un correo "hoy" avisando de algo que pasó hace semanas. Por
    // default true: no cambia el comportamiento normal del ciclo automático.
    public async Task<ResultadoScrapingResponse> EjecutarScrapingAsync(DateOnly? fechaConsulta = null, bool dryRun = false, IReadOnlySet<int>? idsUnidad = null, bool notificar = true)
    {
        var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var fecha = fechaConsulta ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaHoraria));
        _logger.LogInformation("Iniciando scraping de acuerdos para {Fecha} (dryRun={DryRun})", fecha, dryRun);

        // Umbral de similitud aproximada para PartesCoinciden (0.0 a 1.0) — cuando
        // la coincidencia exacta por substring falla, se acepta igual si el nombre
        // se parece lo suficiente (tolera variaciones de ortografía entre cómo el
        // despacho captura la parte y cómo la publica el juzgado, ej. "Corona" vs
        // "Coronado"). Configurable sin redeploy vía ScraperAcuerdos:UmbralSimilitudPartes.
        var umbralSimilitudPartes = _config.GetValue<double>("ScraperAcuerdos:UmbralSimilitudPartes", 0.8);

        var resultado = new ResultadoScrapingResponse { Fecha = fecha, DryRun = dryRun };

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        // Obtener todos los números de expediente activos
        var expedientes = await context.Expedientes
            .Include(e => e.UsuarioAsignado)
            .Include(e => e.Banco)
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
                    // Exh. (Exhorto) y Cuad. (Cuadernillo) son series de numeración propias
                    // del juzgado que recibe el exhorto / abre el cuadernillo — no la del
                    // expediente que lo originó. Que el número coincida con un expediente de
                    // Hermosillo no dice nada por sí solo: son series independientes que solo
                    // comparten número por coincidencia (ver docs/mecanica-legal-sonora.md #2,
                    // caso real exp. 476/2026 vs Exhorto 476/2026 de San Luis Río Colorado).
                    var esSerieAuxiliar = EsSerieAuxiliar(acuerdo.TipoAsunto);
                    string? confianza = null;
                    var oculto = false;

                    if (EsJurisdiccionVoluntaria(expediente.TipoJuicio))
                    {
                        // La Jurisdicción Voluntaria no es un procedimiento adversarial: no
                        // existe "parte demandada" en sentido procesal, solo un promovente
                        // (típicamente un banco) que le pide al juzgado realizar un acto. ADISON
                        // a veces solo publica al promovente (caso real: exp. 434/2026, "SE RADICA
                        // DEMANDA.- BBVA MEXICO..." sin mencionar a la parte a notificar) — ahí
                        // PartesCoinciden contra ParteDemandada solo no basta. Pero asumir que
                        // CUALQUIER acuerdo sin ese nombre es válido tampoco funciona: el exp.
                        // 368/2026 recibió una notificación real por un acuerdo de un caso de
                        // concubinato totalmente ajeno en Cajeme, que coincidió por número pero no
                        // traía ni el nombre de la parte ni el del banco. Por eso se comparan AMBOS
                        // — ParteDemandada y Banco — y basta con que cualquiera de los dos
                        // coincida para Alta confianza; si ninguno coincide, Baja, y se oculta
                        // igual que el resto del sistema (ver docs/mecanica-legal-sonora.md).
                        (confianza, oculto) = EvaluarJurisdiccionVoluntaria(
                            expediente.ParteDemandada, expediente.Banco?.Nombre, acuerdo.Partes, umbralSimilitudPartes);

                        if (dryRun)
                        {
                            var nombreBanco = expediente.Banco?.Nombre ?? "(sin banco capturado)";

                            if (esForaneo)
                                resultado.MatchesForaneosEvaluados.Add(new MatchForaneoEvaluado
                                {
                                    NumeroExpediente = expediente.NumeroExpediente,
                                    Juzgado = nombreJuzgado,
                                    ParteDemandadaExpediente = nombreBanco,
                                    PartesAcuerdo = acuerdo.Partes,
                                    Confianza = confianza,
                                    Sintesis = acuerdo.Sintesis,
                                    FechaAcuerdo = acuerdo.FechaAcuerdo
                                });
                            else
                                resultado.MatchesHermosilloEvaluados.Add(new MatchHermosilloEvaluado
                                {
                                    NumeroExpediente = expediente.NumeroExpediente,
                                    Juzgado = nombreJuzgado,
                                    ParteDemandadaExpediente = nombreBanco,
                                    PartesAcuerdo = acuerdo.Partes,
                                    Confianza = confianza,
                                    Sintesis = acuerdo.Sintesis,
                                    FechaAcuerdo = acuerdo.FechaAcuerdo
                                });
                        }
                    }
                    else if (esForaneo || esSerieAuxiliar)
                    {
                        // Verificación por Partes: el matching foráneo (y, desde aquí, el de
                        // Exh./Cuad. aunque sea Hermosillo — ver comentario en esSerieAuxiliar
                        // arriba) solo tenía número de expediente para confiar, lo que causó
                        // falsos positivos reales en el backfill de julio 2026 (ej. exp. 258/2026
                        // coincidiendo entre Tribunal Laboral Cajeme y Juzgado Oral Penal Agua
                        // Prieta).
                        confianza = PartesCoinciden(expediente.ParteDemandada, acuerdo.Partes, umbralSimilitudPartes) ? "Alta" : "Baja";

                        // Fase 2: los de baja confianza se guardan ocultos (Opción B) — no se
                        // pierden por si el criterio se equivoca (hay casos reales que salen
                        // baja confianza solo por diferencias de formato entre cómo el despacho
                        // captura las partes y cómo las publica ADISON), pero no se notifican
                        // ni se muestran en la sección de Acuerdos hasta revisarlos.
                        oculto = confianza == "Baja";

                        if (dryRun)
                        {
                            if (esForaneo)
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
                            else
                                resultado.MatchesHermosilloEvaluados.Add(new MatchHermosilloEvaluado
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
                    else if (PartesTieneNombre(acuerdo.Partes))
                    {
                        // Hermosillo normalmente confía en número+juzgado sin verificar
                        // Partes (ver comentario en JuzgadosHermosillo) — pero esa confianza
                        // también puede fallar (hallazgo real del 20 de agosto 2026, exp.
                        // 150/2023: coincidencia de número con un caso ajeno). Cuando ADISON
                        // sí trae un nombre reconocible, vale la pena verificarlo igual que
                        // en foráneos; cuando no trae nombre (pasa seguido: "---", solo el
                        // tipo de trámite, etc.) no hay nada que comparar y se sigue confiando
                        // en número+juzgado como siempre.
                        confianza = PartesCoinciden(expediente.ParteDemandada, acuerdo.Partes, umbralSimilitudPartes) ? "Alta" : "Baja";
                        oculto = confianza == "Baja";

                        if (dryRun)
                        {
                            resultado.MatchesHermosilloEvaluados.Add(new MatchHermosilloEvaluado
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
                        // Hermosillo sin nombre en Partes: nada que verificar, se sigue
                        // confiando en número+juzgado como siempre.
                        resultado.MatchesHermosilloEvaluados.Add(new MatchHermosilloEvaluado
                        {
                            NumeroExpediente = expediente.NumeroExpediente,
                            Juzgado = nombreJuzgado,
                            ParteDemandadaExpediente = expediente.ParteDemandada,
                            PartesAcuerdo = acuerdo.Partes,
                            Confianza = null,
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

                    if (!oculto && notificar)
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
                    else if (oculto)
                    {
                        _logger.LogInformation(
                            "Acuerdo oculto (baja confianza) Exp {Numero} en {Juzgado} — no se notifica",
                            expediente.NumeroExpediente, nombreJuzgado);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Acuerdo guardado sin notificar (notificar=false) Exp {Numero} en {Juzgado}",
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

    // Vuelve a evaluar, con el algoritmo y umbral ACTUALES de PartesCoinciden (o de
    // EvaluarJurisdiccionVoluntaria para ese tipo de juicio, que también compara contra
    // el banco), los acuerdos que quedaron guardados ocultos por baja confianza
    // (matching foráneo o de Hermosillo — ver EjecutarScrapingAsync). El criterio se
    // sigue afinando (ver comentarios de PartesCoinciden y SimilitudMaximaSubcadena),
    // así que un ajuste no reclasifica por sí solo lo que ya se guardó antes del
    // cambio; este método cierra ese hueco sin tener que volver a scrapear ADISON.
    // Solo toca los registros que hoy están Oculto=true/Confianza=Baja — nunca los ya
    // visibles ni los manuales.
    public async Task<ResultadoReevaluacionResponse> ReevaluarOcultosAsync(bool dryRun = true)
    {
        var umbralSimilitudPartes = _config.GetValue<double>("ScraperAcuerdos:UmbralSimilitudPartes", 0.8);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var candidatos = await context.AcuerdosScrapeados
            .Include(a => a.Expediente).ThenInclude(e => e.UsuarioAsignado)
            .Include(a => a.Expediente).ThenInclude(e => e.Banco)
            .Where(a => a.Oculto && a.Confianza == "Baja" && !a.RegistradoManualmente)
            .ToListAsync();

        var resultado = new ResultadoReevaluacionResponse
        {
            DryRun = dryRun,
            RegistrosEvaluados = candidatos.Count
        };

        foreach (var acuerdo in candidatos)
        {
            var expediente = acuerdo.Expediente;
            if (expediente == null) continue;

            // Jurisdicción Voluntaria se reevalúa con su propio criterio (parte O banco,
            // ver EvaluarJurisdiccionVoluntaria) — el resto sigue comparando solo contra
            // ParteDemandada, exactamente igual que antes.
            var ahoraCoincide = EsJurisdiccionVoluntaria(expediente.TipoJuicio)
                ? EvaluarJurisdiccionVoluntaria(expediente.ParteDemandada, expediente.Banco?.Nombre, acuerdo.Partes, umbralSimilitudPartes).Confianza == "Alta"
                : PartesCoinciden(expediente.ParteDemandada, acuerdo.Partes, umbralSimilitudPartes);
            if (!ahoraCoincide) continue;

            resultado.RegistrosDesocultados.Add(new AcuerdoDetectadoResumen
            {
                NumeroExpediente = acuerdo.NumeroExpediente,
                Juzgado = acuerdo.NombreJuzgado,
                Sintesis = acuerdo.Sintesis,
                FechaAcuerdo = acuerdo.FechaAcuerdo
            });

            if (dryRun) continue;

            acuerdo.Confianza = "Alta";
            acuerdo.Oculto = false;
            await context.SaveChangesAsync();

            await EnviarNotificacionAsync(emailService, expediente, acuerdo);
            acuerdo.NotificacionEnviada = true;
            await context.SaveChangesAsync();

            _logger.LogInformation(
                "Reevaluación: Exp {Numero} en {Juzgado} pasó de oculto a visible (Confianza Alta) y se notificó",
                acuerdo.NumeroExpediente, acuerdo.NombreJuzgado);
        }

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

    // ¿El expediente es de Jurisdicción Voluntaria? Comparación tolerante a mayúsculas
    // y acentos (reutiliza NormalizarTexto) porque el valor se captura a mano en el
    // despacho y no hay garantía de que siempre se escriba idéntico ("Jurisdicción
    // Voluntaria", "jurisdiccion voluntaria", etc.).
    internal static bool EsJurisdiccionVoluntaria(string? tipoJuicio)
    {
        if (string.IsNullOrWhiteSpace(tipoJuicio)) return false;
        return NormalizarTexto(tipoJuicio) == "JURISDICCION VOLUNTARIA";
    }

    // ¿El TipoAsunto que trae ADISON es una serie de numeración auxiliar (Exhorto o
    // Cuadernillo) en vez del expediente/causa principal? Esas series las numera el
    // juzgado que recibe el exhorto o abre el cuadernillo — no tienen relación con la
    // numeración del expediente original del despacho, así que coincidir en número no
    // basta para confiar (ver docs/mecanica-legal-sonora.md #2). Alcance deliberadamente
    // acotado a estas dos: ADISON usa otras abreviaturas (Toca, Leg., Amp./J.Amp.,
    // Pre., Req., "EXP. C.") cuyo significado y relación de numeración con el
    // expediente original no están confirmados, pero por ahora no se les da trato
    // especial — no se identificó un beneficio claro que lo justifique todavía (ver
    // docs/mecanica-legal-sonora.md #2 y docs/pendientes-reunion-despacho.md).
    internal static bool EsSerieAuxiliar(string? tipoAsunto)
    {
        if (string.IsNullOrWhiteSpace(tipoAsunto)) return false;
        var normalizado = tipoAsunto.Trim();
        return normalizado.Equals("Exh.", StringComparison.OrdinalIgnoreCase)
            || normalizado.Equals("Cuad.", StringComparison.OrdinalIgnoreCase);
    }

    // Confianza/Oculto para un acuerdo de Jurisdicción Voluntaria: no se sabe de
    // antemano si el texto de ADISON va a traer el nombre de la parte demandada (caso
    // normal) o solo el del banco promovente (caso exp. 434/2026, donde ADISON publicó
    // "SE RADICA DEMANDA.- BBVA MEXICO..." sin mencionar a la parte) — así que se
    // prueban los dos y basta con que cualquiera coincida para Alta. Si ninguno
    // coincide, Baja y se oculta igual que el resto del sistema: asumir válido
    // cualquier acuerdo sin nombre reconocible fue justo lo que dejó pasar la
    // notificación errónea del exp. 368/2026 (colisión de número con un caso de
    // concubinato ajeno en Cajeme, sin el nombre de la parte ni el del banco).
    internal static (string Confianza, bool Oculto) EvaluarJurisdiccionVoluntaria(
        string parteDemandada, string? nombreBanco, string partesScrapeadas, double umbralSimilitud = 0.8)
    {
        var coincide = PartesCoinciden(parteDemandada, partesScrapeadas, umbralSimilitud)
            || PartesCoinciden(nombreBanco ?? "", partesScrapeadas, umbralSimilitud);
        var confianza = coincide ? "Alta" : "Baja";
        return (confianza, confianza == "Baja");
    }

    // ¿El texto de "Partes" que trae ADISON parece traer al menos un nombre de
    // persona o empresa, o es solo texto genérico ("---", tipo de trámite sin
    // nombres, etc.)? Si no hay nombre, no hay nada que verificar contra
    // ParteDemandada — se sigue confiando en número+juzgado como antes.
    internal static bool PartesTieneNombre(string partes)
    {
        if (string.IsNullOrWhiteSpace(partes)) return false;

        // Los nombres suelen venir después del último separador ("-" o ".-") que
        // cierra la descripción del tipo de trámite — el texto antes de eso casi
        // siempre es terminología genérica ("ESPECIAL HIPOTECARIO", "ORAL
        // MERCANTIL", etc.) que por sí sola no cuenta como nombre real.
        var ultimoGuion = partes.LastIndexOf('-');
        var textoRelevante = ultimoGuion >= 0 ? partes[(ultimoGuion + 1)..] : partes;

        var palabras = System.Text.RegularExpressions.Regex.Matches(textoRelevante, @"[A-Za-zÁÉÍÓÚÑáéíóúñ]{4,}");
        return palabras.Count >= 2;
    }

    // Confianza del match foráneo (no se puede comparar el juzgado ahí, a
    // diferencia de Hermosillo): ¿el texto de "partes" que trae ADISON menciona
    // a la parte demandada del expediente? (comparación laxa: sin acentos ni
    // mayúsculas ni espacios de más, buscada como substring). No es infalible
    // (hay variaciones de captura, ej. "Vanesa" vs "Vanezza"), pero reduce el
    // ruido de coincidencias que son solo por número y no tienen relación real.
    // Solo clasifica (Alta/Baja) — no filtra nada todavía, ver Fase 1/Fase 2.
    internal static bool PartesCoinciden(string parteDemandada, string partesScrapeadas, double umbralSimilitud = 0.8)
    {
        if (string.IsNullOrWhiteSpace(parteDemandada) || string.IsNullOrWhiteSpace(partesScrapeadas))
            return false;

        var parteRelevante = QuitarSufijoOtroDemandado(parteDemandada);
        var textoNormalizado = NormalizarTexto(partesScrapeadas);
        var patronNormalizado = NormalizarTexto(parteRelevante);

        if (textoNormalizado.Contains(patronNormalizado))
            return true;

        // La coincidencia exacta por substring falló — antes de descartar el
        // match, probar tolerancia a variaciones de ortografía entre cómo el
        // despacho captura la parte y cómo la publica el juzgado (ej. "Corona"
        // vs "Coronado" — hallazgo real del 20 de agosto 2026, exp. 127/2023).
        return SimilitudMaximaSubcadena(patronNormalizado, textoNormalizado) >= umbralSimilitud;
    }

    // Busca, dentro de "texto", la subcadena (de CUALQUIER longitud, no solo la
    // de "patron") que más se le parezca — a diferencia de comparar ventanas de
    // longitud fija, esto sí maneja bien inserciones/omisiones en medio del
    // nombre (ej. "CORONA" vs "CORONADO": la ventana de longitud fija corta mal
    // el resto del nombre y da una similitud artificialmente baja). Es la técnica
    // estándar de "coincidencia aproximada de subcadena": una fila de Levenshtein
    // donde empezar en cualquier punto de "texto" no cuesta nada, y se toma el
    // mínimo de la última fila (terminar en cualquier punto tampoco cuesta).
    internal static double SimilitudMaximaSubcadena(string patron, string texto)
    {
        if (string.IsNullOrEmpty(patron) || string.IsNullOrEmpty(texto)) return 0.0;

        var m = patron.Length;
        var n = texto.Length;
        var anterior = new int[n + 1];
        var actual = new int[n + 1];

        for (var i = 1; i <= m; i++)
        {
            actual[0] = i;
            for (var j = 1; j <= n; j++)
            {
                var costo = patron[i - 1] == texto[j - 1] ? 0 : 1;
                actual[j] = Math.Min(Math.Min(anterior[j] + 1, actual[j - 1] + 1), anterior[j - 1] + costo);
            }
            (anterior, actual) = (actual, anterior);
        }

        var mejorDistancia = int.MaxValue;
        for (var j = 0; j <= n; j++)
            if (anterior[j] < mejorDistancia) mejorDistancia = anterior[j];

        return 1.0 - (double)mejorDistancia / m;
    }

    // % de similitud (0.0 a 1.0) entre dos textos de longitud comparable, basado
    // en distancia de Levenshtein normalizada por la longitud del más largo.
    internal static double Similitud(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1.0;
        var maxLen = Math.Max(a.Length, b.Length);
        if (maxLen == 0) return 1.0;
        return 1.0 - (double)DistanciaLevenshtein(a, b) / maxLen;
    }

    private static int DistanciaLevenshtein(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var costo = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + costo);
            }
        }

        return dp[a.Length, b.Length];
    }

    // "Y OTRA"/"Y OTRO"/"Y OTROS"/"Y OTRAS" es un sufijo genérico que el despacho
    // usa al capturar ParteDemandada cuando hay codemandados sin nombrar — pero
    // ADISON sí los nombra explícitamente, así que ese sufijo nunca aparece tal
    // cual en el texto de "partes" aunque sea el mismo caso. Comparamos solo lo
    // que viene antes del sufijo (validado con datos reales de julio 2026: 1 de
    // 273 matches foráneos pasó de Baja a Alta con este ajuste, sin generar
    // nuevos falsos positivos en el resto).
    // Quita "de lo"/"de la" como relleno gramatical entre dos palabras del nombre
    // de un juzgado (ej. "oral de lo mercantil" -> "oral mercantil"). Usa límites
    // de palabra (\b) para no comerse texto por accidente si "de lo"/"de la"
    // aparecieran pegados a otra palabra.
    private static string QuitarRellenoDeLo(string juzgado)
    {
        var sinRelleno = System.Text.RegularExpressions.Regex.Replace(
            juzgado, @"\bde\s+l[oa]\b", " ");
        return System.Text.RegularExpressions.Regex.Replace(sinRelleno, @"\s+", " ").Trim();
    }

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

    internal static bool JuzgadoCoincide(string juzgadoExpediente, string nombreJuzgadoScrapeado)
    {
        if (string.IsNullOrWhiteSpace(juzgadoExpediente)) return false;

        // "de lo"/"de la" es relleno gramatical que a veces se captura y a veces no
        // (ej. "Segundo Oral Mercantil" vs "Segundo Oral DE LO Mercantil" — mismo
        // juzgado). Se quita antes de comparar para que ningún patrón de abajo
        // tenga que anticipar cada variante de redacción posible — caso real que
        // lo destapó: 28 expedientes activos de Mario con "SEGUNDO ORAL DE LO
        // MERCANTIL" nunca hicieron match con ningún acuerdo de ese juzgado,
        // 1 de septiembre de 2026.
        var exp = QuitarRellenoDeLo(juzgadoExpediente.Trim().ToLowerInvariant());
        var scr = QuitarRellenoDeLo(nombreJuzgadoScrapeado.Trim().ToLowerInvariant());

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

        // Penal, Adolescentes, Ejecución de Sanciones y Laboral: mismo criterio que
        // arriba — la materia de un expediente es fija, así que estos patrones nunca
        // deberían cruzarse con Civil/Mercantil/Familiar ni entre ellos mismos.
        if (exp.Contains("oral penal") || exp.Contains("penal oral"))
            return scr.Contains("oral penal");
        if (exp.Contains("1ro penal") || exp.Contains("primero penal"))
            return scr.Contains("1ro penal") && !scr.Contains("oral");
        if (exp.Contains("2do penal") || exp.Contains("segundo penal"))
            return scr.Contains("2do penal") && !scr.Contains("oral");
        if (exp.Contains("3ro penal") || exp.Contains("tercero penal"))
            return scr.Contains("3ro penal") && !scr.Contains("oral");
        if (exp.Contains("5to penal") || exp.Contains("quinto penal"))
            return scr.Contains("5to penal") && !scr.Contains("oral");
        if (exp.Contains("tribunal unitario") || exp.Contains("regional adolescentes"))
            return scr.Contains("tribunal unitario regional");
        if (exp.Contains("adolescentes"))
            return scr.Contains("adolescentes") && !scr.Contains("tribunal unitario");
        if (exp.Contains("ejecución de sanciones") || exp.Contains("ejecucion de sanciones"))
            return scr.Contains("ejecución de sanciones") || scr.Contains("ejecucion de sanciones");
        if (exp.Contains("1er tribunal laboral") || exp.Contains("primer tribunal laboral"))
            return scr.Contains("1er tribunal laboral");
        if (exp.Contains("2do tribunal laboral") || exp.Contains("segundo tribunal laboral"))
            return scr.Contains("2do tribunal laboral");
        if (exp.Contains("3er tribunal laboral") || exp.Contains("tercer tribunal laboral"))
            return scr.Contains("3er tribunal laboral");

        return false;
    }
}