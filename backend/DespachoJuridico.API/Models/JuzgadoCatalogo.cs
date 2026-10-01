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

    // DJ-122: materias que atiende este juzgado, separadas por coma (ej.
    // "Civil,Familiar") -- se derivan del propio Nombre en DbSeeder
    // (ClasificarJuzgado), no se capturan a mano. Null cuando el juzgado no
    // corresponde a ninguna materia que litigue el despacho hoy (Penal,
    // Laboral, Tribunal Colegiado, Adolescentes, etc.) -- deliberado, no
    // forzado: ese juzgado simplemente no participa del filtro por Materia.
    public string? Materias { get; set; }

    // DJ-122: un juzgado "Mixto" (típico de municipios con un solo juzgado)
    // atiende cualquier materia -- debe aparecer en el combobox sin importar
    // qué Materia se haya elegido, en vez de intentar listar sus materias
    // reales (que no están documentadas por juzgado y variarían caso por caso).
    public bool EsMixto { get; set; }
}
