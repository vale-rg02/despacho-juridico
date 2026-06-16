using DespachoJuridico.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MigracionController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public MigracionController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    // POST /api/migracion/excel
    // Solo funciona en Development — en producción está deshabilitado
    [HttpPost("excel")]
    public async Task<IActionResult> ImportarExcel(IFormFile archivo, [FromQuery] bool soloAbiertos = true)
    {
        // Doble seguridad: solo en entorno de desarrollo
        if (!_env.IsDevelopment())
            return NotFound();

        if (archivo == null || archivo.Length == 0)
            return BadRequest(new { mensaje = "Adjunta el archivo Excel" });

        if (!archivo.FileName.EndsWith(".xlsx") && !archivo.FileName.EndsWith(".xls"))
            return BadRequest(new { mensaje = "El archivo debe ser .xlsx o .xls" });

        var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        // Guardar temporalmente
        var rutaTmp = Path.GetTempFileName() + Path.GetExtension(archivo.FileName);
        await using (var stream = new FileStream(rutaTmp, FileMode.Create))
        {
            await archivo.CopyToAsync(stream);
        }

        try
        {
            var resultado = await MigracionExcel.ImportarAsync(
                _context, rutaTmp, usuarioId, soloAbiertos);

            return Ok(new
            {
                importados = resultado.Importados,
                duplicados = resultado.Duplicados,
                saltados = resultado.FilasSaltadas,
                errores = resultado.Errores
            });
        }
        finally
        {
            if (File.Exists(rutaTmp))
                File.Delete(rutaTmp);
        }
    }
}