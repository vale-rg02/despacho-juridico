using System.Data;
using System.Text;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Models.Enums;
using ExcelDataReader;

namespace DespachoJuridico.API.Data;

public static class MigracionExcel
{
    public static async Task<MigracionResultado> ImportarAsync(
        AppDbContext context,
        string rutaExcel,
        int creadoPorUsuarioId,
        bool soloAbiertos = true)
    {
        var resultado = new MigracionResultado();

        // Necesario para ExcelDataReader en .NET Core
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

        // Tomamos la primera hoja
        var hoja = dataset.Tables[0];
        var ahora = DateTime.UtcNow;

        // Cargar bancos existentes para hacer match por nombre
        var bancosExistentes = context.Bancos.ToList();

        foreach (DataRow fila in hoja.Rows)
        {
            try
            {
                var numero = fila["Número de Expediente"]?.ToString()?.Trim();
                var parte = fila["Parte Demandada"]?.ToString()?.Trim();

                // Si no tiene número ni parte, saltamos la fila
                if (string.IsNullOrWhiteSpace(numero) && string.IsNullOrWhiteSpace(parte))
                {
                    resultado.FilasSaltadas++;
                    continue;
                }

                // Si soloAbiertos=true, solo importamos los que no estén Cerrados
                var estadoTexto = fila.Table.Columns.Contains("Estado")
                    ? fila["Estado"]?.ToString()?.Trim()
                    : null;

                if (soloAbiertos && estadoTexto?.ToLower() == "cerrado")
                {
                    resultado.FilasSaltadas++;
                    continue;
                }

                // Verificar si ya existe (por número de expediente)
                if (!string.IsNullOrWhiteSpace(numero) &&
                    context.Expedientes.Any(e => e.NumeroExpediente == numero))
                {
                    resultado.Duplicados++;
                    resultado.Errores.Add($"Duplicado: {numero}");
                    continue;
                }

                // Resolver banco
                int? bancoId = null;
                var bancoNombre = fila.Table.Columns.Contains("Banco")
                    ? fila["Banco"]?.ToString()?.Trim()
                    : null;

                if (!string.IsNullOrWhiteSpace(bancoNombre))
                {
                    var banco = bancosExistentes.FirstOrDefault(b =>
                        b.Nombre.Equals(bancoNombre, StringComparison.OrdinalIgnoreCase));

                    if (banco == null)
                    {
                        banco = new Banco { Nombre = bancoNombre, CreadoEn = ahora };
                        context.Bancos.Add(banco);
                        await context.SaveChangesAsync();
                        bancosExistentes.Add(banco);
                    }

                    bancoId = banco.Id;
                }

                // Resolver estado
                var estado = estadoTexto?.ToLower() switch
                {
                    "cerrado" => EstadoExpediente.Cerrado,
                    "pausado" => EstadoExpediente.Pausado,
                    _ => EstadoExpediente.Abierto
                };

                // Resolver materia
                var materia = fila.Table.Columns.Contains("Materia")
                    ? fila["Materia"]?.ToString()?.Trim()
                    : null;

                // Resolver juzgado
                var juzgado = fila.Table.Columns.Contains("Juzgado")
                    ? fila["Juzgado"]?.ToString()?.Trim()
                    : null;

                // Resolver notas
                var notas = fila.Table.Columns.Contains("Notas")
                    ? fila["Notas"]?.ToString()?.Trim()
                    : null;

                // Resolver tipo de juicio
                var tipoJuicio = fila.Table.Columns.Contains("Tipo de Juicio")
                    ? fila["Tipo de Juicio"]?.ToString()?.Trim()
                    : null;

                var expediente = new Expediente
                {
                    NumeroExpediente = numero ?? $"IMP-{resultado.Importados + 1}",
                    ParteDemandada = parte ?? "Sin especificar",
                    BancoId = bancoId,
                    Juzgado = string.IsNullOrWhiteSpace(juzgado) ? null : juzgado,
                    Materia = string.IsNullOrWhiteSpace(materia) ? null : materia,
                    TipoJuicio = string.IsNullOrWhiteSpace(tipoJuicio) ? null : tipoJuicio,
                    Estado = estado,
                    Prioridad = Prioridad.Normal,
                    Notas = string.IsNullOrWhiteSpace(notas) ? null : notas,
                    CreadoPorId = creadoPorUsuarioId,
                    CreadoEn = ahora,
                    ActualizadoEn = ahora
                };

                context.Expedientes.Add(expediente);
                resultado.Importados++;
            }
            catch (Exception ex)
            {
                resultado.Errores.Add($"Error en fila: {ex.Message}");
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