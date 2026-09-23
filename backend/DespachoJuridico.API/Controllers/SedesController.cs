using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Controllers;

// DJ-112: catálogo de Sede (municipio). A diferencia de EtapasCatalogoController
// (donde el POST está deshabilitado en producción, ver su comentario), aquí sí
// se habilita un flujo real de autogestión para admin -- Sede/Juzgado es un
// catálogo plano (Nombre + Sede), sin las dependencias jerárquicas complejas
// que hicieron riesgoso habilitar eso para EtapasCatalogo (ver
// docs/auditoria-dj72.md).
[ApiController]
[Route("api/sedes")]
[Authorize]
public class SedesController : ControllerBase
{
    private readonly AppDbContext _context;

    public SedesController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/sedes — cualquier usuario autenticado (lo necesita el combobox
    // del formulario de expediente).
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var sedes = await _context.SedesCatalogo
            .OrderBy(s => s.Nombre)
            .Select(s => new SedeCatalogoResponse { Id = s.Id, Nombre = s.Nombre })
            .ToListAsync();

        return Ok(sedes);
    }

    // POST /api/sedes — solo admin (DJ-112: "Agregar municipio nuevo al
    // catálogo: solo usuarios admin").
    [HttpPost]
    [Authorize(Policy = "AccesoAdmin")]
    public async Task<IActionResult> Create([FromBody] CrearSedeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var nombre = request.Nombre.Trim();
        if (await _context.SedesCatalogo.AnyAsync(s => s.Nombre == nombre))
            return BadRequest(new { mensaje = "Ya existe una sede con ese nombre" });

        var sede = new SedeCatalogo { Nombre = nombre };
        _context.SedesCatalogo.Add(sede);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new SedeCatalogoResponse { Id = sede.Id, Nombre = sede.Nombre });
    }
}
