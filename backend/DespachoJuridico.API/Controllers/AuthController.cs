using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext context, IConfiguration config, ILogger<AuthController> logger)
    {
        _context = context;
        _config = config;
        _logger = logger;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(request.Password, usuario.PasswordHash))
        {
            _logger.LogWarning("Login fallido para {Email} desde {Ip}", request.Email, ip);
            return Unauthorized(new { mensaje = "Credenciales incorrectas" });
        }

        if (!usuario.Activo)
        {
            _logger.LogWarning("Intento de login en cuenta desactivada {Email} desde {Ip}", request.Email, ip);
            return Unauthorized(new { mensaje = "Tu cuenta está desactivada. Contacta al administrador." });
        }

        _logger.LogInformation("Login exitoso para {Email} (id={UsuarioId}) desde {Ip}", usuario.Email, usuario.Id, ip);

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
                rol = usuario.Rol.ToString(),
                nivelAcceso = usuario.NivelAcceso.ToString()
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
        new Claim("NivelAcceso", usuario.NivelAcceso.ToString())

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

