namespace DespachoJuridico.API.Models;

// DJ-102: fila única (Id=1) con la última corrida del scraper que cubrió TODOS
// los juzgados sin restricción (ciclo automático, o un admin corriendo /ejecutar
// sin idsUnidad) -- nunca se actualiza con una corrida manual acotada a los
// expedientes de un litigante (ver ScraperAcuerdosService.EjecutarScrapingAsync).
public class EstadoScraper
{
    public int Id { get; set; }
    public DateTime? UltimaCorridaCompletaEn { get; set; }
}
