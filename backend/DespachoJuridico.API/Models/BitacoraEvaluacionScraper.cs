namespace DespachoJuridico.API.Models;

// DJ-139: una fila por corrida y por juzgado -- el embudo completo de qué pasó
// con cada registro que devolvió ADISON para ese juzgado en esa corrida. Nunca
// contiene Partes ni nombres -- solo conteos, número de expediente (en
// BitacoraDescarteItemizado), juzgado y código de razón. Tabla aditiva, no
// cambia nada de AcuerdosScrapeados ni de la lógica de matching -- es solo
// observación (ver docs/propuestas-tecnicas.md si se necesita contexto del
// plan de cierre que la originó).
public class BitacoraEvaluacionScraper
{
    public int Id { get; set; }

    // Agrupa todas las filas (una por juzgado) de la misma corrida.
    public Guid CorridaId { get; set; }

    public DateTime FechaHoraCorrida { get; set; } = DateTime.UtcNow;
    public DateOnly FechaConsultada { get; set; }
    public int IdUnidad { get; set; }
    public string NombreJuzgado { get; set; } = string.Empty;

    // "Automatica" | "Manual" (DJ-102) | "Backfill" (notificar=false) --
    // derivado de los parámetros de la corrida, no es un parámetro nuevo.
    public string Origen { get; set; } = string.Empty;
    public bool DryRun { get; set; }

    public bool ConsultaExitosa { get; set; }
    // Solo cuando ConsultaExitosa=false -- ej. "HTTP_403", "RESPUESTA_SIN_RESULTADO",
    // o el tipo de excepción si fue un fallo de red/timeout.
    public string? MotivoFallo { get; set; }

    // Lo que regresó ADISON, antes de cualquier matching.
    public int Devueltos { get; set; }

    // Devueltos + candidatos extra cuando un mismo registro coincide con 2+
    // expedientes (números duplicados entre litigantes) -- la suma de las
    // categorías de abajo siempre es igual a Evaluaciones, no necesariamente a
    // Devueltos.
    public int Evaluaciones { get; set; }

    public int GuardadoVisibleAlta { get; set; }
    public int GuardadoVisibleMedia { get; set; }
    public int GuardadoOcultoBaja { get; set; }
    public int YaExistia { get; set; }

    // El descarte masivo (la inmensa mayoría -- casos ajenos al despacho) se
    // cuenta aquí, sin itemizar. Los demás sí se itemizan (ver
    // BitacoraDescarteItemizado) ademas de contarse aquí.
    public int DescartadoSinCandidato { get; set; }
    public int DescartadoNumeroJuzgadoNoCoincide { get; set; }
    public int DescartadoExpedienteCerrado { get; set; }
    public int DescartadoFueraDeAlcanceFiltro { get; set; }
    public int DescartadoExcepcion { get; set; }
}
