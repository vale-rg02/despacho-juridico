using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace DespachoJuridico.API.Services;

// DJ-112/DJ-87: deriva Sede y normaliza Juzgado para expedientes que ya
// existían con Juzgado en texto libre. A diferencia de las migraciones de
// DJ-76/78/119 (que corren solas en cada arranque vía DbSeeder.SeedAsync),
// esta NO se auto-ejecuta -- reescribe un campo que el despacho ya capturó a
// mano en hasta ~377 expedientes, así que requiere un paso de revisión humana
// explícita: se corre en dryRun primero (AdminController), se revisa el
// reporte contra los valores reales, y solo con ok explícito se corre en
// firme. Ver plan de rollout de esta historia.
public static class MigracionSedeJuzgadoService
{
    // Los 15 municipios foráneos que cubre ADISON (todo lo que no es
    // Hermosillo, el default cuando el texto no menciona ninguna ciudad --
    // mismo criterio que la mayoría real de los datos de producción).
    private static readonly string[] MunicipiosForaneos =
    {
        "Cajeme", "Agua Prieta", "Álamos", "Caborca", "Cananea", "Cumpas", "Guaymas",
        "Huatabampo", "Magdalena", "Navojoa", "Nogales", "Puerto Peñasco", "Sahuaripa",
        "San Luis Río Colorado", "Ures"
    };

    private const double UmbralSimilitud = 0.8; // mismo default que PartesCoinciden

    // Mismo criterio de normalización que ScraperAcuerdosService (mayúsculas,
    // sin acentos, espacios colapsados, sin relleno "de lo"/"de la") --
    // replicado aquí en vez de exponer esas funciones desde el scraper, para
    // no arriesgar su lógica interna ya afinada (ver DJ-103). Además convierte
    // ordinales escritos ("PRIMERO", "SEGUNDO"...) a la forma abreviada del
    // catálogo ("1RO", "2DO"...) -- sin esto, "SEGUNDO ORAL MERCANTIL" (30
    // expedientes reales) y "PRIMERO ORAL MERCANTIL" (28 reales) no alcanzan
    // el umbral de similitud contra "2do/1ro Oral Mercantil Hermosillo", por
    // ser cadenas léxicamente muy distintas aunque signifiquen lo mismo.
    internal static string NormalizarTexto(string texto)
    {
        var mayusculas = texto.Trim().ToUpperInvariant();
        var sinAcentos = string.Concat(
            mayusculas.Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark));
        var sinRelleno = Regex.Replace(sinAcentos, @"\bDE\s+L[OA]\b", " ");
        var ordinalesAbreviados = ConvertirOrdinalesEscritos(sinRelleno);
        return Regex.Replace(ordinalesAbreviados, @"\s+", " ").Trim();
    }

    private static readonly (string Escrito, string Abreviado)[] Ordinales =
    {
        ("PRIMERO", "1RO"), ("SEGUNDO", "2DO"), ("TERCERO", "3RO"), ("CUARTO", "4TO"), ("QUINTO", "5TO"),
    };

    private static string ConvertirOrdinalesEscritos(string texto)
    {
        foreach (var (escrito, abreviado) in Ordinales)
            texto = Regex.Replace(texto, $@"\b{escrito}\b", abreviado);
        return texto;
    }

    // Mención explícita de un municipio foráneo en el texto -- null si no hay
    // ninguna (se asume Hermosillo).
    internal static string? DetectarSedeForanea(string juzgadoTexto)
    {
        var normalizado = NormalizarTexto(juzgadoTexto);
        foreach (var municipio in MunicipiosForaneos)
        {
            var patron = $@"\b{Regex.Escape(NormalizarTexto(municipio))}\b";
            if (Regex.IsMatch(normalizado, patron)) return municipio;
        }
        return null;
    }

    // El candidato del catálogo (ya acotado a una Sede) más parecido al texto
    // libre, o null si ninguno alcanza el umbral de confianza -- no se fuerza
    // un mapeo dudoso.
    // Un texto de 1-3 caracteres ("A", "1", "kk") no tiene contenido suficiente
    // para comparar con confianza -- cualquier candidato del catálogo "contiene"
    // por casualidad una letra o dígito suelto, dando una similitud del 100%
    // que no significa nada. Caso real encontrado al probar contra basura real
    // de producción ("A", "1"): sin este mínimo, mapeaban falso a cualquier
    // juzgado del catálogo.
    private const int LongitudMinimaParaComparar = 4;

    internal static string? EncontrarJuzgadoCanonico(string juzgadoTexto, IEnumerable<string> candidatos)
    {
        var normalizado = NormalizarTexto(juzgadoTexto);
        if (normalizado.Length < LongitudMinimaParaComparar) return null;

        string? mejor = null;
        var mejorSimilitud = 0.0;

        foreach (var candidato in candidatos)
        {
            var candidatoNormalizado = NormalizarTexto(candidato);

            // SimilitudMaximaSubcadena normaliza por la longitud del PRIMER
            // argumento ("patron") -- hay que pasarle siempre el más corto de
            // los dos, o un candidato más largo (ej. "...Hermosillo" cuando el
            // texto histórico no repite la ciudad, como "Arrendamiento" vs
            // "Arrendamiento Hermosillo") sale penalizado injustamente por el
            // texto de más, aunque sea el mismo juzgado.
            var similitud = candidatoNormalizado.Length <= normalizado.Length
                ? ScraperAcuerdosService.SimilitudMaximaSubcadena(candidatoNormalizado, normalizado)
                : ScraperAcuerdosService.SimilitudMaximaSubcadena(normalizado, candidatoNormalizado);

            if (similitud > mejorSimilitud)
            {
                mejorSimilitud = similitud;
                mejor = candidato;
            }
        }

        return mejorSimilitud >= UmbralSimilitud ? mejor : null;
    }

    public static async Task<ResultadoMigracionSedeJuzgado> MigrarAsync(AppDbContext context, bool dryRun)
    {
        var juzgadosCatalogo = await context.JuzgadosCatalogo
            .Include(j => j.Sede)
            .ToListAsync();

        var expedientes = await context.Expedientes
            .Where(e => e.Sede == null && e.Juzgado != null && e.Juzgado != "")
            .ToListAsync();

        var resultado = new ResultadoMigracionSedeJuzgado { DryRun = dryRun, TotalEvaluados = expedientes.Count };

        foreach (var expediente in expedientes)
        {
            var sedeDetectada = DetectarSedeForanea(expediente.Juzgado!) ?? "Hermosillo";
            var candidatos = juzgadosCatalogo
                .Where(j => j.Sede.Nombre == sedeDetectada)
                .Select(j => j.Nombre);

            var canonico = EncontrarJuzgadoCanonico(expediente.Juzgado!, candidatos);

            if (canonico != null)
            {
                resultado.Mapeados.Add(new MapeoSedeJuzgado
                {
                    ExpedienteId = expediente.Id,
                    NumeroExpediente = expediente.NumeroExpediente,
                    JuzgadoOriginal = expediente.Juzgado!,
                    SedeAsignada = sedeDetectada,
                    JuzgadoCanonico = canonico
                });

                if (!dryRun)
                {
                    expediente.Sede = sedeDetectada;
                    expediente.Juzgado = canonico;
                }
            }
            else
            {
                resultado.SinMapear.Add(new MapeoSedeJuzgado
                {
                    ExpedienteId = expediente.Id,
                    NumeroExpediente = expediente.NumeroExpediente,
                    JuzgadoOriginal = expediente.Juzgado!
                });
            }
        }

        if (!dryRun)
            await context.SaveChangesAsync();

        return resultado;
    }
}
