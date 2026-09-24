using System.Security.Claims;
using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Models.Enums;
using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DespachoJuridico.Tests;

// DJ-108: POST /api/acuerdos/{expedienteId}/manual -- generaliza el registro
// manual (antes hardcodeado a EsExhorto=true, solo exhortos) a cualquier
// acuerdo. Acotado a expedientes propios (titular o colaborador, activos),
// más estricto que el resto de AcuerdosController (que usa TieneAccesoAsync,
// "litigantes trabajan en conjunto") -- aquí se reusa
// ScraperAcuerdosService.AplicarFiltroExpedientesPropios (DJ-102).
public class RegistrarAcuerdoManualTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    private static (AcuerdosController controller, FakeEmailService email) CrearControllerComoUsuario(AppDbContext context, int usuarioId)
    {
        var fakeEmail = new FakeEmailService();
        var controller = new AcuerdosController(
            context, new AccesoExpedientesService(context), fakeEmail,
            new ConfigurationBuilder().Build(), NullLogger<AcuerdosController>.Instance);
        var identidad = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
        };
        return (controller, fakeEmail);
    }

    private static async Task<(Usuario titular, Usuario ajeno, Expediente expediente)> SembrarExpedienteAsync(
        AppDbContext context, EstadoExpediente estado = EstadoExpediente.Abierto)
    {
        var titular = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        var ajeno = new Usuario { Nombre = "Litigante Ajeno", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.AddRange(titular, ajeno);
        await context.SaveChangesAsync();

        var expediente = new Expediente
        {
            NumeroExpediente = "401/2026",
            ParteDemandada = "Juan Pérez",
            UsuarioAsignadoId = titular.Id,
            CreadoPorId = titular.Id,
            Estado = estado
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        return (titular, ajeno, expediente);
    }

    [Fact]
    public async Task AcuerdoNormal_SeRegistraCorrectamente_ConEsExhortoFalse()
    {
        using var context = CrearContextoEnMemoria(nameof(AcuerdoNormal_SeRegistraCorrectamente_ConEsExhortoFalse));
        var (titular, _, expediente) = await SembrarExpedienteAsync(context);
        var (controller, _) = CrearControllerComoUsuario(context, titular.Id);

        var resultado = await controller.RegistrarManual(expediente.Id, new RegistrarAcuerdoManualRequest
        {
            Sintesis = "Se tiene por contestada la demanda",
            FechaAcuerdo = new DateOnly(2026, 9, 24),
            EsExhorto = false,
            TipoAsunto = "Contestación"
        });

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<AcuerdoResponse>(ok.Value);
        Assert.False(respuesta.EsExhorto);
        Assert.Equal("Contestación", respuesta.TipoAsunto);
        Assert.True(respuesta.RegistradoManualmente);

        var guardado = await context.AcuerdosScrapeados.SingleAsync();
        Assert.False(guardado.EsExhorto);
        Assert.Equal("Contestación", guardado.TipoAsunto);
    }

    [Fact]
    public async Task GetByExpediente_ExponeTipoAsuntoDelAcuerdoManualNormal()
    {
        // Regresión encontrada en verificación manual: AcuerdoResponse ganó el
        // campo TipoAsunto, pero el listado (GetByExpediente) tenía su propio
        // Select y no lo proyectaba -- el registro se guardaba bien pero la
        // UI mostraba el badge vacío ("Acuerdo") en vez del tipo capturado.
        using var context = CrearContextoEnMemoria(nameof(GetByExpediente_ExponeTipoAsuntoDelAcuerdoManualNormal));
        var (titular, _, expediente) = await SembrarExpedienteAsync(context);
        var (controller, _) = CrearControllerComoUsuario(context, titular.Id);
        await controller.RegistrarManual(expediente.Id, new RegistrarAcuerdoManualRequest
        {
            Sintesis = "x", FechaAcuerdo = new DateOnly(2026, 9, 24), EsExhorto = false, TipoAsunto = "Contestación"
        });

        var listado = Assert.IsType<OkObjectResult>(await controller.GetByExpediente(expediente.Id));
        var acuerdos = Assert.IsType<List<AcuerdoResponse>>(listado.Value);

        Assert.Equal("Contestación", Assert.Single(acuerdos).TipoAsunto);
    }

    [Fact]
    public async Task AcuerdoNormal_SinTipoAsunto_RegresaBadRequest()
    {
        using var context = CrearContextoEnMemoria(nameof(AcuerdoNormal_SinTipoAsunto_RegresaBadRequest));
        var (titular, _, expediente) = await SembrarExpedienteAsync(context);
        var (controller, _) = CrearControllerComoUsuario(context, titular.Id);

        var resultado = await controller.RegistrarManual(expediente.Id, new RegistrarAcuerdoManualRequest
        {
            Sintesis = "Se tiene por contestada la demanda",
            FechaAcuerdo = new DateOnly(2026, 9, 24),
            EsExhorto = false,
            TipoAsunto = null
        });

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Empty(context.AcuerdosScrapeados);
    }

    [Fact]
    public async Task Exhorto_SigueFuncionandoSinRegresion()
    {
        using var context = CrearContextoEnMemoria(nameof(Exhorto_SigueFuncionandoSinRegresion));
        var (titular, _, expediente) = await SembrarExpedienteAsync(context);
        var (controller, _) = CrearControllerComoUsuario(context, titular.Id);

        var resultado = await controller.RegistrarManual(expediente.Id, new RegistrarAcuerdoManualRequest
        {
            Sintesis = "Se recibe exhorto para diligenciar",
            FechaAcuerdo = new DateOnly(2026, 9, 24),
            EsExhorto = true,
            NombreJuzgado = "1ro Civil Guadalajara",
            CiudadDestino = "Guadalajara, Jalisco"
        });

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<AcuerdoResponse>(ok.Value);
        Assert.True(respuesta.EsExhorto);
        Assert.Equal("Exhorto (manual)", respuesta.TipoAsunto);
        Assert.Equal("Guadalajara, Jalisco", respuesta.CiudadDestino);
    }

    [Fact]
    public async Task RequestSinCampoEsExhortoEnJson_SeSigueComportandoComoExhorto()
    {
        // Simula un frontend viejo (sin desplegar todavía) que no manda
        // "esExhorto" -- el DTO debe defaultear a true, comportamiento
        // idéntico al de antes de este cambio.
        using var context = CrearContextoEnMemoria(nameof(RequestSinCampoEsExhortoEnJson_SeSigueComportandoComoExhorto));
        var (titular, _, expediente) = await SembrarExpedienteAsync(context);
        var (controller, _) = CrearControllerComoUsuario(context, titular.Id);

        var request = System.Text.Json.JsonSerializer.Deserialize<RegistrarAcuerdoManualRequest>(
            """{"sintesis":"Exhorto viejo","fechaAcuerdo":"2026-09-24"}""",
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var resultado = await controller.RegistrarManual(expediente.Id, request);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<AcuerdoResponse>(ok.Value);
        Assert.True(respuesta.EsExhorto);
        Assert.Equal("Exhorto (manual)", respuesta.TipoAsunto);
    }

    [Fact]
    public async Task Titular_PuedeRegistrar()
    {
        using var context = CrearContextoEnMemoria(nameof(Titular_PuedeRegistrar));
        var (titular, _, expediente) = await SembrarExpedienteAsync(context);
        var (controller, _) = CrearControllerComoUsuario(context, titular.Id);

        var resultado = await controller.RegistrarManual(expediente.Id, new RegistrarAcuerdoManualRequest
        {
            Sintesis = "x", FechaAcuerdo = new DateOnly(2026, 9, 24), EsExhorto = true
        });

        Assert.IsType<OkObjectResult>(resultado);
    }

    [Fact]
    public async Task Colaborador_PuedeRegistrar()
    {
        using var context = CrearContextoEnMemoria(nameof(Colaborador_PuedeRegistrar));
        var (_, colaborador, expediente) = await SembrarExpedienteAsync(context);
        context.ExpedienteAccesos.Add(new ExpedienteAcceso { ExpedienteId = expediente.Id, UsuarioId = colaborador.Id });
        await context.SaveChangesAsync();
        var (controller, _) = CrearControllerComoUsuario(context, colaborador.Id);

        var resultado = await controller.RegistrarManual(expediente.Id, new RegistrarAcuerdoManualRequest
        {
            Sintesis = "x", FechaAcuerdo = new DateOnly(2026, 9, 24), EsExhorto = true
        });

        Assert.IsType<OkObjectResult>(resultado);
    }

    [Fact]
    public async Task LitiganteSinRelacionConElExpediente_RegresaNotFound()
    {
        using var context = CrearContextoEnMemoria(nameof(LitiganteSinRelacionConElExpediente_RegresaNotFound));
        var (_, ajeno, expediente) = await SembrarExpedienteAsync(context);
        var (controller, _) = CrearControllerComoUsuario(context, ajeno.Id);

        var resultado = await controller.RegistrarManual(expediente.Id, new RegistrarAcuerdoManualRequest
        {
            Sintesis = "x", FechaAcuerdo = new DateOnly(2026, 9, 24), EsExhorto = true
        });

        Assert.IsType<NotFoundObjectResult>(resultado);
        Assert.Empty(context.AcuerdosScrapeados);
    }

    [Fact]
    public async Task ExpedienteCerrado_RegresaNotFound_AunSiendoTitular()
    {
        using var context = CrearContextoEnMemoria(nameof(ExpedienteCerrado_RegresaNotFound_AunSiendoTitular));
        var (titular, _, expediente) = await SembrarExpedienteAsync(context, EstadoExpediente.Cerrado);
        var (controller, _) = CrearControllerComoUsuario(context, titular.Id);

        var resultado = await controller.RegistrarManual(expediente.Id, new RegistrarAcuerdoManualRequest
        {
            Sintesis = "x", FechaAcuerdo = new DateOnly(2026, 9, 24), EsExhorto = true
        });

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    [Fact]
    public async Task Colaborador_RecibeCorreoDeAviso()
    {
        using var context = CrearContextoEnMemoria(nameof(Colaborador_RecibeCorreoDeAviso));
        var (titular, colaborador, expediente) = await SembrarExpedienteAsync(context);
        context.ExpedienteAccesos.Add(new ExpedienteAcceso { ExpedienteId = expediente.Id, UsuarioId = colaborador.Id });
        await context.SaveChangesAsync();
        var (controller, fakeEmail) = CrearControllerComoUsuario(context, titular.Id);

        await controller.RegistrarManual(expediente.Id, new RegistrarAcuerdoManualRequest
        {
            Sintesis = "x", FechaAcuerdo = new DateOnly(2026, 9, 24), EsExhorto = true
        });

        Assert.Single(fakeEmail.Enviados);
        Assert.Equal(colaborador.Email, fakeEmail.Enviados[0].Email);
    }

    [Fact]
    public async Task QuienRegistra_NuncaSeMandaElCorreoASiMismo()
    {
        using var context = CrearContextoEnMemoria(nameof(QuienRegistra_NuncaSeMandaElCorreoASiMismo));
        var (titular, _, expediente) = await SembrarExpedienteAsync(context);
        var (controller, fakeEmail) = CrearControllerComoUsuario(context, titular.Id);

        await controller.RegistrarManual(expediente.Id, new RegistrarAcuerdoManualRequest
        {
            Sintesis = "x", FechaAcuerdo = new DateOnly(2026, 9, 24), EsExhorto = true
        });

        // El titular es el único con acceso y es quien registró -- nadie más
        // que avisar.
        Assert.Empty(fakeEmail.Enviados);
    }

    [Fact]
    public async Task AcuerdoManualNormal_NuncaApareceEntreCandidatosDeReevaluarOcultos()
    {
        // DJ-108 requisito 3: RegistradoManualmente ya distingue un registro
        // manual de uno del scraper -- ReevaluarOcultosAsync ya lo excluye
        // (ScraperAcuerdosService.cs:762). Este test ejercita el método REAL
        // (no solo el predicado copiado) para blindar explícitamente el caso
        // de un acuerdo NORMAL manual (el existente ya cubría exhortos).
        var nombreBD = nameof(AcuerdoManualNormal_NuncaApareceEntreCandidatosDeReevaluarOcultos);
        using var context = CrearContextoEnMemoria(nombreBD);
        var (titular, _, expediente) = await SembrarExpedienteAsync(context);

        // Simula un acuerdo manual normal que, si el filtro fallara, calificaría
        // como candidato de reevaluación (Oculto + Confianza "Baja").
        var acuerdo = new AcuerdoScrapeado
        {
            ExpedienteId = expediente.Id,
            NumeroExpediente = expediente.NumeroExpediente,
            Sintesis = "Acuerdo manual normal",
            FechaAcuerdo = new DateOnly(2026, 9, 24),
            EsExhorto = false,
            TipoAsunto = "Contestación",
            RegistradoManualmente = true,
            Oculto = true,
            Confianza = "Baja"
        };
        context.AcuerdosScrapeados.Add(acuerdo);
        await context.SaveChangesAsync();

        var servicios = new ServiceCollection();
        servicios.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(nombreBD));
        servicios.AddSingleton<IEmailService>(new FakeEmailService());
        var proveedor = servicios.BuildServiceProvider();
        var scraper = new ScraperAcuerdosService(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ScraperAcuerdosService>.Instance,
            new ConfigurationBuilder().Build());

        var resultado = await scraper.ReevaluarOcultosAsync(dryRun: false);

        Assert.Equal(0, resultado.RegistrosEvaluados);
        Assert.Empty(resultado.RegistrosDesocultados);
        Assert.Empty(resultado.RegistrosSugeridos);

        var sinTocar = await context.AcuerdosScrapeados.SingleAsync(a => a.Id == acuerdo.Id);
        Assert.True(sinTocar.Oculto);
        Assert.Equal("Baja", sinTocar.Confianza);
    }

    // Confirma explícitamente el pedido del usuario: las cuentas de soporte
    // (dev1/dev2) deben poder registrar acuerdos manuales para hacer pruebas,
    // y sus colaboradores (soporte incluido) deben recibir el aviso igual que
    // el litigante principal. AplicarFiltroExpedientesPropios (DJ-102) nunca
    // filtró por EsCuentaSoporte -- solo por titular/colaborador -- así que
    // esto ya funcionaba por construcción; estos tests lo dejan verificado en
    // vez de asumido.

    [Fact]
    public async Task CuentaSoporte_TitularDeSuPropioExpediente_PuedeRegistrar()
    {
        using var context = CrearContextoEnMemoria(nameof(CuentaSoporte_TitularDeSuPropioExpediente_PuedeRegistrar));
        var soporte = new Usuario { Nombre = "dev1", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x", EsCuentaSoporte = true };
        context.Usuarios.Add(soporte);
        await context.SaveChangesAsync();
        var expediente = new Expediente
        {
            NumeroExpediente = "900/2026", ParteDemandada = "Prueba", UsuarioAsignadoId = soporte.Id, CreadoPorId = soporte.Id
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        var (controller, _) = CrearControllerComoUsuario(context, soporte.Id);

        var resultado = await controller.RegistrarManual(expediente.Id, new RegistrarAcuerdoManualRequest
        {
            Sintesis = "x", FechaAcuerdo = new DateOnly(2026, 9, 24), EsExhorto = false, TipoAsunto = "Prueba"
        });

        Assert.IsType<OkObjectResult>(resultado);
    }

    [Fact]
    public async Task CuentaSoporte_ComoColaborador_TambienPuedeRegistrar()
    {
        using var context = CrearContextoEnMemoria(nameof(CuentaSoporte_ComoColaborador_TambienPuedeRegistrar));
        var (titular, _, expediente) = await SembrarExpedienteAsync(context);
        var soporte = new Usuario { Nombre = "dev2", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x", EsCuentaSoporte = true };
        context.Usuarios.Add(soporte);
        await context.SaveChangesAsync();
        context.ExpedienteAccesos.Add(new ExpedienteAcceso { ExpedienteId = expediente.Id, UsuarioId = soporte.Id });
        await context.SaveChangesAsync();
        var (controller, _) = CrearControllerComoUsuario(context, soporte.Id);

        var resultado = await controller.RegistrarManual(expediente.Id, new RegistrarAcuerdoManualRequest
        {
            Sintesis = "x", FechaAcuerdo = new DateOnly(2026, 9, 24), EsExhorto = true
        });

        Assert.IsType<OkObjectResult>(resultado);
    }

    [Fact]
    public async Task CuentaSoporte_ComoColaborador_RecibeNotificacionIgualQueCualquierOtro()
    {
        using var context = CrearContextoEnMemoria(nameof(CuentaSoporte_ComoColaborador_RecibeNotificacionIgualQueCualquierOtro));
        var (titular, _, expediente) = await SembrarExpedienteAsync(context);
        var soporte = new Usuario { Nombre = "dev1", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x", EsCuentaSoporte = true };
        context.Usuarios.Add(soporte);
        await context.SaveChangesAsync();
        context.ExpedienteAccesos.Add(new ExpedienteAcceso { ExpedienteId = expediente.Id, UsuarioId = soporte.Id });
        await context.SaveChangesAsync();
        var (controller, fakeEmail) = CrearControllerComoUsuario(context, titular.Id);

        await controller.RegistrarManual(expediente.Id, new RegistrarAcuerdoManualRequest
        {
            Sintesis = "x", FechaAcuerdo = new DateOnly(2026, 9, 24), EsExhorto = true
        });

        Assert.Single(fakeEmail.Enviados);
        Assert.Equal(soporte.Email, fakeEmail.Enviados[0].Email);
    }
}
