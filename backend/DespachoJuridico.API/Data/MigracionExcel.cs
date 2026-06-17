using System.Data;
using System.Text;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Models.Enums;
using ExcelDataReader;

namespace DespachoJuridico.API.Data;

public static class MigracionExcel
{
    private static readonly (string Columna, string NombreEtapa, int Orden)[] EtapasProceso =
    [
        ("DEMANDA",                        "Demanda",                        1),
    ("RADICACIÓN",                     "Radicación",                     2),
    ("Término",                        "Término",                        3),
    ("EMPLAZAMIENTO",                  "Emplazamiento",                  4),
    ("CONTESTACIÓN ",                  "Contestación",                   5),
    ("ACUSAR REBELDÍA ",               "Acusar Rebeldía",                6),
    ("REBELDIA ",                      "Rebeldía",                       7),
    ("PRUEBAS",                        "Pruebas",                        8),
    ("ALEGATOS ",                      "Alegatos",                       9),
    ("Audiencia Preeliminar",          "Audiencia Preliminar",          10),
    ("Audiencia de Juicio",            "Audiencia de Juicio",           11),
    ("Audiencia de sentencia",         "Audiencia de Sentencia",        12),
    ("SENTENCIA",                      "Sentencia",                     13),
    ("Término para amparo",            "Término para Amparo",           14),
    ("AMPARO",                         "Amparo",                        15),
    ("COSA JUZGADA",                   "Cosa Juzgada",                  16),
    ("CERTIFICADO DE GRAVAMEN ",       "Certificado de Gravamen",       17),
    ("AVALUOS ",                       "Avalúos",                       18),
    ("DILIGENCIA DE REMATE ",          "Diligencia de Remate",          19),
    ("AUTO APROBATORIO",               "Auto Aprobatorio",              20),
    ("AUTO QUE DECLARA FIRME REMATE ", "Auto que declara firme remate", 21),
    ("ESCRITURA DE ADJUDICACIÓN ",     "Escritura de adjudicación",     22),
    ("LANZAMIENTO",                    "Lanzamiento",                   23),
];

