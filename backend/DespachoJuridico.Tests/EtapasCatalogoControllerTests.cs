using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace DespachoJuridico.Tests;

// Mismo patrón de candado que MigracionController (POST /api/migracion/excel):
// solo funciona en Development. Ver docs/auditoria-dj72.md — el catálogo real
// vive hardcodeado en DbSeeder.cs, así que este endpoint nunca debe poder
// tocar el catálogo de una instancia en producción.
public class EtapasCatalogoControllerTests
{
    private class EntornoFalso : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "DespachoJuridico.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public async Task Create_FueraDeDevelopment_RegresaNotFoundSinTocarLaBD()
    {
        // El contexto nunca se configura con un proveedor real: si el candado no
        // detiene la ejecución antes de _context.SaveChangesAsync(), la prueba
        // truena con una excepción de EF Core en vez de solo fallar el Assert —
        // así queda claro que el bloqueo pasa ANTES de tocar la base de datos.
        var contextSinProveedor = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        var controller = new EtapasCatalogoController(contextSinProveedor, new EntornoFalso { EnvironmentName = "Production" });

        var resultado = await controller.Create(new EtapaCatalogoCreateRequest
        {
            Nombre = "Etapa de prueba",
            TipoJuicio = "Civil",
            Orden = 1
        });

        Assert.IsType<NotFoundResult>(resultado);
    }

    [Fact]
    public async Task Create_EnDevelopment_NoQuedaBloqueadoPorElCandado()
    {
        // Este proyecto de pruebas no tiene un proveedor de EF Core real configurado
        // (solo unit tests sobre métodos internos hasta ahora) — en vez de agregar una
        // dependencia nueva solo para esta prueba, se deja avanzar la ejecución hasta el
        // primer punto que sí necesita un proveedor (SaveChangesAsync). Si el candado
        // bloqueara aquí por error, regresaría NotFoundResult en vez de tronar por falta
        // de proveedor — eso es justo lo que la prueba descarta.
        var contextSinProveedor = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        var controller = new EtapasCatalogoController(contextSinProveedor, new EntornoFalso { EnvironmentName = "Development" });

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => controller.Create(new EtapaCatalogoCreateRequest
        {
            Nombre = "Etapa de prueba",
            TipoJuicio = "Civil",
            Orden = 1
        }));
    }
}
