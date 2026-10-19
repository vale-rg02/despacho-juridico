using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Services;

// DJ-140: reporte diario de auditoría del scraper -- el proceso propio e
// independiente de los litigantes para detectar falsos negativos/positivos
// cada día durante la ventana de observación. Es SOLO un aviso: nunca oculta,
// descarta, confirma ni promueve un acuerdo, y no cambia el matching -- mismo
// criterio que DJ-139 y DJ-133. Mismo patrón de servicio periódico que
// FrasesGenericasCandidatasService: BackgroundService + PeriodicTimer +
// try/catch por ciclo + un método público reutilizado por el ciclo real y por
// la vista previa del controller.
public class ReporteAuditoriaScraperService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReporteAuditoriaScraperService> _logger;
    private readonly IConfiguration _config;
    private readonly FrasesGenericasCandidatasService _frasesGenericas;

    public ReporteAuditoriaScraperService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReporteAuditoriaScraperService> logger,
        IConfiguration config,
        FrasesGenericasCandidatasService frasesGenericas)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config;
        _frasesGenericas = frasesGenericas;
    }

    // Programado a una hora fija diaria (default 18:30, 25 min después de que
    // cierra la ventana del scraper a las 18:05 Hermosillo -- ver
    // ScraperAcuerdosService) en vez de un intervalo libre, a diferencia de
    // FrasesGenericasCandidatasService -- este correo sirve de latido, así que
    // debe llegar siempre a la misma hora, no "cada 24 horas desde que arrancó
    // el proceso".
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var horaEnvio = TimeOnly.TryParse(_config["Auditoria:HoraEnvioLocal"], out var h) ? h : new TimeOnly(18, 30);

        while (!stoppingToken.IsCancellationRequested)
        {
            var ahoraLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaHoraria);
            var proximoEnvioLocal = ProximoEnvioProgramado(ahoraLocal, horaEnvio);
            var proximoEnvioUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(proximoEnvioLocal, DateTimeKind.Unspecified), zonaHoraria);
            var espera = proximoEnvioUtc - DateTime.UtcNow;

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
                if (!_config.GetValue<bool>("Auditoria:Activo", false))
                {
                    _logger.LogInformation("Reporte de auditoría: Auditoria:Activo=false, no se genera ni envía nada hoy");
                }
                else
                {
                    var fechaHoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaHoraria));
                    var reporte = await GenerarReporteAsync(fechaHoy, avanzarMarcaDeAgua: true, stoppingToken);
                    await EnviarReporteAsync(reporte);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el ciclo del reporte diario de auditoría -- no afecta al scraper ni a otros servicios");
            }
        }
    }

    internal static DateTime ProximoEnvioProgramado(DateTime ahoraLocal, TimeOnly horaEnvio)
    {
        var candidato = ahoraLocal.Date + horaEnvio.ToTimeSpan();
        return candidato > ahoraLocal ? candidato : candidato.AddDays(1);
    }

    // Núcleo del reporte -- usado tanto por el ciclo diario como por la vista
    // previa (ScraperController). Crea su propio scope (patrón idéntico a
    // FrasesGenericasCandidatasService.GenerarReporteAsync). avanzarMarcaDeAgua
    // debe ser false en toda vista previa -- nunca oculta lo ya mostrado al
    // siguiente envío real.
    public async Task<ReporteAuditoriaScraperResponse> GenerarReporteAsync(DateOnly fecha, bool avanzarMarcaDeAgua, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var estado = await context.EstadosScraper.FindAsync(new object[] { 1 }, ct);
        var marcaDeAgua = estado?.UltimoReporteAuditoriaEnviadoEn ?? DateTime.MinValue;

        var umbralSimilitudReal = _config.GetValue<double>("ScraperAcuerdos:UmbralSimilitudPartes", 0.8);
        var bandaMin = _config.GetValue<double>("Auditoria:BandaSimilitudMinima", 0.65);
        var bandaMax = _config.GetValue<double>("Auditoria:BandaSimilitudMaxima", umbralSimilitudReal);
        var umbralColisionJuzgado = _config.GetValue<double>("Auditoria:UmbralSimilitudJuzgadoColision", 0.5);
        var diasUmbralCero = _config.GetValue<int>("Auditoria:DiasUnidadEnCero", 3);
        var umbralCaidaBrusca = _config.GetValue<double>("Auditoria:UmbralCaidaBrusca", 0.5);

        var reporte = new ReporteAuditoriaScraperResponse { Fecha = fecha };

        // ── Elemento 0: resumen del embudo del día, solo Origen=Automatica ──
        var filasDelDia = await context.BitacoraEvaluacionScraper
            .Where(b => b.FechaConsultada == fecha)
            .ToListAsync(ct);

        var filasAutomaticas = filasDelDia.Where(f => f.Origen == "Automatica").ToList();
        reporte.ResumenEmbudoAutomatico = AEmbudoTotal(filasAutomaticas);
        reporte.CorridasNoAutomaticasDelDia = filasDelDia
            .Where(f => f.Origen != "Automatica")
            .Select(f => f.CorridaId)
            .Distinct()
            .Count();

        // ── Elemento 1 y 2: ocultos Baja, Hermosillo exacto vs. foráneo parecido ──
        var ocultosBaja = await context.AcuerdosScrapeados
            .Include(a => a.Expediente)
            .Where(a => a.Oculto && a.Confianza == "Baja" && a.FechaDetectado > marcaDeAgua)
            .ToListAsync(ct);

        foreach (var acuerdo in ocultosBaja)
        {
            if (acuerdo.Expediente == null) continue;

            if (ScraperAcuerdosService.JuzgadosHermosillo.Contains(acuerdo.IdUnidad))
            {
                if (ScraperAcuerdosService.JuzgadoCoincide(acuerdo.Expediente.Juzgado ?? "", acuerdo.NombreJuzgado))
                {
                    reporte.Cola1OcultosHermosillo.Add(new AcuerdoOcultoCandidato
                    {
                        NumeroExpediente = acuerdo.NumeroExpediente,
                        NombreJuzgado = acuerdo.NombreJuzgado,
                        FechaDetectado = acuerdo.FechaDetectado
                    });
                }
                else
                {
                    // Ruido conocido (DJ-98): vínculo de un JuzgadoCoincide ya
                    // superado -- no es el caso que esta cola busca.
                    reporte.Cola1ExcluidosPorRuidoConocido++;
                }
            }
            else
            {
                var puntaje = ScraperAcuerdosService.PuntajeMaximoSimilitud(acuerdo.Expediente.ParteDemandada, acuerdo.Partes);
                if (puntaje >= bandaMin && puntaje < bandaMax)
                {
                    reporte.Cola2ForaneosParecidos.Add(new AcuerdoOcultoCandidato
                    {
                        NumeroExpediente = acuerdo.NumeroExpediente,
                        NombreJuzgado = acuerdo.NombreJuzgado,
                        FechaDetectado = acuerdo.FechaDetectado,
                        PuntajeSimilitud = puntaje
                    });
                }
            }
        }
        reporte.Cola2ForaneosParecidos = reporte.Cola2ForaneosParecidos.OrderByDescending(c => c.PuntajeSimilitud).ToList();

        // ── Elemento 2b: NUMERO_COINCIDE_JUZGADO_NO ──
        var expedientesActivos = await context.Expedientes
            .Where(e => e.Estado != Models.Enums.EstadoExpediente.Cerrado)
            .ToListAsync(ct);

        var colisiones = await context.BitacoraDescarteItemizado
            .Where(d => d.Razon == "NUMERO_COINCIDE_JUZGADO_NO" && d.FechaHoraCorrida > marcaDeAgua)
            .ToListAsync(ct);

        foreach (var item in colisiones)
        {
            var coincidentes = expedientesActivos.Where(e =>
                ScraperAcuerdosService.NormalizarNumero(e.NumeroExpediente) == ScraperAcuerdosService.NormalizarNumero(item.NumeroExpediente));

            var puntajeMax = coincidentes
                .Select(e => ScraperAcuerdosService.Similitud(e.Juzgado ?? "", item.NombreJuzgado))
                .DefaultIfEmpty(0.0)
                .Max();

            if (puntajeMax >= umbralColisionJuzgado)
            {
                var expedienteCoincidente = coincidentes.FirstOrDefault();
                reporte.Cola2bColisionesConIndicio.Add(new ColisionJuzgadoCandidato
                {
                    Id = item.Id,
                    NumeroExpediente = item.NumeroExpediente,
                    NombreJuzgadoAdison = item.NombreJuzgado,
                    NombreJuzgadoExpediente = expedienteCoincidente?.Juzgado ?? "",
                    PuntajeSimilitudJuzgado = puntajeMax
                });
            }
            else
            {
                reporte.Cola2bColisionesSinIndicio++;
            }
        }
        reporte.Cola2bColisionesConIndicio = reporte.Cola2bColisionesConIndicio
            .OrderByDescending(c => c.PuntajeSimilitudJuzgado).ToList();

        // ── Elemento 3: frases candidatas (DJ-133), sin duplicar lógica ──
        reporte.FrasesCandidatas = await _frasesGenericas.GenerarReporteAsync(null, ct);

        // ── Elemento 4: anomalías ──
        reporte.NotificacionesFallidas = await context.BitacoraDescarteItemizado
            .Where(d => d.Razon == "EXCEPCION" && d.EtapaFalla == "notificacion" && d.FechaHoraCorrida > marcaDeAgua)
            .OrderBy(d => d.FechaHoraCorrida)
            .Select(d => new DescarteItemizadoResponse
            {
                NumeroExpediente = d.NumeroExpediente,
                NombreJuzgado = d.NombreJuzgado,
                Razon = d.Razon,
                EtapaFalla = d.EtapaFalla
            })
            .ToListAsync(ct);

        reporte.ConsultasFallidas = filasDelDia
            .Where(f => !f.ConsultaExitosa)
            .Select(AEmbudoResponseLocal)
            .ToList();

        var historial = await context.BitacoraEvaluacionScraper
            .Where(b => b.Origen == "Automatica" && b.FechaConsultada > fecha.AddDays(-9) && b.FechaConsultada <= fecha)
            .ToListAsync(ct);

        reporte.UnidadesEnCeroVariosDias = DetectarUnidadesEnCero(historial, fecha, diasUmbralCero);
        reporte.CaidasBruscas = DetectarCaidasBruscas(historial, fecha, umbralCaidaBrusca);

        // ── Elemento 5: ventana de observación ──
        var inicioVentanaCruda = _config["Auditoria:InicioVentana"];
        if (DateOnly.TryParse(inicioVentanaCruda, out var inicioVentana) && fecha >= inicioVentana)
        {
            reporte.VentanaIniciada = true;

            var diasNoHabiles = (_config["Auditoria:DiasNoHabiles"] ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => DateOnly.TryParse(s, out var d) ? d : (DateOnly?)null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToHashSet();

            var hallazgos = await context.HallazgosAuditoria
                .Where(h => h.Fecha >= inicioVentana && h.Fecha <= fecha)
                .OrderBy(h => h.Fecha)
                .Include(h => h.RegistradoPor)
                .ToListAsync(ct);

            var (s1, s2, s3) = CalcularDiasHabilesSinSeveridad(
                hallazgos.Select(h => (h.Fecha, h.Severidad)).ToList(), fecha, inicioVentana, diasNoHabiles);
            reporte.DiasHabilesSinS1 = s1;
            reporte.DiasHabilesSinS2 = s2;
            reporte.DiasHabilesSinS3 = s3;

            reporte.AcumuladoVisiblesDesdeInicioVentana = await context.BitacoraEvaluacionScraper
                .Where(b => b.Origen == "Automatica" && b.FechaConsultada >= inicioVentana && b.FechaConsultada <= fecha)
                .SumAsync(b => b.GuardadoVisibleAlta + b.GuardadoVisibleMedia, ct);

            reporte.TablaHallazgos = hallazgos.Select(h => new HallazgoAuditoriaResponse
            {
                Id = h.Id,
                Fecha = h.Fecha,
                Severidad = h.Severidad,
                Categoria = h.Categoria,
                Descripcion = h.Descripcion,
                NumeroExpediente = h.NumeroExpediente,
                NombreJuzgado = h.NombreJuzgado,
                RegistradoPorNombre = h.RegistradoPor.Nombre,
                CreadoEn = h.CreadoEn
            }).ToList();
        }

        if (avanzarMarcaDeAgua)
        {
            if (estado == null)
            {
                estado = new EstadoScraper { Id = 1 };
                context.EstadosScraper.Add(estado);
            }
            estado.UltimoReporteAuditoriaEnviadoEn = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
        }

        return reporte;
    }

    // DJ-140: cuántos días hábiles consecutivos (hasta "hoy" inclusive) sin un
    // hallazgo de cada severidad. Un S1 reinicia los TRES contadores (acordado
    // explícitamente con el usuario); S2 y S3 solo reinician el suyo propio.
    // Pura, sin EF -- opera sobre hallazgos ya cargados.
    internal static (int DiasSinS1, int DiasSinS2, int DiasSinS3) CalcularDiasHabilesSinSeveridad(
        List<(DateOnly Fecha, string Severidad)> hallazgos, DateOnly hoy, DateOnly inicioVentana, HashSet<DateOnly> diasNoHabiles)
    {
        var diasSinS1 = 0;
        var diasSinS2 = 0;
        var diasSinS3 = 0;

        var dia = inicioVentana;
        while (dia <= hoy)
        {
            if (EsDiaHabil(dia, diasNoHabiles))
            {
                var huboS1 = hallazgos.Any(h => h.Fecha == dia && h.Severidad == "S1");
                var huboS2 = hallazgos.Any(h => h.Fecha == dia && h.Severidad == "S2");
                var huboS3 = hallazgos.Any(h => h.Fecha == dia && h.Severidad == "S3");

                diasSinS1 = huboS1 ? 0 : diasSinS1 + 1;
                diasSinS2 = (huboS1 || huboS2) ? 0 : diasSinS2 + 1;
                diasSinS3 = (huboS1 || huboS3) ? 0 : diasSinS3 + 1;
            }
            dia = dia.AddDays(1);
        }

        return (diasSinS1, diasSinS2, diasSinS3);
    }

    // DJ-140: guardarraíl de entrada real para POST /api/scraper/hallazgos --
    // "1-2 palabras" de la regla original se aplica con margen (hasta 6) para
    // no estorbar descripciones legítimas un poco más largas, pero sigue
    // rechazando algo del tamaño de una oración/nombre completo. No es una
    // detección semántica de nombres -- es el mismo tipo de guardarraíl
    // estructural que ya usa DJ-139 (ver NingunCampoDeLosModelos_MencionaPartesONombre).
    internal static string? ValidarHallazgo(HallazgoAuditoriaRequest req)
    {
        if (req.Severidad != "S1" && req.Severidad != "S2" && req.Severidad != "S3")
            return "Severidad debe ser S1, S2 o S3.";
        if (string.IsNullOrWhiteSpace(req.Categoria) || req.Categoria.Length > 40)
            return "Categoria es requerida y debe tener 40 caracteres o menos.";
        if (string.IsNullOrWhiteSpace(req.Descripcion))
            return "Descripcion es requerida.";
        if (req.Descripcion.Length > 80)
            return "Descripcion debe tener 80 caracteres o menos.";
        if (req.Descripcion.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 6)
            return "Descripcion debe ser corta (1-2 palabras) -- no texto de Partes ni nombres.";
        return null;
    }

    internal static bool EsDiaHabil(DateOnly fecha, HashSet<DateOnly> diasNoHabiles) =>
        fecha.DayOfWeek != DayOfWeek.Saturday && fecha.DayOfWeek != DayOfWeek.Sunday && !diasNoHabiles.Contains(fecha);

    // DJ-140: una unidad que hoy dio cero y lleva >= diasUmbral días
    // consecutivos (incluyendo hoy) en cero, dentro de las corridas Automatica
    // ya cargadas en "historial" -- no decide nada, solo lo señala para
    // revisión humana.
    internal static List<AnomaliaUnidadEnCero> DetectarUnidadesEnCero(
        List<BitacoraEvaluacionScraper> historial, DateOnly hoy, int diasUmbral)
    {
        var resultado = new List<AnomaliaUnidadEnCero>();

        foreach (var grupo in historial.GroupBy(f => f.IdUnidad))
        {
            var porFecha = grupo
                .GroupBy(f => f.FechaConsultada)
                .Select(g => new { Fecha = g.Key, Devueltos = g.Sum(f => f.Devueltos) })
                .OrderByDescending(g => g.Fecha)
                .ToList();

            if (porFecha.Count == 0 || porFecha[0].Fecha != hoy || porFecha[0].Devueltos != 0) continue;

            var consecutivos = 0;
            var fechaEsperada = hoy;
            foreach (var dia in porFecha)
            {
                if (dia.Fecha != fechaEsperada || dia.Devueltos != 0) break;
                consecutivos++;
                fechaEsperada = fechaEsperada.AddDays(-1);
            }

            if (consecutivos >= diasUmbral)
            {
                resultado.Add(new AnomaliaUnidadEnCero
                {
                    IdUnidad = grupo.Key,
                    NombreJuzgado = grupo.First().NombreJuzgado,
                    DiasConsecutivosEnCero = consecutivos
                });
            }
        }

        return resultado.OrderByDescending(a => a.DiasConsecutivosEnCero).ToList();
    }

    // DJ-140: hoy cayó por debajo de umbralProporcion de la mediana de los
    // últimos días disponibles en "historial" (hasta 7, sin contar hoy) -- solo
    // se evalúa si hay al menos 3 días previos y la mediana es > 0 (si la
    // mediana ya era 0, no hay "caída", es una unidad de por sí vacía).
    internal static List<AnomaliaCaidaBrusca> DetectarCaidasBruscas(
        List<BitacoraEvaluacionScraper> historial, DateOnly hoy, double umbralProporcion)
    {
        var resultado = new List<AnomaliaCaidaBrusca>();

        foreach (var grupo in historial.GroupBy(f => f.IdUnidad))
        {
            var porFecha = grupo
                .GroupBy(f => f.FechaConsultada)
                .ToDictionary(g => g.Key, g => g.Sum(f => f.Devueltos));

            if (!porFecha.TryGetValue(hoy, out var devueltosHoy)) continue;

            var previos = porFecha
                .Where(kv => kv.Key < hoy && kv.Key >= hoy.AddDays(-7))
                .Select(kv => (double)kv.Value)
                .OrderBy(v => v)
                .ToList();

            if (previos.Count < 3) continue;

            var mediana = previos.Count % 2 == 1
                ? previos[previos.Count / 2]
                : (previos[previos.Count / 2 - 1] + previos[previos.Count / 2]) / 2.0;

            if (mediana <= 0) continue;

            if (devueltosHoy < mediana * umbralProporcion)
            {
                resultado.Add(new AnomaliaCaidaBrusca
                {
                    IdUnidad = grupo.Key,
                    NombreJuzgado = grupo.First().NombreJuzgado,
                    DevueltosHoy = devueltosHoy,
                    MedianaUltimos7Dias = mediana
                });
            }
        }

        return resultado.OrderBy(a => a.DevueltosHoy - a.MedianaUltimos7Dias).ToList();
    }

    internal async Task EnviarReporteAsync(ReporteAuditoriaScraperResponse reporte)
    {
        var destinatarios = (_config["Auditoria:Destinatarios"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (destinatarios.Count == 0)
        {
            _logger.LogWarning("Reporte de auditoría listo para {Fecha} pero no hay destinatarios configurados (Auditoria:Destinatarios) -- no se envía.", reporte.Fecha);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var asunto = $"Reporte diario de auditoría del scraper -- {reporte.Fecha:dd/MM/yyyy}";
        var cuerpoHtml = ConstruirCuerpoCorreo(reporte);

        foreach (var destinatario in destinatarios)
            await emailService.EnviarAsync(destinatario, "Equipo técnico", asunto, cuerpoHtml);

        _logger.LogInformation("Reporte de auditoría enviado a {Cantidad} destinatarios ({Fecha})", destinatarios.Count, reporte.Fecha);
    }

    // Suma las categorías de una lista de filas del embudo (mismo patrón que
    // ObtenerResumenDiaAsync.Total en ScraperAcuerdosService).
    private static EmbudoJuzgadoResponse AEmbudoTotal(List<BitacoraEvaluacionScraper> filas) => new()
    {
        Devueltos = filas.Sum(f => f.Devueltos),
        Evaluaciones = filas.Sum(f => f.Evaluaciones),
        GuardadoVisibleAlta = filas.Sum(f => f.GuardadoVisibleAlta),
        GuardadoVisibleMedia = filas.Sum(f => f.GuardadoVisibleMedia),
        GuardadoOcultoBaja = filas.Sum(f => f.GuardadoOcultoBaja),
        YaExistia = filas.Sum(f => f.YaExistia),
        DescartadoSinCandidato = filas.Sum(f => f.DescartadoSinCandidato),
        DescartadoNumeroJuzgadoNoCoincide = filas.Sum(f => f.DescartadoNumeroJuzgadoNoCoincide),
        DescartadoExpedienteCerrado = filas.Sum(f => f.DescartadoExpedienteCerrado),
        DescartadoFueraDeAlcanceFiltro = filas.Sum(f => f.DescartadoFueraDeAlcanceFiltro),
        DescartadoExcepcion = filas.Sum(f => f.DescartadoExcepcion),
        ConsultaExitosa = filas.All(f => f.ConsultaExitosa)
    };

    private static EmbudoJuzgadoResponse AEmbudoResponseLocal(BitacoraEvaluacionScraper b) => new()
    {
        IdUnidad = b.IdUnidad,
        NombreJuzgado = b.NombreJuzgado,
        Origen = b.Origen,
        DryRun = b.DryRun,
        ConsultaExitosa = b.ConsultaExitosa,
        MotivoFallo = b.MotivoFallo,
        Devueltos = b.Devueltos,
        Evaluaciones = b.Evaluaciones,
        GuardadoVisibleAlta = b.GuardadoVisibleAlta,
        GuardadoVisibleMedia = b.GuardadoVisibleMedia,
        GuardadoOcultoBaja = b.GuardadoOcultoBaja,
        YaExistia = b.YaExistia,
        DescartadoSinCandidato = b.DescartadoSinCandidato,
        DescartadoNumeroJuzgadoNoCoincide = b.DescartadoNumeroJuzgadoNoCoincide,
        DescartadoExpedienteCerrado = b.DescartadoExpedienteCerrado,
        DescartadoFueraDeAlcanceFiltro = b.DescartadoFueraDeAlcanceFiltro,
        DescartadoExcepcion = b.DescartadoExcepcion
    };

    // Mismo esqueleto visual navy+dorado que el resto de correos del sistema.
    // Nunca incluye Partes ni nombres -- solo número de expediente, juzgado,
    // conteos y puntajes.
    private static string ConstruirCuerpoCorreo(ReporteAuditoriaScraperResponse r)
    {
        string Fila(string etiqueta, object? valor) => $"<p><strong>{etiqueta}:</strong> {valor}</p>";

        var resumen = $@"
    <div class='highlight'>
      {Fila("Devueltos", r.ResumenEmbudoAutomatico.Devueltos)}
      {Fila("Guardado Alta", r.ResumenEmbudoAutomatico.GuardadoVisibleAlta)}
      {Fila("Guardado Media", r.ResumenEmbudoAutomatico.GuardadoVisibleMedia)}
      {Fila("Guardado Oculto (Baja)", r.ResumenEmbudoAutomatico.GuardadoOcultoBaja)}
      {Fila("Ya existía", r.ResumenEmbudoAutomatico.YaExistia)}
      {Fila("Corridas no automáticas hoy", r.CorridasNoAutomaticasDelDia)}
    </div>";

        string ListaCola1() => r.Cola1OcultosHermosillo.Count == 0
            ? "<p>Sin candidatos hoy.</p>"
            : string.Join("\n", r.Cola1OcultosHermosillo.Select(c =>
                $"<p>{System.Net.WebUtility.HtmlEncode(c.NumeroExpediente)} — {System.Net.WebUtility.HtmlEncode(c.NombreJuzgado)}</p>"));

        string ListaCola2() => r.Cola2ForaneosParecidos.Count == 0
            ? "<p>Sin candidatos hoy.</p>"
            : string.Join("\n", r.Cola2ForaneosParecidos.Select(c =>
                $"<p>{System.Net.WebUtility.HtmlEncode(c.NumeroExpediente)} — {System.Net.WebUtility.HtmlEncode(c.NombreJuzgado)} (puntaje {c.PuntajeSimilitud:F2})</p>"));

        string ListaCola2b() => r.Cola2bColisionesConIndicio.Count == 0
            ? $"<p>Sin indicios hoy ({r.Cola2bColisionesSinIndicio} colisiones de número sin indicio de bug).</p>"
            : string.Join("\n", r.Cola2bColisionesConIndicio.Select(c =>
                $"<p>{System.Net.WebUtility.HtmlEncode(c.NumeroExpediente)}: \"{System.Net.WebUtility.HtmlEncode(c.NombreJuzgadoExpediente)}\" vs \"{System.Net.WebUtility.HtmlEncode(c.NombreJuzgadoAdison)}\" (puntaje {c.PuntajeSimilitudJuzgado:F2})</p>"))
              + $"<p>({r.Cola2bColisionesSinIndicio} colisiones adicionales sin indicio, no itemizadas)</p>";

        string ListaFrases() => r.FrasesCandidatas.Candidatos.Count == 0
            ? "<p>Sin frases candidatas nuevas.</p>"
            : string.Join("\n", r.FrasesCandidatas.Candidatos.Select(c =>
                $"<p>\"{System.Net.WebUtility.HtmlEncode(c.Frase)}\" ({c.ExpedientesDistintos} expedientes distintos)</p>"));

        // DJ-134: una palabra suelta nunca pasó por el mismo filtro que un
        // candidato de 2+ palabras (PartesTieneNombre no la reconoce como
        // nombre, sea jerga o apellido real) -- se muestra aparte y marcada
        // como revisión manual, nunca junto a ListaFrases().
        string ListaPalabraUnica() => r.FrasesCandidatas.CandidatosPalabraUnica.Count == 0
            ? "<p>Sin candidatos de una palabra hoy.</p>"
            : string.Join("\n", r.FrasesCandidatas.CandidatosPalabraUnica.Select(c =>
                $"<p>\"{System.Net.WebUtility.HtmlEncode(c.Frase)}\" ({c.ExpedientesDistintos} expedientes distintos)</p>"));

        string ListaAnomalias()
        {
            var partes = new List<string>();
            if (r.NotificacionesFallidas.Count > 0)
                partes.Add($"<p><strong>Notificaciones fallidas:</strong> {r.NotificacionesFallidas.Count}</p>");
            if (r.ConsultasFallidas.Count > 0)
                partes.Add($"<p><strong>Consultas fallidas:</strong> {r.ConsultasFallidas.Count}</p>");
            if (r.UnidadesEnCeroVariosDias.Count > 0)
                partes.Add($"<p><strong>Unidades en cero varios días:</strong> {r.UnidadesEnCeroVariosDias.Count}</p>");
            if (r.CaidasBruscas.Count > 0)
                partes.Add($"<p><strong>Caídas bruscas:</strong> {r.CaidasBruscas.Count}</p>");
            return partes.Count == 0 ? "<p>Sin anomalías hoy.</p>" : string.Join("\n", partes);
        }

        var ventana = r.VentanaIniciada
            ? $@"<div class='highlight'>
      {Fila("Días hábiles sin S1", r.DiasHabilesSinS1)}
      {Fila("Días hábiles sin S2", r.DiasHabilesSinS2)}
      {Fila("Días hábiles sin S3", r.DiasHabilesSinS3)}
      {Fila("Acumulado visibles desde inicio de ventana", r.AcumuladoVisiblesDesdeInicioVentana)}
    </div>"
            : "<p>Ventana de observación no iniciada.</p>";

        return $@"
<!DOCTYPE html><html><head><meta charset='UTF-8'>
<style>
  body{{font-family:Georgia,'Times New Roman',serif;background:#f7f5f0;margin:0;padding:0;}}
  .container{{max-width:640px;margin:40px auto;background:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);}}
  .header{{background:#1c2b4a;padding:32px 40px;text-align:center;}}
  .header h1{{color:#ffffff;font-size:20px;margin:0;font-weight:normal;letter-spacing:1px;}}
  .header p{{color:#9a7c3c;font-size:13px;margin:6px 0 0;letter-spacing:2px;text-transform:uppercase;}}
  .body{{padding:40px;color:#333333;}}
  .body h2{{font-size:16px;color:#1c2b4a;margin:28px 0 8px;}}
  .body p{{font-size:14px;line-height:1.6;margin:0 0 8px;}}
  .highlight{{background:#f0f4fa;border-left:4px solid #9a7c3c;padding:16px 20px;margin:8px 0;border-radius:0 6px 6px 0;}}
  .footer{{background:#f7f5f0;padding:20px 40px;text-align:center;border-top:1px solid #e0ddd6;}}
  .footer p{{font-size:12px;color:#888;margin:0;}}
</style></head>
<body><div class='container'>
  <div class='header'>
    <h1>Despacho Jurídico Acedo e Hijos</h1>
    <p>Reporte diario de auditoría del scraper</p>
  </div>
  <div class='body'>
    <p>Equipo técnico, este es el reporte del {r.Fecha:dd/MM/yyyy}. Solo observación -- ningún acuerdo cambió de visibilidad ni de confianza.</p>
    <h2>Resumen del embudo (ciclo automático)</h2>
    {resumen}
    <h2>Cola 1 — ocultos Hermosillo exactos{(r.Cola1ExcluidosPorRuidoConocido > 0 ? $" ({r.Cola1ExcluidosPorRuidoConocido} excluidos por ruido conocido)" : "")}</h2>
    {ListaCola1()}
    <h2>Cola 2 — foráneos ocultos, nombre parecido</h2>
    {ListaCola2()}
    <h2>Cola 2b — número coincide, juzgado no</h2>
    {ListaCola2b()}
    <h2>Frases candidatas nuevas</h2>
    {ListaFrases()}
    <h2>Candidatos de una palabra (revisar manualmente)</h2>
    <p>No pasan por el mismo filtro automático que las anteriores -- podrían ser jerga del trámite o un apellido real. No se agregue a FrasesGenericasConocidas sin confirmar.</p>
    {ListaPalabraUnica()}
    <h2>Anomalías</h2>
    {ListaAnomalias()}
    <h2>Ventana de observación</h2>
    {ventana}
    <p>Atentamente,<br><strong>Sistema de Gestión de Expedientes</strong></p>
  </div>
  <div class='footer'>
    <p>Este es un mensaje automático. Por favor no responda a este correo.</p>
  </div>
</div></body></html>";
    }
}
