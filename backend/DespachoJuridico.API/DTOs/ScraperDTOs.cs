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
