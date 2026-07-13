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
    public int ExpedientesConsultados { get; set; }
    public List<AcuerdoDetectadoResumen> AcuerdosDetectados { get; set; } = new();
    public List<string> JuzgadosConError { get; set; } = new();
}
