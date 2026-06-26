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

    public EtapasCatalogoController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/etapascatalogo?tipoJuicio=Civil
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? tipoJuicio)
    {
        var query = _context.EtapasCatalogo.AsQueryable();

        if (!string.IsNullOrWhiteSpace(tipoJuicio))
        {
            query = query.Where(e => e.TipoJuicio == tipoJuicio);
        }

        var etapas = await query
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
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EtapaCatalogoCreateRequest request)
    {
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