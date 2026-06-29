using ClosedXML.Excel;
using DespachoJuridico.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly AppDbContext _context;

    public ExportController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/export/expedientes
    [HttpGet("expedientes")]
    public async Task<IActionResult> ExportarExpedientes()
    {
        var expedientes = await _context.Expedientes
            .Include(e => e.Banco)
            .Include(e => e.UsuarioAsignado)
            .OrderBy(e => e.NumeroExpediente)
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Expedientes");

        // Encabezados
        var headers = new[] { "Número", "Parte Demandada", "Juzgado", "Materia", "Tipo Juicio", "Banco", "Estado", "Prioridad", "Asignado a", "Etapa Actual", "Acción Pendiente", "Creado En" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1c2b4a");
            ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }

        // Datos
        for (int i = 0; i < expedientes.Count; i++)
        {
            var e = expedientes[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = e.NumeroExpediente;
            ws.Cell(row, 2).Value = e.ParteDemandada;
            ws.Cell(row, 3).Value = e.Juzgado ?? "";
            ws.Cell(row, 4).Value = e.Materia ?? "";
            ws.Cell(row, 5).Value = e.TipoJuicio ?? "";
            ws.Cell(row, 6).Value = e.Banco?.Nombre ?? "";
            ws.Cell(row, 7).Value = e.Estado.ToString();
            ws.Cell(row, 8).Value = e.Prioridad.ToString();
            ws.Cell(row, 9).Value = e.UsuarioAsignado?.Nombre ?? "";
            ws.Cell(row, 10).Value = e.EtapaActual ?? "";
            ws.Cell(row, 11).Value = e.AccionPendiente ?? "";
            ws.Cell(row, 12).Value = e.CreadoEn.ToLocalTime().ToString("dd/MM/yyyy");
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Position = 0;

        var fecha = DateTime.Now.ToString("yyyyMMdd");
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"expedientes_{fecha}.xlsx");
    }
}