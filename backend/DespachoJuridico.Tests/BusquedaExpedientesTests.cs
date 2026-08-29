using DespachoJuridico.API.Controllers;
using DespachoJuridico.API.Data;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.Tests;

// DJ-110: la búsqueda por ParteDemandada/NumeroExpediente era sensible a
// mayúsculas ("Sue" no encontraba "SUE"), y de paso se agregó normalizar acentos
// ("Mexico" ahora encuentra "México"). El fix usa EF.Functions.ILike + la función
// unaccent() de Postgres, que el servidor resuelve — no son métodos de C# con
// lógica propia, así que no se pueden probar ejecutándolos directo (lanzan
// NotSupportedException fuera de una consulta traducida por EF Core) ni con el
// proveedor InMemory (ninguno de los dos existe ahí, son específicos de Npgsql/
// Postgres). Por eso estas pruebas verifican el SQL que EF Core genera —con un
// connection string que nunca se conecta a nada, ToQueryString() solo traduce la
// consulta, no la ejecuta— en vez de comparar resultados reales contra una base
// de datos. El comportamiento real ya se verificó en vivo contra la BD.
public class BusquedaExpedientesTests
{
    private static AppDbContext CrearContextoSinConectar()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=no_se_usa;Username=x;Password=x")
            .Options;
        return new AppDbContext(opciones);
    }

    [Fact]
    public void AplicarFiltroBusqueda_GeneraIlikeNoLikeNiContainsSensibleAMayusculas()
    {
        using var context = CrearContextoSinConectar();

        var sql = ExpedientesController.AplicarFiltroBusqueda(context.Expedientes, "Sue").ToQueryString();

        Assert.Contains("ILIKE", sql);
    }

    [Fact]
    public void AplicarFiltroBusqueda_EnvuelveAmbosLadosEnUnaccent()
    {
        using var context = CrearContextoSinConectar();

        var sql = ExpedientesController.AplicarFiltroBusqueda(context.Expedientes, "Mexico").ToQueryString();

        // Al menos 3 apariciones: NumeroExpediente, ParteDemandada, y el patrón de
        // búsqueda — si el patrón no está envuelto, "México" nunca coincidiría con
        // "Mexico". No se fija un número exacto porque EF Core podría repetir el
        // patrón una vez por cada lado del OR (columna vs. columna).
        var apariciones = System.Text.RegularExpressions.Regex.Matches(sql, "unaccent", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        Assert.True(apariciones >= 3, $"Se esperaban al menos 3 apariciones de 'unaccent', se encontraron {apariciones}");
    }

    [Fact]
    public void AplicarFiltroBusqueda_ComparaAmbosCampos()
    {
        using var context = CrearContextoSinConectar();

        var sql = ExpedientesController.AplicarFiltroBusqueda(context.Expedientes, "673").ToQueryString();

        Assert.Contains("NumeroExpediente", sql);
        Assert.Contains("ParteDemandada", sql);
    }

    [Fact]
    public void AplicarFiltroBusqueda_SinBusqueda_NoAgregaFiltro()
    {
        using var context = CrearContextoSinConectar();

        var sinFiltro = ExpedientesController.AplicarFiltroBusqueda(context.Expedientes, null).ToQueryString();
        var conEspacios = ExpedientesController.AplicarFiltroBusqueda(context.Expedientes, "   ").ToQueryString();

        Assert.DoesNotContain("ILIKE", sinFiltro);
        Assert.DoesNotContain("ILIKE", conEspacios);
    }

    [Theory]
    [InlineData("Sue", "%Sue%")]
    [InlineData("SUE", "%SUE%")]
    [InlineData("suE", "%suE%")]
    public void EscaparComodinesLike_TextoNormal_NoLoAltera(string entrada, string esperadoDentroDelPatron)
    {
        // Las tres variantes de mayúsculas generan patrones distintos en texto (no se
        // normaliza aquí — eso lo hace ILIKE en el servidor), pero ninguna se escapa
        // de más: confirma que el helper no toca letras normales.
        Assert.Equal(entrada, ExpedientesController.EscaparComodinesLike(entrada));
        Assert.Equal(esperadoDentroDelPatron, $"%{ExpedientesController.EscaparComodinesLike(entrada)}%");
    }

    [Fact]
    public void EscaparComodinesLike_PorcentajeYGuionBajo_QuedanComoTextoLiteral()
    {
        // Caso real: alguien busca "50%" esperando encontrar exactamente eso, no que
        // "%" se interprete como comodín de SQL y regrese todos los expedientes.
        Assert.Equal(@"50\%", ExpedientesController.EscaparComodinesLike("50%"));
        Assert.Equal(@"exp\_1", ExpedientesController.EscaparComodinesLike("exp_1"));
        Assert.Equal(@"100\\\%", ExpedientesController.EscaparComodinesLike(@"100\%"));
    }

    [Fact]
    public void EscaparComodinesLike_CadenaVacia_RegresaVacia()
    {
        Assert.Equal("", ExpedientesController.EscaparComodinesLike(""));
    }
}
