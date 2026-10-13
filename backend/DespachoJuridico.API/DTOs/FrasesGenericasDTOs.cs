namespace DespachoJuridico.API.DTOs;

// DJ-133 (implementa el diseño de Parte 2 de DJ-131, ver
// docs/investigacion-frases-genericas.md): respuesta de la vista previa y
// contenido base del correo periódico de candidatos a frase genérica.
public class ReporteCandidatosResponse
{
    public int Umbral { get; set; }
    public List<CandidatoFraseGenerica> Candidatos { get; set; } = new();

    // Acuerdos descartados por no pasar la revalidación de JuzgadoCoincide
    // actual -- ruido conocido de vínculos viejos (ver comentario "102 registros
    // históricos" en ScraperAcuerdosService.cs), no el bug que esta historia
    // ataca. Cuenta acuerdos, no candidatos distintos.
    public int ExcluidosPorJuzgadoNoValido { get; set; }

    // Candidatos que sí cruzan el umbral pero son de una sola palabra (ej.
    // "GARANTIA") -- quedan fuera de esta historia, ver DJ-134.
    public int ExcluidosPorPalabraUnica { get; set; }

    // Candidatos que ya están en FrasesGenericasConocidas -- no se repiten en
    // el reporte porque su historial nunca desaparece (ver sección 5.2 de la
    // investigación).
    public int ExcluidosPorYaAprobada { get; set; }
}

public class CandidatoFraseGenerica
{
    public string Frase { get; set; } = string.Empty;
    public int ExpedientesDistintos { get; set; }

    // Ejemplos de (NumeroExpediente, NombreJuzgado) -- nunca nombres de partes.
    public List<string> Ejemplos { get; set; } = new();

    public DateTime PrimeraDeteccion { get; set; }
    public DateTime UltimaDeteccion { get; set; }

    // Cuántos bancos distintos (Expediente.BancoId) aparecen entre los
    // expedientes de este candidato. 1 solo banco es señal de que el candidato
    // probablemente ES el nombre de ese banco, no jerga (ver sección 4.2 de la
    // investigación) -- se etiqueta, nunca se oculta del reporte.
    public int BancosDistintos { get; set; }
    public bool PosibleNombreDeBanco { get; set; }
    public List<string> NombresBanco { get; set; } = new();
}
