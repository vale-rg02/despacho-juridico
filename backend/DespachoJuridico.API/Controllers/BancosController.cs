using Microsoft.AspNetCore.Authorization;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
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
}