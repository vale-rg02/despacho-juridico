namespace DespachoJuridico.API.Models;

// DJ-137: registro de que un candidato a frase genérica (DJ-133) ya fue revisado
// por una persona -- "Aprobado" (es jerga, aunque el cambio real a
// FrasesGenericasConocidas sigue siendo un commit revisado aparte; esta fila es
// solo rastro) o "Descartado" (nombre real que coincidió por casualidad, no jerga).
// Sin esto, CalcularCandidatos no tiene ninguna noción de "ya revisado" y un
// descarte se repetiría en el correo diario de DJ-140 indefinidamente mientras
// el candidato siga cruzando el umbral -- ver docs/propuesta-revision-frases-genericas.md.
//
// CandidatoNormalizado SÍ contiene texto real (un nombre de persona o una razón
// social, por definición) -- vive aquí, en una tabla de solo-lectura-admin, nunca
// en código fuente ni en ningún archivo comiteado a git.
public class RevisionCandidatoFraseGenerica
{
    public int Id { get; set; }

    // Mismo formato que produce FrasesGenericasCandidatasService.NormalizarCandidato
    // -- el servidor normaliza antes de guardar, nunca confía en que el cliente
    // ya lo haya hecho. Único: un candidato solo puede tener una revisión vigente.
    public string CandidatoNormalizado { get; set; } = string.Empty;

    // "Aprobado" | "Descartado"
    public string Decision { get; set; } = string.Empty;

    // Corto, opcional -- motivo de la decisión, nunca texto de Partes adicional.
    public string? Motivo { get; set; }

    public int RevisadoPorId { get; set; }
    public Usuario RevisadoPor { get; set; } = null!;

    public DateTime RevisadoEn { get; set; } = DateTime.UtcNow;
}