    public static async Task<MigracionResultado> ImportarAsync(
        AppDbContext context,
        string rutaExcel,
        int creadoPorUsuarioId,
        bool soloAbiertos = true)
    {
        var resultado = new MigracionResultado();

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var stream = File.Open(rutaExcel, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataset = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = true
            }
        });

        var hoja = dataset.Tables[0];
        var ahora = DateTime.UtcNow;

        var numerosExistentes = context.Expedientes
            .Select(e => e.NumeroExpediente)
            .ToHashSet();

        var catalogosExistentes = context.EtapasCatalogo.ToList();

        int numeroFila = 1;

        foreach (DataRow fila in hoja.Rows)
        {
            numeroFila++;
            string? numero = null;

            try
            {
                numero = fila["EXP"]?.ToString()?.Trim();
                var parte = fila["PARTE"]?.ToString()?.Trim();

                if (string.IsNullOrWhiteSpace(numero) && string.IsNullOrWhiteSpace(parte))
                {
                    resultado.FilasSaltadas++;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(numero) && numerosExistentes.Contains(numero))
                {
                    resultado.Duplicados++;
                    resultado.Errores.Add($"Duplicado (fila {numeroFila}): {numero}");
                    continue;
                }

                var juzgado = fila.Table.Columns.Contains("JUZGADO")
                    ? fila["JUZGADO"]?.ToString()?.Trim()
                    : null;

                var notas = fila.Table.Columns.Contains("NOTAS")
                    ? fila["NOTAS"]?.ToString()?.Trim()
                    : null;

                var materia = fila.Table.Columns.Contains("MATERIA")
                    ? fila["MATERIA"]?.ToString()?.Trim()
                    : null;

                var etapaActual = fila.Table.Columns.Contains("ETAPA")
                    ? fila["ETAPA"]?.ToString()?.Trim()
                    : null;

                var accionColumna = fila.Table.Columns
                    .Cast<DataColumn>()
                    .FirstOrDefault(c => c.ColumnName.Trim().ToLower()
                        .Replace("ó", "o").Replace("á", "a") == "accion")
                    ?.ColumnName;

                // Buscar Acción por posición (columna 4, índice base 0) 
                // porque el encoding del Excel no coincide con el string literal
                var accionActual = hoja.Columns.Count > 4
                    ? fila[4]?.ToString()?.Trim()
                    : null;

                var expediente = new Expediente
                {
                    NumeroExpediente = numero ?? $"IMP-{resultado.Importados + 1}",
                    ParteDemandada = parte ?? "Sin especificar",
                    Juzgado = string.IsNullOrWhiteSpace(juzgado) ? null : juzgado,
                    Materia = string.IsNullOrWhiteSpace(materia) ? null : materia,
                    Estado = EstadoExpediente.Abierto,
                    Prioridad = Prioridad.Normal,
                    Notas = string.IsNullOrWhiteSpace(notas) ? null : notas,
                    EtapaActual = string.IsNullOrWhiteSpace(etapaActual) ? null : etapaActual,
                    AccionPendiente = string.IsNullOrWhiteSpace(accionActual) ? null : accionActual,
                    CreadoPorId = creadoPorUsuarioId,
                    CreadoEn = ahora,
                    ActualizadoEn = ahora
                };

                foreach (var (columna, nombreEtapa, orden) in EtapasProceso)
                {
                    if (!fila.Table.Columns.Contains(columna))
                        continue;

                    var valorCelda = fila[columna]?.ToString()?.Trim();

                    if (string.IsNullOrWhiteSpace(valorCelda))
                        continue;

                    if (valorCelda.ToLower().Contains("n/a") ||
                        valorCelda.ToLower().Contains("no tuvo"))
                        continue;

                    var catalogo = catalogosExistentes.FirstOrDefault(c =>
                        c.Nombre.Equals(nombreEtapa, StringComparison.OrdinalIgnoreCase));

                    if (catalogo == null)
                    {
                        catalogo = new EtapaCatalogo
                        {
                            Nombre = nombreEtapa,
                            Orden = orden,
                            EsDiasHabiles = true
                        };
                        context.EtapasCatalogo.Add(catalogo);
                        catalogosExistentes.Add(catalogo);
                    }

                    DateTime? fechaCompletada = null;
                    string? notasEtapa = null;

                    if (DateTime.TryParseExact(valorCelda, "dd/MM/yyyy",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out var fechaParseada))
                    {
                        fechaCompletada = DateTime.SpecifyKind(fechaParseada, DateTimeKind.Utc);
                    }
                    else
                    {
                        notasEtapa = $"Valor original del Excel: {valorCelda}";
                    }

                    expediente.Historial.Add(new HistorialEtapa
                    {
                        EtapaCatalogo = catalogo,
                        FechaInicio = fechaCompletada ?? ahora,
                        FechaCompletada = fechaCompletada,
                        FechaLimite = null,
                        Atendido = fechaCompletada.HasValue,
                        RegistradoPorId = creadoPorUsuarioId,
                        Notas = notasEtapa
                    });
                }

                context.Expedientes.Add(expediente);

                if (!string.IsNullOrWhiteSpace(numero))
                    numerosExistentes.Add(numero);

                resultado.Importados++;
            }
            catch (Exception ex)
            {
                var referencia = !string.IsNullOrWhiteSpace(numero)
                    ? $"expediente {numero}"
                    : $"fila {numeroFila}";

                resultado.Errores.Add($"Error en {referencia}: {ex.Message}");
            }
        }

        await context.SaveChangesAsync();
        return resultado;
    }
}

public class MigracionResultado
{
    public int Importados { get; set; }
    public int Duplicados { get; set; }
    public int FilasSaltadas { get; set; }
    public List<string> Errores { get; set; } = new();

    public void Imprimir()
    {
        Console.WriteLine("═══════════════════════════════════");
        Console.WriteLine("   RESULTADO DE MIGRACIÓN");
        Console.WriteLine("═══════════════════════════════════");
        Console.WriteLine($"✅ Importados:    {Importados}");
        Console.WriteLine($"⚠️  Duplicados:    {Duplicados}");
        Console.WriteLine($"⏭️  Saltados:      {FilasSaltadas}");
        Console.WriteLine($"❌ Errores:       {Errores.Count}");
        if (Errores.Count > 0)
        {
            Console.WriteLine("\nDetalle de errores:");
            foreach (var e in Errores)
                Console.WriteLine($"  • {e}");
        }
        Console.WriteLine("═══════════════════════════════════");
    }
}