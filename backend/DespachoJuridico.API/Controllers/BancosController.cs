using Microsoft.AspNetCore.Authorization;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BancosController : ControllerBase
{
    private readonly AppDbContext _context;

    public BancosController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/bancos
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var bancos = await _context.Bancos
            .OrderBy(b => b.Nombre)
            .Select(b => new BancoResponse
            {
                Id = b.Id,
                Nombre = b.Nombre,
                Direccion = b.Direccion,
                Telefono = b.Telefono
            })
            .ToListAsync();

        return Ok(bancos);
    }

    // POST /api/bancos — solo admin (DJ-105, mismo criterio que
    // SedesController/JuzgadosController: agregar catálogo nuevo = solo admin).
    [HttpPost]
    [Authorize(Policy = "AccesoAdmin")]
    public async Task<IActionResult> Create([FromBody] CrearBancoRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var nombre = request.Nombre.Trim();
        if (await _context.Bancos.AnyAsync(b => b.Nombre == nombre))
            return BadRequest(new { mensaje = "Ya existe un banco con ese nombre" });

        var banco = new Banco { Nombre = nombre };
        _context.Bancos.Add(banco);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new BancoResponse { Id = banco.Id, Nombre = banco.Nombre });
    }
}