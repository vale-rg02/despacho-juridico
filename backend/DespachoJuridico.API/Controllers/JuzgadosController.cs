using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Controllers;

// DJ-87: catálogo de Juzgado, dependiente de Sede.
[ApiController]
[Route("api/juzgados")]
[Authorize]
public class JuzgadosController : ControllerBase
{
    private readonly AppDbContext _context;

    public JuzgadosController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/juzgados?sedeId=3 — filtrado por Sede, para el combobox
    // dependiente del formulario de expediente. Sin sedeId, lista vacía (igual
    // que EtapasCatalogoController.GetAll sin tipoJuicio) -- no tiene sentido
    // mostrar los 83 juzgados de todo el estado mezclados.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? sedeId)
    {
        if (sedeId == null)
            return Ok(new List<JuzgadoCatalogoResponse>());

        var juzgados = await _context.JuzgadosCatalogo
            .Include(j => j.Sede)
            .Where(j => j.SedeId == sedeId)
            .OrderBy(j => j.Nombre)
            .Select(j => new JuzgadoCatalogoResponse
            {
                Id = j.Id,
                Nombre = j.Nombre,
                SedeId = j.SedeId,
                SedeNombre = j.Sede.Nombre
            })
            .ToListAsync();

        return Ok(juzgados);
    }

    // POST /api/juzgados — solo admin (mismo criterio que SedesController).
    [HttpPost]
    [Authorize(Policy = "AccesoAdmin")]
    public async Task<IActionResult> Create([FromBody] CrearJuzgadoRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var sede = await _context.SedesCatalogo.FindAsync(request.SedeId);
        if (sede == null)
            return BadRequest(new { mensaje = "La sede indicada no existe" });

        var nombre = request.Nombre.Trim();
        if (await _context.JuzgadosCatalogo.AnyAsync(j => j.SedeId == request.SedeId && j.Nombre == nombre))
            return BadRequest(new { mensaje = "Ya existe un juzgado con ese nombre en esa sede" });

        var juzgado = new JuzgadoCatalogo { Nombre = nombre, SedeId = request.SedeId };
        _context.JuzgadosCatalogo.Add(juzgado);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { sedeId = juzgado.SedeId }, new JuzgadoCatalogoResponse
        {
            Id = juzgado.Id,
            Nombre = juzgado.Nombre,
            SedeId = juzgado.SedeId,
            SedeNombre = sede.Nombre
        });
    }
}
