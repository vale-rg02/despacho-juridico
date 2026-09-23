namespace DespachoJuridico.API.Models;

// DJ-87: catálogo de juzgados, dependiente de Sede (un juzgado pertenece a
// exactamente un municipio). Fuente real: el diccionario interno de ADISON en
// ScraperAcuerdosService.cs (uso exclusivo del scraper) -- este catálogo es
// una copia deliberada, no una lectura en vivo de ese diccionario, para no
// arriesgar la lógica de matching ya afinada en varios tickets (ver DJ-103).
public class JuzgadoCatalogo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public int SedeId { get; set; }
    public SedeCatalogo Sede { get; set; } = null!;
}
