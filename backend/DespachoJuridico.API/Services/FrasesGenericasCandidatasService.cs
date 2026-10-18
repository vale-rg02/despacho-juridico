using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Services;

// DJ-133 (implementa el diseño de la Parte 2 de DJ-131 -- ver
// docs/investigacion-frases-genericas.md, que es la referencia normativa de
// esta clase: su definición de "candidato", su consulta y su umbral ganan
// sobre cualquier otra interpretación).
//
// Aviso periódico -- NUNCA oculta ni promueve ningún acuerdo -- de candidatos a
// frase genérica nueva: el mismo patrón de bug que DJ-130/DJ-131 Parte 1 (texto
// descriptivo del trámite, sin nombre, que PartesTieneNombre todavía no sabe
// descartar porque no está en FrasesGenericasConocidas). Sigue el patrón
// periódico de RecordatorioAcuerdosPendientesService (PeriodicTimer + try/catch
// por ciclo), no el patrón "un correo por evento" del resto del scraper -- esto
// agrupa todos los candidatos nuevos en un solo correo.
public class FrasesGenericasCandidatasService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FrasesGenericasCandidatasService> _logger;
    private readonly IConfiguration _config;
    private readonly TimeSpan _intervalo;

    public FrasesGenericasCandidatasService(
        IServiceScopeFactory scopeFactory,
        ILogger<FrasesGenericasCandidatasService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config;

        // Semanal por defecto (10080 min) -- la investigación no encontró
        // evidencia de que haga falta más frecuencia que eso; configurable sin
        // redeploy vía FrasesGenericas__IntervaloMinutos, mismo estilo que
        // ScraperAcuerdos__* y RecordatorioAcuerdosPendientes__*.
        var minutos = config.GetValue<int?>("FrasesGenericas:IntervaloMinutos") ?? 10080;
        _intervalo = TimeSpan.FromMinutes(minutos);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_intervalo);

        do
        {
            try
            {
                await RevisarYEnviarSiHayCandidatosAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la revisión periódica de candidatos a frase genérica");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task RevisarYEnviarSiHayCandidatosAsync(CancellationToken ct)
    {
        // DJ-140: cuando el reporte diario de auditoría está activo, absorbe
        // este aviso semanal -- queda un solo correo, no dos. GenerarReporteAsync
        // y EnviarSiHayCandidatosAsync siguen sin cambios para quien los llame
        // directo (la vista previa de este controller, y el elemento 3 del
        // reporte de DJ-140).
        if (_config.GetValue<bool>("Auditoria:ApagarCorreoSemanalFrasesGenericas", false)) return;

        var reporte = await GenerarReporteAsync(null, ct);
        await EnviarSiHayCandidatosAsync(reporte);
    }

    // Envía el correo si hay candidatos y hay destinatarios configurados. Punto
    // de entrada compartido por el ciclo periódico y la vista previa del
    // controller cuando se pide dryRun=false -- misma lógica de envío sin
    // importar quién la dispare.
    public async Task EnviarSiHayCandidatosAsync(ReporteCandidatosResponse reporte)
    {
        if (reporte.Candidatos.Count == 0) return;

        var destinatarios = ObtenerDestinatarios(_config);
        if (destinatarios.Count == 0)
        {
            _logger.LogWarning(
                "Hay {Cantidad} candidatos a frase genérica nuevos, pero no hay destinatarios configurados (FrasesGenericas:Destinatarios) -- no se envía correo.",
                reporte.Candidatos.Count);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var asunto = reporte.Candidatos.Count == 1
            ? "1 frase candidata a genérica detectada"
            : $"{reporte.Candidatos.Count} frases candidatas a genéricas detectadas";
        var cuerpoHtml = ConstruirCuerpoCorreo(reporte);

        foreach (var destinatario in destinatarios)
            await emailService.EnviarAsync(destinatario, "Equipo técnico", asunto, cuerpoHtml);

        _logger.LogInformation(
            "Reporte de candidatos a frase genérica enviado a {CantidadDestinatarios} destinatarios ({CantidadCandidatos} candidatos)",
            destinatarios.Count, reporte.Candidatos.Count);
    }

    // Los destinatarios son el equipo técnico, nunca litigantes -- por diseño no
    // hay ninguna ruta en esta clase que derive un email de Usuario/Expediente;
    // la única fuente posible es esta variable de configuración. Si no está
    // configurada, no se envía nada (ver EnviarSiHayCandidatosAsync).
    internal static List<string> ObtenerDestinatarios(IConfiguration config)
    {
        var crudo = config["FrasesGenericas:Destinatarios"];
        if (string.IsNullOrWhiteSpace(crudo)) return new List<string>();

        return crudo.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    // Calcula el reporte -- usado tanto por el ciclo periódico como por la vista
    // previa (ScraperController). Crea su propio scope (patrón idéntico a
    // ReevaluarOcultosAsync) -- NUNCA escribe nada: solo SELECT, ni un solo
    // SaveChangesAsync en toda esta clase.
    public async Task<ReporteCandidatosResponse> GenerarReporteAsync(int? umbralOverride, CancellationToken ct)
    {
        var umbral = umbralOverride ?? _config.GetValue<int?>("FrasesGenericas:UmbralExpedientes") ?? 3;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var acuerdos = await context.AcuerdosScrapeados
            .Include(a => a.Expediente).ThenInclude(e => e.Banco)
            .Where(a => a.Oculto && a.Confianza == "Baja")
            .Where(a => ScraperAcuerdosService.JuzgadosHermosillo.Contains(a.IdUnidad))
            .ToListAsync(ct);

        var filas = new List<AcuerdoParaCandidato>();
        var excluidosPorJuzgadoNoValido = 0;

        foreach (var acuerdo in acuerdos)
        {
            var expediente = acuerdo.Expediente;
            if (expediente == null) continue;

            // Mismo alcance que el bug de PartesTieneNombre (DJ-131 Parte 1):
            // solo la rama Hermosillo directa, sin Jurisdicción Voluntaria ni
            // Exh./Cuad. -- las demás rutas nunca pasan por PartesTieneNombre,
            // así que no pueden producir este tipo de candidato.
            if (ScraperAcuerdosService.EsJurisdiccionVoluntaria(expediente.TipoJuicio)) continue;
            if (ScraperAcuerdosService.EsSerieAuxiliar(acuerdo.TipoAsunto)) continue;

            // Revalidado con el JuzgadoCoincide ACTUAL -- filas viejas vinculadas
            // antes de que existiera el patrón de Laboral/Penal (ver comentario
            // "102 registros históricos" en ScraperAcuerdosService.cs) ya no
            // pasarían este chequeo hoy y no son el bug que esta historia ataca
            // (confirmado en la verificación manual de DJ-131: 6 de 7 candidatos
            // encontrados así eran justo este ruido, no PartesTieneNombre).
            if (!ScraperAcuerdosService.JuzgadoCoincide(expediente.Juzgado ?? "", acuerdo.NombreJuzgado))
            {
                excluidosPorJuzgadoNoValido++;
                continue;
            }

            // Un conector (VS/A BIENES DE/...) es una señal fuerte de nombre real
            // -- lo que sigue no es "texto descriptivo sin nombre", es otra cosa
            // (posible falso negativo de PartesCoinciden, fuera de este alcance).
            if (ScraperAcuerdosService.SigueUnNombreDespuesDeUnConector(acuerdo.Partes)) continue;

            var segmento = ScraperAcuerdosService.ObtenerUltimoSegmentoConContenido(acuerdo.Partes);
            if (segmento == null) continue;

            var candidato = NormalizarCandidato(segmento);
            if (candidato.Length == 0) continue;

            filas.Add(new AcuerdoParaCandidato(
                acuerdo.NumeroExpediente, acuerdo.NombreJuzgado, candidato, acuerdo.FechaDetectado,
                expediente.BancoId, expediente.Banco?.Nombre));
        }

        var calculo = CalcularCandidatos(filas, ScraperAcuerdosService.FrasesGenericasConocidas, umbral);

        return new ReporteCandidatosResponse
        {
            Umbral = umbral,
            Candidatos = calculo.CandidatosNuevos,
            ExcluidosPorJuzgadoNoValido = excluidosPorJuzgadoNoValido,
            ExcluidosPorPalabraUnica = calculo.ExcluidosPorPalabraUnica,
            ExcluidosPorYaAprobada = calculo.ExcluidosPorYaAprobada
        };
    }

    // Misma normalización que ScraperAcuerdosService.NormalizarTexto (mayúsculas,
    // sin acentos, espacios colapsados) más recorte de puntuación final -- la
    // definición de "candidato" de la investigación (sección 2).
    internal static string NormalizarCandidato(string segmentoCrudo) =>
        ScraperAcuerdosService.NormalizarTexto(segmentoCrudo).Trim(' ', '.', ',', ';');

    internal static bool EsPalabraUnica(string candidatoNormalizado) =>
        candidatoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 1;

    internal record AcuerdoParaCandidato(
        string NumeroExpediente, string NombreJuzgado, string Candidato, DateTime FechaDetectado,
        int? BancoId, string? NombreBanco);

    internal record ResultadoCalculoCandidatos(
        List<CandidatoFraseGenerica> CandidatosNuevos, int ExcluidosPorPalabraUnica, int ExcluidosPorYaAprobada);

    // Núcleo de la lógica de decisión -- sin EF, sin IO, 100% unit-testable.
    // Cuenta expedientes distintos como el par (NumeroExpediente, NombreJuzgado)
    // -- NUNCA el número solo (sección 4.4 de la investigación: el mismo número
    // se repite incluso entre los dos juzgados Oral Mercantil de Hermosillo).
    internal static ResultadoCalculoCandidatos CalcularCandidatos(
        IReadOnlyList<AcuerdoParaCandidato> filas, IReadOnlyCollection<string> frasesYaAprobadas, int umbralExpedientes)
    {
        var yaAprobadas = frasesYaAprobadas.Select(NormalizarCandidato).ToHashSet();

        var candidatos = new List<CandidatoFraseGenerica>();
        var excluidosPorPalabraUnica = 0;
        var excluidosPorYaAprobada = 0;

        foreach (var grupo in filas.GroupBy(f => f.Candidato))
        {
            var candidato = grupo.Key;

            var expedientesDistintos = grupo
                .Select(f => (f.NumeroExpediente, f.NombreJuzgado))
                .Distinct()
                .ToList();

            // Por debajo del umbral no es candidato a nada -- no se cuenta en
            // ninguna de las exclusiones de abajo, es simplemente ruido normal.
            if (expedientesDistintos.Count < umbralExpedientes) continue;

            if (yaAprobadas.Contains(candidato))
            {
                excluidosPorYaAprobada++;
                continue;
            }

            if (EsPalabraUnica(candidato))
            {
                excluidosPorPalabraUnica++;
                continue;
            }

            var bancosDistintos = grupo
                .Where(f => f.BancoId.HasValue)
                .Select(f => f.BancoId!.Value)
                .Distinct()
                .ToList();

            var nombresBanco = grupo
                .Where(f => f.BancoId.HasValue && f.NombreBanco != null)
                .Select(f => f.NombreBanco!)
                .Distinct()
                .ToList();

            candidatos.Add(new CandidatoFraseGenerica
            {
                Frase = candidato,
                ExpedientesDistintos = expedientesDistintos.Count,
                Ejemplos = expedientesDistintos.Take(3)
                    .Select(e => $"{e.NumeroExpediente} ({e.NombreJuzgado})")
                    .ToList(),
                PrimeraDeteccion = grupo.Min(f => f.FechaDetectado),
                UltimaDeteccion = grupo.Max(f => f.FechaDetectado),
                BancosDistintos = bancosDistintos.Count,
                PosibleNombreDeBanco = bancosDistintos.Count == 1,
                NombresBanco = nombresBanco
            });
        }

        return new ResultadoCalculoCandidatos(
            candidatos.OrderByDescending(c => c.ExpedientesDistintos).ToList(),
            excluidosPorPalabraUnica,
            excluidosPorYaAprobada);
    }

    // Mismo esqueleto visual que el resto de correos del sistema (navy +
    // dorado #9a7c3c, ver EnviarNotificacionAsync en ScraperAcuerdosService) --
    // una tarjeta ('highlight') por candidato. Nunca incluye nombres de partes:
    // solo la frase, conteos, número+juzgado de ejemplo y fechas.
    private static string ConstruirCuerpoCorreo(ReporteCandidatosResponse reporte)
    {
        var tarjetas = string.Join("\n", reporte.Candidatos.Select(c =>
        {
            var fraseEnc = System.Net.WebUtility.HtmlEncode(c.Frase);
            var ejemplosEnc = string.Join(", ", c.Ejemplos.Select(System.Net.WebUtility.HtmlEncode));
            var etiquetaBanco = c.PosibleNombreDeBanco
                ? $"<p><strong>Nota:</strong> aparece con un solo banco ({System.Net.WebUtility.HtmlEncode(c.NombresBanco.FirstOrDefault() ?? "")}) -- podría ser el nombre del banco, no jerga.</p>"
                : c.BancosDistintos >= 2
                    ? $"<p><strong>Bancos distintos:</strong> {c.BancosDistintos} -- consistente con jerga del tipo de trámite.</p>"
                    : "";

            return $@"
    <div class='highlight'>
      <p><strong>Frase:</strong> {fraseEnc}</p>
      <p><strong>Expedientes distintos:</strong> {c.ExpedientesDistintos}</p>
      <p><strong>Ejemplos:</strong> {ejemplosEnc}</p>
      <p><strong>Rango de fechas:</strong> {c.PrimeraDeteccion:dd/MM/yyyy} – {c.UltimaDeteccion:dd/MM/yyyy}</p>
      {etiquetaBanco}
    </div>";
        }));

        return $@"
<!DOCTYPE html><html><head><meta charset='UTF-8'>
<style>
  body{{font-family:Georgia,'Times New Roman',serif;background:#f7f5f0;margin:0;padding:0;}}
  .container{{max-width:580px;margin:40px auto;background:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);}}
  .header{{background:#1c2b4a;padding:32px 40px;text-align:center;}}
  .header h1{{color:#ffffff;font-size:20px;margin:0;font-weight:normal;letter-spacing:1px;}}
  .header p{{color:#9a7c3c;font-size:13px;margin:6px 0 0;letter-spacing:2px;text-transform:uppercase;}}
  .body{{padding:40px;color:#333333;}}
  .body p{{font-size:15px;line-height:1.7;margin:0 0 16px;}}
  .highlight{{background:#f0f4fa;border-left:4px solid #9a7c3c;padding:16px 20px;margin:16px 0;border-radius:0 6px 6px 0;}}
  .highlight p{{margin:4px 0;font-size:14px;color:#444;}}
  .highlight strong{{color:#1c2b4a;}}
  .footer{{background:#f7f5f0;padding:20px 40px;text-align:center;border-top:1px solid #e0ddd6;}}
  .footer p{{font-size:12px;color:#888;margin:0;}}
</style></head>
<body><div class='container'>
  <div class='header'>
    <h1>Despacho Jurídico Acedo e Hijos</h1>
    <p>Candidatos a frase genérica detectados</p>
  </div>
  <div class='body'>
    <p>Equipo técnico,</p>
    <p>El sistema encontró {(reporte.Candidatos.Count == 1 ? "la siguiente frase que se repite" : $"las siguientes {reporte.Candidatos.Count} frases que se repiten")} en el texto de acuerdos ocultos (confianza baja) lo suficiente como para parecer jerga del tipo de trámite, no un nombre real. Esto es solo un aviso -- ningún acuerdo cambió de visibilidad.</p>
    {tarjetas}
    <p>Si una frase es jerga, agréguela a <code>FrasesGenericasConocidas</code> mediante un cambio de código revisado -- nunca automáticamente. Si es un nombre real que coincidió por casualidad, no hace falta ninguna acción.</p>
    <p>Atentamente,<br><strong>Sistema de Gestión de Expedientes</strong></p>
  </div>
  <div class='footer'>
    <p>Este es un mensaje automático. Por favor no responda a este correo.</p>
  </div>
</div></body></html>";
    }
}
