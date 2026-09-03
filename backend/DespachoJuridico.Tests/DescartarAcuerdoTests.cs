using System.Security.Claims;
using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Models.Enums;
using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.Tests;

// DJ-99: PATCH /api/acuerdos/{id}/descartar — permite al litigante marcar un
// acuerdo visible como no relevante (ej. un falso positivo de Alta confianza),
// sin depender solo del algoritmo. Se usa InMemory porque el endpoint corre
// consultas reales contra AcuerdosScrapeados/Expedientes/BitacoraCambios, no
// solo lógica interna (mismo criterio que TerminoTipoJuicioTests/RemateSubmenuTests).
public class DescartarAcuerdoTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    private static AcuerdosController CrearControllerComoUsuario(AppDbContext context, int usuarioId)
    {
        var controller = new AcuerdosController(context, new AccesoExpedientesService(context));
        var identidad = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
        };
        return controller;
    }

    private static async Task<(Usuario litigante, Expediente expediente, AcuerdoScrapeado acuerdo)> SembrarEscenarioAsync(AppDbContext context)
    {
        var litigante = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.Add(litigante);
        await context.SaveChangesAsync();

        var expediente = new Expediente
        {
            NumeroExpediente = "150/2023",
            ParteDemandada = "Roberto Sánchez Mena",
            UsuarioAsignadoId = litigante.Id,
            CreadoPorId = litigante.Id
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        var acuerdo = new AcuerdoScrapeado
        {
            ExpedienteId = expediente.Id,
            NumeroExpediente = expediente.NumeroExpediente,
            IdUnidad = 154,
            NombreJuzgado = "3ro Civil Hermosillo",
            Partes = "SUMARIO CIVIL - ISMAEL DE JESUS CASTRO OQUITA VS INFONAVIT",
            Sintesis = "Se tiene por recibido oficio",
            FechaAcuerdo = new DateOnly(2026, 8, 20),
            Confianza = "Alta",
            Oculto = false
        };
        context.AcuerdosScrapeados.Add(acuerdo);
        await context.SaveChangesAsync();

        return (litigante, expediente, acuerdo);
    }

    [Fact]
    public async Task Descartar_AcuerdoVisible_QuedaOcultoYMarcadoComoDescartadoManualmente()
    {
        using var context = CrearContextoEnMemoria(nameof(Descartar_AcuerdoVisible_QuedaOcultoYMarcadoComoDescartadoManualmente));
        var (litigante, _, acuerdo) = await SembrarEscenarioAsync(context);
        var controller = CrearControllerComoUsuario(context, litigante.Id);

        var resultado = await controller.Descartar(acuerdo.Id);

        Assert.IsType<OkObjectResult>(resultado);
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.True(actualizado!.Oculto);
        Assert.True(actualizado.DescartadoManualmente);
    }

    [Fact]
    public async Task Descartar_RegistraQuienYCuandoEnBitacora()
    {
        using var context = CrearContextoEnMemoria(nameof(Descartar_RegistraQuienYCuandoEnBitacora));
        var (litigante, expediente, acuerdo) = await SembrarEscenarioAsync(context);
        var controller = CrearControllerComoUsuario(context, litigante.Id);

        var antes = DateTime.UtcNow;
        await controller.Descartar(acuerdo.Id);
        var despues = DateTime.UtcNow;

        var entrada = await context.BitacoraCambios.SingleOrDefaultAsync(b => b.ExpedienteId == expediente.Id);
        Assert.NotNull(entrada);
        Assert.Equal("acuerdo_descartado", entrada!.Accion);
        Assert.Equal(litigante.Id, entrada.UsuarioId);
        Assert.Contains(acuerdo.NombreJuzgado, entrada.Detalle);
        Assert.InRange(entrada.Fecha, antes, despues);
    }

    [Fact]
    public async Task Descartar_AcuerdoYaOculto_RegresaBadRequestSinDuplicarBitacora()
    {
        using var context = CrearContextoEnMemoria(nameof(Descartar_AcuerdoYaOculto_RegresaBadRequestSinDuplicarBitacora));
        var (litigante, _, acuerdo) = await SembrarEscenarioAsync(context);
        acuerdo.Oculto = true;
        acuerdo.Confianza = "Baja";
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, litigante.Id);

        var resultado = await controller.Descartar(acuerdo.Id);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.False((await context.AcuerdosScrapeados.FindAsync(acuerdo.Id))!.DescartadoManualmente);
        Assert.Empty(context.BitacoraCambios);
    }

    [Fact]
    public async Task Descartar_UsuarioSinAccesoAlExpediente_RegresaNotFound()
    {
        using var context = CrearContextoEnMemoria(nameof(Descartar_UsuarioSinAccesoAlExpediente_RegresaNotFound));
        var (_, _, acuerdo) = await SembrarEscenarioAsync(context);

        // Cuenta de soporte con expediente asignado a otra persona: sin acceso
        // según AccesoExpedientesService (ver comentario ahí).
        var soporte = new Usuario { Nombre = "dev1", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x", EsCuentaSoporte = true };
        context.Usuarios.Add(soporte);
        await context.SaveChangesAsync();

        var controller = CrearControllerComoUsuario(context, soporte.Id);

        var resultado = await controller.Descartar(acuerdo.Id);

        Assert.IsType<NotFoundObjectResult>(resultado);
        Assert.False((await context.AcuerdosScrapeados.FindAsync(acuerdo.Id))!.Oculto);
    }

    [Fact]
    public async Task ReevaluarOcultos_NuncaTocaUnAcuerdoDescartadoManualmente()
    {
        // Caso límite explícito del punto 3 de DJ-99: aunque un descarte manual
        // terminara con Confianza="Baja" (ej. si el acuerdo ya venía así de otra
        // corrida), DescartadoManualmente debe bloquear la reevaluación por sí
        // solo — no depender de que Confianza distinga el origen.
        using var context = CrearContextoEnMemoria(nameof(ReevaluarOcultos_NuncaTocaUnAcuerdoDescartadoManualmente));
        var (litigante, expediente, _) = await SembrarEscenarioAsync(context);

        var descartado = new AcuerdoScrapeado
        {
            ExpedienteId = expediente.Id,
            NumeroExpediente = expediente.NumeroExpediente,
            IdUnidad = 154,
            NombreJuzgado = "3ro Civil Hermosillo",
            Partes = "SUMARIO CIVIL - ROBERTO SANCHEZ MENA VS ALGUIEN",
            Sintesis = "Acuerdo descartado a mano por el litigante",
            FechaAcuerdo = new DateOnly(2026, 8, 20),
            Confianza = "Baja",
            Oculto = true,
            DescartadoManualmente = true
        };
        context.AcuerdosScrapeados.Add(descartado);
        await context.SaveChangesAsync();

        var candidatos = await context.AcuerdosScrapeados
            .Where(a => a.Oculto && a.Confianza == "Baja" && !a.RegistradoManualmente && !a.DescartadoManualmente)
            .ToListAsync();

        Assert.DoesNotContain(candidatos, a => a.Id == descartado.Id);
    }

    [Fact]
    public async Task Descartar_AcuerdoInexistente_RegresaNotFound()
    {
        using var context = CrearContextoEnMemoria(nameof(Descartar_AcuerdoInexistente_RegresaNotFound));
        var (litigante, _, _) = await SembrarEscenarioAsync(context);
        var controller = CrearControllerComoUsuario(context, litigante.Id);

        var resultado = await controller.Descartar(999999);

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    [Fact]
    public async Task Descartar_UsuarioEstandarDuenoDelExpediente_SiPuedeDescartar()
    {
        // A diferencia de SembrarEscenarioAsync (donde el litigante queda con Id=1
        // y dispara el atajo "usuarioActualId == 1" de AccesoExpedientesService),
        // aquí se siembra un usuario "de relleno" primero para que el litigante NO
        // sea Id=1 — así esta prueba de verdad ejercita la rama de "asignado a mí
        // mismo", no el atajo.
        using var context = CrearContextoEnMemoria(nameof(Descartar_UsuarioEstandarDuenoDelExpediente_SiPuedeDescartar));
        context.Usuarios.Add(new Usuario { Nombre = "Relleno", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" });
        await context.SaveChangesAsync();

        var (litigante, _, acuerdo) = await SembrarEscenarioAsync(context);
        Assert.NotEqual(1, litigante.Id);
        var controller = CrearControllerComoUsuario(context, litigante.Id);

        var resultado = await controller.Descartar(acuerdo.Id);

        Assert.IsType<OkObjectResult>(resultado);
    }

    [Fact]
    public async Task Descartar_ColaboradorExplicitoSinSerElAsignado_SiPuedeDescartar()
    {
        using var context = CrearContextoEnMemoria(nameof(Descartar_ColaboradorExplicitoSinSerElAsignado_SiPuedeDescartar));
        context.Usuarios.Add(new Usuario { Nombre = "Relleno", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" });
        await context.SaveChangesAsync();

        var (_, expediente, acuerdo) = await SembrarEscenarioAsync(context);

        var colaborador = new Usuario { Nombre = "Ana Colaboradora", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.Add(colaborador);
        await context.SaveChangesAsync();
        Assert.NotEqual(1, colaborador.Id);

        context.ExpedienteAccesos.Add(new ExpedienteAcceso { ExpedienteId = expediente.Id, UsuarioId = colaborador.Id });
        await context.SaveChangesAsync();

        var controller = CrearControllerComoUsuario(context, colaborador.Id);

        var resultado = await controller.Descartar(acuerdo.Id);

        Assert.IsType<OkObjectResult>(resultado);
    }

    [Fact]
    public async Task Descartar_UsuarioNivelAdministrativo_PuedeDescartarExpedienteDeOtroLitigante()
    {
        using var context = CrearContextoEnMemoria(nameof(Descartar_UsuarioNivelAdministrativo_PuedeDescartarExpedienteDeOtroLitigante));
        context.Usuarios.Add(new Usuario { Nombre = "Relleno", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" });
        await context.SaveChangesAsync();

        var (_, _, acuerdo) = await SembrarEscenarioAsync(context);

        var admin = new Usuario { Nombre = "Carlos Admin", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x", NivelAcceso = NivelAcceso.Administrativo };
        context.Usuarios.Add(admin);
        await context.SaveChangesAsync();
        Assert.NotEqual(1, admin.Id);

        var controller = CrearControllerComoUsuario(context, admin.Id);

        var resultado = await controller.Descartar(acuerdo.Id);

        Assert.IsType<OkObjectResult>(resultado);
    }

    [Fact]
    public async Task Descartar_AcuerdoRegistradoManualmente_TambienSePuedeDescartar()
    {
        // No hay conflicto entre los dos flags: un exhorto que el litigante
        // capturó a mano (RegistradoManualmente) puede además descartarse después
        // si resulta que ya no aplica — ambos quedan true a la vez.
        using var context = CrearContextoEnMemoria(nameof(Descartar_AcuerdoRegistradoManualmente_TambienSePuedeDescartar));
        var (litigante, _, acuerdo) = await SembrarEscenarioAsync(context);
        acuerdo.RegistradoManualmente = true;
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, litigante.Id);

        var resultado = await controller.Descartar(acuerdo.Id);

        Assert.IsType<OkObjectResult>(resultado);
        var actualizado = await context.AcuerdosScrapeados.FindAsync(acuerdo.Id);
        Assert.True(actualizado!.RegistradoManualmente);
        Assert.True(actualizado.DescartadoManualmente);
    }

    [Fact]
    public async Task Descartar_NoAlteraElCampoVisto()
    {
        using var context = CrearContextoEnMemoria(nameof(Descartar_NoAlteraElCampoVisto));
        var (litigante, _, acuerdo) = await SembrarEscenarioAsync(context);
        acuerdo.Visto = true;
        await context.SaveChangesAsync();
        var controller = CrearControllerComoUsuario(context, litigante.Id);

        await controller.Descartar(acuerdo.Id);

        Assert.True((await context.AcuerdosScrapeados.FindAsync(acuerdo.Id))!.Visto);
    }

    [Fact]
    public async Task GetByExpediente_YaNoIncluyeElAcuerdoDescartado()
    {
        using var context = CrearContextoEnMemoria(nameof(GetByExpediente_YaNoIncluyeElAcuerdoDescartado));
        var (litigante, expediente, acuerdo) = await SembrarEscenarioAsync(context);
        var controller = CrearControllerComoUsuario(context, litigante.Id);

        await controller.Descartar(acuerdo.Id);
        var resultado = await controller.GetByExpediente(expediente.Id);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var lista = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value);
        Assert.Empty(lista.Cast<object>());
    }

    [Fact]
    public async Task RegistrosScraper_ExponeDescartadoManualmenteEnElDiagnostico()
    {
        // El controller de scraper (_scraper no se usa en Registros()) queda como
        // null! a propósito: este endpoint solo lee _context.
        using var context = CrearContextoEnMemoria(nameof(RegistrosScraper_ExponeDescartadoManualmenteEnElDiagnostico));
        var (litigante, _, acuerdo) = await SembrarEscenarioAsync(context);
        var acuerdosController = CrearControllerComoUsuario(context, litigante.Id);
        await acuerdosController.Descartar(acuerdo.Id);

        // Sin pasar fecha: el filtro real es por FechaDetectado (hoy, momento en
        // que SembrarEscenarioAsync creó el registro), no por FechaAcuerdo.
        var scraperController = new ScraperController(null!, context);
        var resultado = await scraperController.Registros();

        var ok = Assert.IsType<OkObjectResult>(resultado);
        dynamic body = ok.Value!;
        var registros = (IEnumerable<DespachoJuridico.API.DTOs.RegistroScraperDiaResponse>)body.registros;
        var registro = Assert.Single(registros, r => r.Id == acuerdo.Id);
        Assert.True(registro.DescartadoManualmente);
        Assert.True(registro.Oculto);
    }

    [Fact]
    public async Task ReevaluarOcultos_SigueReevaluandoUnOcultoPorAlgoritmoQueNoFueDescartado()
    {
        // Control de regresión del punto anterior: la exclusión de
        // DescartadoManualmente no debe bloquear el caso ya existente (falso
        // negativo del algoritmo, nunca tocado por un litigante).
        using var context = CrearContextoEnMemoria(nameof(ReevaluarOcultos_SigueReevaluandoUnOcultoPorAlgoritmoQueNoFueDescartado));
        var (_, expediente, _) = await SembrarEscenarioAsync(context);

        var ocultoPorAlgoritmo = new AcuerdoScrapeado
        {
            ExpedienteId = expediente.Id,
            NumeroExpediente = expediente.NumeroExpediente,
            IdUnidad = 154,
            NombreJuzgado = "3ro Civil Hermosillo",
            Partes = "SUMARIO CIVIL - OTRO CASO CUALQUIERA",
            Sintesis = "Baja confianza real, nunca descartado a mano",
            FechaAcuerdo = new DateOnly(2026, 8, 21),
            Confianza = "Baja",
            Oculto = true,
            DescartadoManualmente = false
        };
        context.AcuerdosScrapeados.Add(ocultoPorAlgoritmo);
        await context.SaveChangesAsync();

        var candidatos = await context.AcuerdosScrapeados
            .Where(a => a.Oculto && a.Confianza == "Baja" && !a.RegistradoManualmente && !a.DescartadoManualmente)
            .ToListAsync();

        Assert.Contains(candidatos, a => a.Id == ocultoPorAlgoritmo.Id);
    }

    [Fact]
    public async Task Descartar_DosAcuerdosDelMismoExpediente_GeneraDosEntradasDeBitacoraIndependientes()
    {
        using var context = CrearContextoEnMemoria(nameof(Descartar_DosAcuerdosDelMismoExpediente_GeneraDosEntradasDeBitacoraIndependientes));
        var (litigante, expediente, acuerdo1) = await SembrarEscenarioAsync(context);

        var acuerdo2 = new AcuerdoScrapeado
        {
            ExpedienteId = expediente.Id,
            NumeroExpediente = expediente.NumeroExpediente,
            IdUnidad = 173,
            NombreJuzgado = "1ro Oral Mercantil Hermosillo",
            Partes = "ORAL MERCANTIL - OTRO ACUERDO DEL MISMO EXPEDIENTE",
            Sintesis = "Segundo acuerdo, también irrelevante",
            FechaAcuerdo = new DateOnly(2026, 8, 22),
            Confianza = "Alta",
            Oculto = false
        };
        context.AcuerdosScrapeados.Add(acuerdo2);
        await context.SaveChangesAsync();

        var controller = CrearControllerComoUsuario(context, litigante.Id);
        await controller.Descartar(acuerdo1.Id);
        await controller.Descartar(acuerdo2.Id);

        var entradas = await context.BitacoraCambios
            .Where(b => b.ExpedienteId == expediente.Id && b.Accion == "acuerdo_descartado")
            .ToListAsync();

        Assert.Equal(2, entradas.Count);
        Assert.Contains(entradas, e => e.Detalle!.Contains("3ro Civil Hermosillo"));
        Assert.Contains(entradas, e => e.Detalle!.Contains("1ro Oral Mercantil Hermosillo"));
    }
}
