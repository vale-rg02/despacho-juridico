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
    // "GARANTIA") -- DJ-134: no se tratan automáticamente (no pueden distinguirse
    // de un apellido real sin revisión humana), pero sí se reportan en
    // CandidatosPalabraUnica para que el equipo técnico los revise.
    public int ExcluidosPorPalabraUnica { get; set; }

    // DJ-134: mismo contenido que Candidatos, pero para los candidatos de una
    // sola palabra -- nunca se agregan a Candidatos (no pasan por el mismo
    // nivel de filtro que un candidato de 2+ palabras), se listan aparte para
    // que quede claro que requieren revisión manual antes de decidir si es
    // jerga o un nombre real.
    public List<CandidatoFraseGenerica> CandidatosPalabraUnica { get; set; } = new();

    // Candidatos que ya están en FrasesGenericasConocidas -- no se repiten en
    // el reporte porque su historial nunca desaparece (ver sección 5.2 de la
    // investigación).
    public int ExcluidosPorYaAprobada { get; set; }

    // DJ-137: candidatos ya revisados y marcados "Descartado" en
    // RevisionCandidatoFraseGenerica (nombre real que coincidió por casualidad,
    // no jerga) -- sin esto se repetirían en el reporte indefinidamente.
    public int ExcluidosPorYaDescartado { get; set; }
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

// DJ-137: registrar la revisión de un candidato -- "Aprobado" (jerga, rastro
// informativo; el cambio real a FrasesGenericasConocidas sigue siendo un commit
// aparte) o "Descartado" (nombre real, se excluye del reporte de ahí en adelante).
public class RevisionCandidatoRequest
{
    public string Candidato { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string? Motivo { get; set; }
}

// Nunca incluye el candidato en texto plano más allá de lo que ya es la propia
// tabla (acceso solo-admin) -- esta respuesta es la confirmación de que se
// guardó, no una forma nueva de exponer el texto.
public class RevisionCandidatoResponse
{
    public int Id { get; set; }
    public string Decision { get; set; } = string.Empty;
    public DateTime RevisadoEn { get; set; }
    public string RevisadoPorNombre { get; set; } = string.Empty;
}
