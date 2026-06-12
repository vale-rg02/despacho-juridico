using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DespachoJuridico.API.Controllers;

[ApiController]
[Route("api/prueba-fechas")]
[Authorize]
public class PruebaFechasController : ControllerBase
{
    private readonly ICalculadorFechasService _calculador;

    public PruebaFechasController(ICalculadorFechasService calculador)
    {
        _calculador = calculador;
    }

    // GET /api/prueba-fechas?fecha=2026-06-11&dias=5&habiles=true
    [HttpGet]
    public IActionResult Calcular([FromQuery] DateTime fecha, [FromQuery] int dias, [FromQuery] bool habiles = true)
    {
        var resultado = habiles
            ? _calculador.SumarDiasHabiles(fecha, dias)
            : _calculador.SumarDiasNaturales(fecha, dias);

        return Ok(new
        {
            fechaInicio = fecha.ToString("yyyy-MM-dd dddd"),
            diasSumados = dias,
            tipoCalculo = habiles ? "Días hábiles" : "Días naturales",
            fechaResultado = resultado.ToString("yyyy-MM-dd dddd")
        });
    }
}