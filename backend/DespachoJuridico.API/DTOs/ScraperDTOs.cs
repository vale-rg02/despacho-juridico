namespace DespachoJuridico.API.DTOs;

public class AcuerdoDetectadoResumen
{
    public string NumeroExpediente { get; set; } = string.Empty;
    public string Juzgado { get; set; } = string.Empty;
    public string Sintesis { get; set; } = string.Empty;
    public DateOnly FechaAcuerdo { get; set; }
}

public class ResultadoScrapingResponse
{
    public DateOnly Fecha { get; set; }
    public bool DryRun { get; set; }
    public int ExpedientesConsultados { get; set; }
    public List<AcuerdoDetectadoResumen> AcuerdosDetectados { get; set; } = new();
    public List<string> JuzgadosConError { get; set; } = new();

    // Solo se llena en dry-run: matches de juzgados foráneos evaluados (número
    // de expediente coincide) pero sin guardar ni notificar, para poder revisar
    // la calidad del criterio de verificación por Partes antes de activarlo.
    public List<MatchForaneoEvaluado> MatchesForaneosEvaluados { get; set; } = new();

    // Solo se llena en dry-run: matches de juzgados de Hermosillo (número +
    // juzgado coinciden, sin verificación de Partes) evaluados pero sin guardar
    // ni notificar — para poder revisar el resultado antes de que se guarde.
    public List<MatchHermosilloEvaluado> MatchesHermosilloEvaluados { get; set; } = new();

    // DJ-139: embudo de esta corrida, siempre lleno (dry-run o real) -- mismo
    // formato que ObtenerResumenDiaAsync, para revisarlo sin esperar a que se
    // guarde en la bitácora.
    public Guid CorridaId { get; set; }
    public List<EmbudoJuzgadoResponse> Embudo { get; set; } = new();

    // DJ-139: los descartes "interesantes" de esta corrida (nunca el masivo
    // sin candidato) -- siempre lleno, dry-run o real, para poder revisarlos
    // sin esperar a que se escriban en la bitácora. Nunca contiene Partes ni
    // nombres.
    public List<DescarteItemizadoResponse> DescartesItemizados { get; set; } = new();
}

// DJ-139: un descarte "interesante" -- solo número de expediente, juzgado y
// razón, nunca Partes ni nombres.
public class DescarteItemizadoResponse
{
    public string NumeroExpediente { get; set; } = string.Empty;
    public string NombreJuzgado { get; set; } = string.Empty;
    public string Razon { get; set; } = string.Empty;
    public string? EtapaFalla { get; set; }
}

// DJ-139: el embudo de un juzgado en una corrida -- nunca contiene Partes ni
// nombres de partes, solo conteos y el motivo de un fallo de consulta.
public class EmbudoJuzgadoResponse
{
    public int IdUnidad { get; set; }
    public string NombreJuzgado { get; set; } = string.Empty;
    public string Origen { get; set; } = string.Empty;
    public bool DryRun { get; set; }
    public bool ConsultaExitosa { get; set; }
    public string? MotivoFallo { get; set; }
    public int Devueltos { get; set; }
    public int Evaluaciones { get; set; }
    public int GuardadoVisibleAlta { get; set; }
    public int GuardadoVisibleMedia { get; set; }
    public int GuardadoOcultoBaja { get; set; }
    public int YaExistia { get; set; }
    public int DescartadoSinCandidato { get; set; }
    public int DescartadoNumeroJuzgadoNoCoincide { get; set; }
    public int DescartadoExpedienteCerrado { get; set; }
    public int DescartadoFueraDeAlcanceFiltro { get; set; }
    public int DescartadoExcepcion { get; set; }
}

// DJ-139: resumen de un día completo -- por juzgado y total. Es el contrato de
// lectura que DJ-140 consume (vía ObtenerResumenDiaAsync o
// GET /api/scraper/bitacora-dia).
public class ResumenBitacoraDiaResponse
{
    public DateOnly Fecha { get; set; }
    public List<EmbudoJuzgadoResponse> PorJuzgado { get; set; } = new();
    public List<DescarteItemizadoResponse> DescartesItemizados { get; set; } = new();
    public EmbudoJuzgadoResponse Total { get; set; } = new();
}

