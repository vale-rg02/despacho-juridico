using System.Security.Claims;
using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DespachoJuridico.Tests;

// La parte actora de un expediente es o un banco del catálogo (BancoId) o el
// nombre libre de una persona en un "asunto particular" (ParteActoraParticular)
// -- nunca ambos a la vez. ResolverParteActora es la fuente de verdad de esa
// regla (ExpedientesController), ejercida aquí tanto en aislado como a través
// de Create/Update reales contra InMemory.
public class ParteActoraParticularTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    private static ExpedientesController CrearControllerComoUsuario(AppDbContext context, int usuarioId)
    {
        var controller = new ExpedientesController(
            context, new CalculadorFechasService(), new FakeEmailService(),
            NullLogger<ExpedientesController>.Instance, new AccesoExpedientesService(context));
        var identidad = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
        };
        return controller;
    }

    [Theory]
    [InlineData(5, "Juan Pérez", 5, null)]
    [InlineData(null, "Juan Pérez", null, "Juan Pérez")]
    [InlineData(null, "  Juan Pérez  ", null, "Juan Pérez")]
    [InlineData(null, "   ", null, null)]
    [InlineData(null, null, null, null)]
    public void ResolverParteActora_BancoIdGanaSobreTextoLibre(int? bancoId, string? texto, int? bancoIdEsperado, string? textoEsperado)
    {
        var (bancoResuelto, textoResuelto) = ExpedientesController.ResolverParteActora(bancoId, texto);

        Assert.Equal(bancoIdEsperado, bancoResuelto);
        Assert.Equal(textoEsperado, textoResuelto);
    }

    [Fact]
    public async Task Create_ConParteActoraParticular_SeGuardaSinBanco()
    {
        using var context = CrearContextoEnMemoria(nameof(Create_ConParteActoraParticular_SeGuardaSinBanco));
        var controller = CrearControllerComoUsuario(context, 1);

        var resultado = await controller.Create(new ExpedienteCreateRequest
        {
            NumeroExpediente = "500/2026",
            ParteDemandada = "Banco X",
            ParteActoraParticular = "María López"
        });

        var creado = Assert.IsType<CreatedAtActionResult>(resultado);
        var respuesta = Assert.IsType<ExpedienteResponse>(creado.Value);
        Assert.Null(respuesta.BancoId);
        Assert.Equal("María López", respuesta.ParteActoraParticular);

        var enBd = await context.Expedientes.SingleAsync();
        Assert.Null(enBd.BancoId);
        Assert.Equal("María López", enBd.ParteActoraParticular);
    }

    [Fact]
    public async Task Create_ConBancoIdYParteActoraParticularALaVez_SoloSeQuedaElBanco()
    {
        using var context = CrearContextoEnMemoria(nameof(Create_ConBancoIdYParteActoraParticularALaVez_SoloSeQuedaElBanco));
        var banco = new Banco { Nombre = "BBVA México" };
        context.Bancos.Add(banco);
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, 1);

        var resultado = await controller.Create(new ExpedienteCreateRequest
        {
            NumeroExpediente = "501/2026",
            ParteDemandada = "Juan Pérez",
            BancoId = banco.Id,
            ParteActoraParticular = "Texto que no debería guardarse"
        });

        var creado = Assert.IsType<CreatedAtActionResult>(resultado);
        var respuesta = Assert.IsType<ExpedienteResponse>(creado.Value);
        Assert.Equal(banco.Id, respuesta.BancoId);
        Assert.Null(respuesta.ParteActoraParticular);

        var enBd = await context.Expedientes.SingleAsync();
        Assert.Equal(banco.Id, enBd.BancoId);
        Assert.Null(enBd.ParteActoraParticular);
    }

    [Fact]
    public async Task Update_CambiaDeBancoAParticular_LimpiaElBancoId()
    {
        using var context = CrearContextoEnMemoria(nameof(Update_CambiaDeBancoAParticular_LimpiaElBancoId));
        var banco = new Banco { Nombre = "HSBC" };
        context.Bancos.Add(banco);
        await context.SaveChangesAsync();
        var expediente = new Expediente
        {
            NumeroExpediente = "502/2026", ParteDemandada = "Juan Pérez",
            BancoId = banco.Id, UsuarioAsignadoId = 1, CreadoPorId = 1
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, 1);

        var resultado = await controller.Update(expediente.Id, new ExpedienteUpdateRequest
        {
            NumeroExpediente = expediente.NumeroExpediente,
            ParteDemandada = expediente.ParteDemandada,
            BancoId = null,
            ParteActoraParticular = "Rosa Martínez"
        });

        Assert.IsType<OkObjectResult>(resultado);
        var enBd = await context.Expedientes.SingleAsync();
        Assert.Null(enBd.BancoId);
        Assert.Equal("Rosa Martínez", enBd.ParteActoraParticular);
    }

    [Fact]
    public async Task Update_CambiaDeParticularABanco_LimpiaParteActoraParticular()
    {
        using var context = CrearContextoEnMemoria(nameof(Update_CambiaDeParticularABanco_LimpiaParteActoraParticular));
        var banco = new Banco { Nombre = "Santander" };
        context.Bancos.Add(banco);
        var expediente = new Expediente
        {
            NumeroExpediente = "503/2026", ParteDemandada = "Juan Pérez",
            ParteActoraParticular = "Rosa Martínez", UsuarioAsignadoId = 1, CreadoPorId = 1
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, 1);

        var resultado = await controller.Update(expediente.Id, new ExpedienteUpdateRequest
        {
            NumeroExpediente = expediente.NumeroExpediente,
            ParteDemandada = expediente.ParteDemandada,
            BancoId = banco.Id,
            ParteActoraParticular = "Rosa Martínez"
        });

        Assert.IsType<OkObjectResult>(resultado);
        var enBd = await context.Expedientes.SingleAsync();
        Assert.Equal(banco.Id, enBd.BancoId);
        Assert.Null(enBd.ParteActoraParticular);
    }

    [Fact]
    public async Task Update_RegistraCambioDeParteActoraParticularEnBitacora()
    {
        using var context = CrearContextoEnMemoria(nameof(Update_RegistraCambioDeParteActoraParticularEnBitacora));
        var expediente = new Expediente
        {
            NumeroExpediente = "504/2026", ParteDemandada = "Juan Pérez",
            UsuarioAsignadoId = 1, CreadoPorId = 1
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, 1);

        await controller.Update(expediente.Id, new ExpedienteUpdateRequest
        {
            NumeroExpediente = expediente.NumeroExpediente,
            ParteDemandada = expediente.ParteDemandada,
            ParteActoraParticular = "Rosa Martínez"
        });

        var entrada = await context.BitacoraCambios.SingleAsync(b => b.ExpedienteId == expediente.Id);
        Assert.Contains("Parte actora particular", entrada.Detalle);
    }
}
