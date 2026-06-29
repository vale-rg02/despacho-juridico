using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsuariosController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/usuarios
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var usuarios = await _context.Usuarios
            .Where(u => u.Activo)
            .OrderBy(u => u.Nombre)
            .Select(u => new UsuarioResponse
            {
                Id = u.Id,
                Nombre = u.Nombre,
                Email = u.Email,
                Rol = u.Rol.ToString()
            })
            .ToListAsync();

        return Ok(usuarios);
    }

    // PUT /api/usuarios/{id}/password
    [HttpPut("{id}/password")]
    public async Task<IActionResult> CambiarPassword(int id, [FromBody] CambiarPasswordRequest request)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return NotFound(new { mensaje = "Usuario no encontrado" });

        if (request.NuevaPassword != request.ConfirmarPassword)
            return BadRequest(new { mensaje = "Las contraseñas no coinciden" });

        if (request.NuevaPassword.Length < 6)
            return BadRequest(new { mensaje = "La contraseña debe tener al menos 6 caracteres" });

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NuevaPassword);
        await _context.SaveChangesAsync();

        return Ok(new { mensaje = "Contraseña actualizada correctamente" });
    }

    // GET /api/usuarios/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return NotFound(new { mensaje = "Usuario no encontrado" });

        return Ok(new UsuarioResponse
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Email = usuario.Email,
            Rol = usuario.Rol.ToString(),
            Activo = usuario.Activo
        });
    }

    // POST /api/usuarios
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearUsuarioRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existe = await _context.Usuarios.AnyAsync(u => u.Email == request.Email);
        if (existe)
            return BadRequest(new { mensaje = "Ya existe un usuario con ese correo" });

        var usuario = new Usuario
        {
            Nombre = request.Nombre,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Rol = request.Rol,
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return Ok(new UsuarioResponse
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Email = usuario.Email,
            Rol = usuario.Rol.ToString(),
            Activo = usuario.Activo
        });
    }

    // PUT /api/usuarios/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] EditarUsuarioRequest request)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return NotFound(new { mensaje = "Usuario no encontrado" });

        usuario.Nombre = request.Nombre;
        usuario.Email = request.Email;
        usuario.Rol = request.Rol;
        await _context.SaveChangesAsync();

        return Ok(new UsuarioResponse
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Email = usuario.Email,
            Rol = usuario.Rol.ToString(),
            Activo = usuario.Activo
        });
    }

    // PATCH /api/usuarios/{id}/activo
    [HttpPatch("{id}/activo")]
    public async Task<IActionResult> CambiarActivo(int id, [FromBody] CambiarActivoRequest request)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return NotFound(new { mensaje = "Usuario no encontrado" });

        usuario.Activo = request.Activo;
        await _context.SaveChangesAsync();

        return Ok(new { mensaje = $"Usuario {(request.Activo ? "activado" : "desactivado")}" });
    }

}