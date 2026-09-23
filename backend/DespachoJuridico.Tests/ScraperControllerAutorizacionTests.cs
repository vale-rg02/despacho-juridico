using System.Reflection;
using DespachoJuridico.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-102: ScraperController pasó de [Authorize(Policy = "AccesoAdmin")] a nivel
// de clase a [Authorize] (cualquier usuario autenticado) + el atributo admin
// repetido en cada endpoint que debía seguir restringido -- el cambio de mayor
// riesgo del ticket (un endpoint admin abierto por accidente a cualquier
// litigante). Verificado por reflexión en vez de un test de integración real
// (no hay infraestructura de WebApplicationFactory en este proyecto todavía) --
// evalúa exactamente los atributos que ASP.NET Core usa para autorizar.
public class ScraperControllerAutorizacionTests
{
    private const string AccesoAdmin = "AccesoAdmin";

    [Theory]
    [InlineData(nameof(ScraperController.Registros))]
    [InlineData(nameof(ScraperController.Ejecutar))]
    [InlineData(nameof(ScraperController.EjecutarRango))]
    [InlineData(nameof(ScraperController.ReevaluarOcultos))]
    public void EndpointsAdminExistentes_SiguenRequiriendoAccesoAdmin(string nombreMetodo)
    {
        var metodo = ObtenerMetodoPublico(nombreMetodo);

        var authorize = metodo.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(AccesoAdmin, authorize!.Policy);
    }

    [Theory]
    [InlineData(nameof(ScraperController.ActualizarMisExpedientes))]
    [InlineData(nameof(ScraperController.MiEstadoActualizacion))]
    public void EndpointsNuevosDeLitigante_NoRequierenAccesoAdmin(string nombreMetodo)
    {
        var metodo = ObtenerMetodoPublico(nombreMetodo);

        var authorize = metodo.GetCustomAttribute<AuthorizeAttribute>();

        // Sin [Authorize] propio -- heredan solo el [Authorize] de clase (cualquier
        // usuario autenticado, sin política admin).
        Assert.Null(authorize);
    }

    [Fact]
    public void LaClase_YaNoRequiereAccesoAdminPorDefecto()
    {
        var authorize = typeof(ScraperController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Null(authorize!.Policy); // [Authorize] simple, no la política admin
    }

    private static MethodInfo ObtenerMetodoPublico(string nombre) =>
        typeof(ScraperController).GetMethod(nombre, BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"No se encontró el método público {nombre} en ScraperController");
}
