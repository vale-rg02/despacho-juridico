using System.Reflection;
using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.Tests;

// DJ-105: POST /api/bancos -- mismo patrón de catálogo autogestionable que
// SedesController/JuzgadosController (DJ-112/DJ-87): agregar banco nuevo
// restringido a admin, GET abierto a cualquier usuario autenticado.
public class BancosControllerTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    [Fact]
    public void Create_RequiereLaPoliticaAccesoAdmin()
    {
        // Igual que en DJ-102 (ver ScraperControllerAutorizacionTests): [Authorize]
        // en un método de controller no se aplica al llamarlo directamente en un
        // test -- se verifica por reflexión, que es exactamente lo que ASP.NET Core
        // usa para decidir si autoriza la petición.
        var metodo = typeof(BancosController).GetMethod(nameof(BancosController.Create))!;

        var authorize = metodo.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("AccesoAdmin", authorize!.Policy);
    }

    [Fact]
    public async Task Create_BancoNuevo_SeCreaYApareceEnGetAll()
    {
        using var context = CrearContextoEnMemoria(nameof(Create_BancoNuevo_SeCreaYApareceEnGetAll));
        var controller = new BancosController(context);

        var resultado = await controller.Create(new CrearBancoRequest { Nombre = "Banorte" });

        Assert.IsType<CreatedAtActionResult>(resultado);
        var listado = Assert.IsType<OkObjectResult>(await controller.GetAll());
        var bancos = Assert.IsType<List<BancoResponse>>(listado.Value);
        Assert.Contains(bancos, b => b.Nombre == "Banorte");
    }

    [Fact]
    public async Task Create_QuitaEspaciosAlrededorDelNombre()
    {
        using var context = CrearContextoEnMemoria(nameof(Create_QuitaEspaciosAlrededorDelNombre));
        var controller = new BancosController(context);

        await controller.Create(new CrearBancoRequest { Nombre = "  Banorte  " });

        var banco = await context.Bancos.SingleAsync(b => b.Nombre == "Banorte");
        Assert.Equal("Banorte", banco.Nombre);
    }

    [Fact]
    public async Task Create_NombreDuplicado_RegresaBadRequest()
    {
        using var context = CrearContextoEnMemoria(nameof(Create_NombreDuplicado_RegresaBadRequest));
        context.Bancos.Add(new Banco { Nombre = "BBVA México" });
        await context.SaveChangesAsync();
        var controller = new BancosController(context);

        var resultado = await controller.Create(new CrearBancoRequest { Nombre = "BBVA México" });

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(1, await context.Bancos.CountAsync(b => b.Nombre == "BBVA México"));
    }

    [Fact]
    public async Task GetAll_RegresaBancosOrdenadosPorNombre()
    {
        using var context = CrearContextoEnMemoria(nameof(GetAll_RegresaBancosOrdenadosPorNombre));
        context.Bancos.AddRange(
            new Banco { Nombre = "Scotiabank" },
            new Banco { Nombre = "Banco Azteca" });
        await context.SaveChangesAsync();
        var controller = new BancosController(context);

        var resultado = Assert.IsType<OkObjectResult>(await controller.GetAll());
        var bancos = Assert.IsType<List<BancoResponse>>(resultado.Value);

        Assert.Equal(new[] { "Banco Azteca", "Scotiabank" }, bancos.Select(b => b.Nombre));
    }
}
