using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/etapas-catalogo")]
[Authorize]
public class EtapasCatalogoController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public EtapasCatalogoController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    // GET /api/etapascatalogo?tipoJuicio=Civil
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? tipoJuicio)
    {
        // Sin tipoJuicio no hay catálogo que mostrar. Antes esto no aplicaba filtro
        // y regresaba las etapas de TODOS los tipos de juicio mezcladas (DJ-75):
        // expedientes sin tipo de juicio capturado (campo opcional; frecuente en los
        // de prueba de cuentas de soporte, que suelen crearse rápido sin llenarlo)
        // mostraban duplicados los pasos que comparten nombre entre catálogos
        // (Amparo, Sentencia, Demanda, Radicación, Contestación...).
        if (string.IsNullOrWhiteSpace(tipoJuicio))
            return Ok(new List<EtapaCatalogoResponse>());

        var etapas = await _context.EtapasCatalogo
            .Where(e => e.TipoJuicio == tipoJuicio)
            .OrderBy(e => e.Orden)
            .Select(e => new EtapaCatalogoResponse
            {
                Id = e.Id,
                Nombre = e.Nombre,
                TipoJuicio = e.TipoJuicio,
                TerminoDias = e.TerminoDias,
                EsDiasHabiles = e.EsDiasHabiles,
                Orden = e.Orden
            })
            .ToListAsync();

        return Ok(etapas);
    }

    // POST /api/etapas-catalogo
    // Solo funciona en Development — en producción está deshabilitado (mismo
    // candado que MigracionController). El catálogo real vive hardcodeado en
    // Data/DbSeeder.cs; nada en el sistema espera que aparezcan entradas nuevas
    // por esta vía, así que una llamada directa (Postman, script) puede dejar
    // el catálogo en un estado que ningún flujo del despacho contempla —
    // ver docs/auditoria-dj72.md.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EtapaCatalogoCreateRequest request)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var etapa = new EtapaCatalogo
        {
            Nombre = request.Nombre,
            TipoJuicio = request.TipoJuicio,
            TerminoDias = request.TerminoDias,
            EsDiasHabiles = request.EsDiasHabiles,
            Orden = request.Orden
        };

        _context.EtapasCatalogo.Add(etapa);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { tipoJuicio = etapa.TipoJuicio }, new EtapaCatalogoResponse
        {
            Id = etapa.Id,
            Nombre = etapa.Nombre,
            TipoJuicio = etapa.TipoJuicio,
            TerminoDias = etapa.TerminoDias,
            EsDiasHabiles = etapa.EsDiasHabiles,
            Orden = etapa.Orden
        });
    }

}