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

    // Texto de Partes que trae ADISON, mostrado solo como referencia — el
    // matching de Hermosillo no lo compara (confía en número + juzgado).
    public string PartesAcuerdo { get; set; } = string.Empty;
    public string Sintesis { get; set; } = string.Empty;
    public DateOnly FechaAcuerdo { get; set; }
}