public class MatchForaneoEvaluado
{
    public string NumeroExpediente { get; set; } = string.Empty;
    public string Juzgado { get; set; } = string.Empty;
    public string ParteDemandadaExpediente { get; set; } = string.Empty;
    public string PartesAcuerdo { get; set; } = string.Empty;

    // "Alta" si ParteDemandada del expediente aparece dentro de Partes del
    // acuerdo (case-insensitive, tolerante a acentos); "Baja" si no.
    public string Confianza { get; set; } = string.Empty;
    public string Sintesis { get; set; } = string.Empty;
    public DateOnly FechaAcuerdo { get; set; }
}

public class MatchHermosilloEvaluado
{
    public string NumeroExpediente { get; set; } = string.Empty;
    public string Juzgado { get; set; } = string.Empty;
    public string ParteDemandadaExpediente { get; set; } = string.Empty;
    public string PartesAcuerdo { get; set; } = string.Empty;

    // "Alta"/"Baja" si ADISON trajo un nombre reconocible en Partes y se pudo
    // verificar; null si no traía nombre y se confió en número+juzgado sin
    // comparar (ver PartesTieneNombre).
    public string? Confianza { get; set; }
    public string Sintesis { get; set; } = string.Empty;
    public DateOnly FechaAcuerdo { get; set; }
}

public class ResultadoReevaluacionResponse
{
    public bool DryRun { get; set; }
    public int RegistrosEvaluados { get; set; }
    public List<AcuerdoDetectadoResumen> RegistrosDesocultados { get; set; } = new();

    // DJ-122: ocultos que no llegan a coincidir por nombre pero sí califican para
    // la sugerencia "Media" (banco + juzgado/número ya confirmados) — separados de
    // RegistrosDesocultados porque suben a Media, no a Alta.
    public List<AcuerdoDetectadoResumen> RegistrosSugeridos { get; set; } = new();

    // 27 oct 2026: si dryRun=false pero RegistrosDesocultados+RegistrosSugeridos
    // excede el tope (ScraperAcuerdos:TopeReevaluacionPorCorrida), no se guarda ni
    // se notifica nada -- se comporta como si hubiera sido dryRun=true, pero
    // dejando explícito por qué (un volumen muy por encima de lo histórico puede
    // significar que el supuesto detrás de alguna regla ya no aplica, y conviene
    // que alguien lo revise antes de mandar correos reales en masa).
    public bool TopeExcedido { get; set; }
    public int Tope { get; set; }
}

// Un registro real de AcuerdosScrapeados (visible u oculto) para el endpoint de
// diagnóstico GET /api/scraper/registros — a diferencia de los DTOs de arriba,
// que solo existen durante dryRun, este refleja lo que de verdad quedó guardado.
public class RegistroScraperDiaResponse
{
    public int Id { get; set; }
    public string NumeroExpediente { get; set; } = string.Empty;
    public string NombreJuzgado { get; set; } = string.Empty;
    public string? Asignado { get; set; }
    public DateOnly FechaAcuerdo { get; set; }
    public DateTime FechaDetectado { get; set; }
    public string? Confianza { get; set; }
    public bool Oculto { get; set; }
    public bool NotificacionEnviada { get; set; }
    public string Partes { get; set; } = string.Empty;
    public string ParteDemandada { get; set; } = string.Empty;
    public bool RegistradoManualmente { get; set; }

    // DJ-99: distingue si Oculto=true vino del algoritmo (baja confianza) o de un
    // litigante descartándolo a mano — sin esto, este diagnóstico no podía
    // diferenciar ambos orígenes.
    public bool DescartadoManualmente { get; set; }
}

// DJ-102: respuesta de GET /api/scraper/mi-estado-actualizacion -- estado del
// botón "Actualizar expedientes" para el usuario autenticado.
public class EstadoActualizacionScraperResponse
{
    public bool EnProgreso { get; set; }

    // La corrida automática global más reciente, o la propia corrida manual del
    // usuario, la que sea más nueva -- null si ninguna de las dos ocurrió todavía.
    public DateTime? UltimaActualizacionEn { get; set; }

    public int CooldownRestanteSegundos { get; set; }
    public bool PuedeActualizar { get; set; }
}
