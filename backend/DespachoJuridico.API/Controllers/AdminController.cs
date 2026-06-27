using DespachoJuridico.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private const int SocioPrincipalId = 1;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/admin/actividad?periodo=dia|semana|mes
    [HttpGet("actividad")]
    public async Task<IActionResult> GetActividad([FromQuery] string periodo = "semana")
    {
        var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var ahora = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaHoraria);

        var desde = periodo switch
        {
            "dia" => ahora.Date,
            "mes" => new DateTime(ahora.Year, ahora.Month, 1),
            _ => ahora.Date.AddDays(-(int)ahora.DayOfWeek + 1) // semana
        };

        var desdeUtc = TimeZoneInfo.ConvertTimeToUtc(desde, zonaHoraria);

        var bitacora = await _context.BitacoraCambios
            .Include(b => b.Usuario)
            .Include(b => b.Expediente)
            .Where(b => b.Fecha >= desdeUtc)
            .OrderByDescending(b => b.Fecha)
            .Select(b => new
            {
                b.Id,
                b.Accion,
                b.Detalle,
                b.Fecha,
                UsuarioNombre = b.Usuario.Nombre,
                NumeroExpediente = b.Expediente.NumeroExpediente
            })
            .ToListAsync();

        var resumenPorUsuario = bitacora
            .GroupBy(b => b.UsuarioNombre)
            .Select(g => new
            {
                Usuario = g.Key,
                TotalAcciones = g.Count(),
                Acciones = g.ToList()
            })
            .OrderByDescending(g => g.TotalAcciones)
            .ToList();

        return Ok(new
        {
            periodo,
            desde,
            resumenPorUsuario
        });
    }
}