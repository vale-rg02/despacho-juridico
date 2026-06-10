using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Buscar usuario por email
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.Activo);

        if (usuario == null)
            return Unauthorized(new { mensaje = "Credenciales incorrectas" });

        // Verificar contraseña
        if (!BCrypt.Net.BCrypt.Verify(request.Password, usuario.PasswordHash))
            return Unauthorized(new { mensaje = "Credenciales incorrectas" });

        // Generar JWT
        var token = GenerarToken(usuario);

        return Ok(new
        {
            token,
            usuario = new
            {
                id = usuario.Id,
                nombre = usuario.Nombre,
                email = usuario.Email,
                rol = usuario.Rol.ToString()
            }
        });
    }
private string GenerarToken(Usuario usuario)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
        new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
        new Claim(ClaimTypes.Email,          usuario.Email),
        new Claim(ClaimTypes.Name,           usuario.Nombre),
        new Claim(ClaimTypes.Role,           usuario.Rol.ToString()),
    };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// DTO de entrada
public record LoginRequest(string Email, string Password);

