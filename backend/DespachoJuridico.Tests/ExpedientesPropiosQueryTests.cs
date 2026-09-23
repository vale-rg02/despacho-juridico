using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Models.Enums;
using DespachoJuridico.API.Services;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.Tests;

// DJ-102: AplicarFiltroExpedientesPropios -- el universo de expedientes que el
// botón "Actualizar expedientes" de un litigante puede tocar (titular ∪
// colaborador, activos). Cubre también el caso de aislamiento entre litigantes
// que comparten juzgado (requisito "no debe... hacer match fuera de sus
// expedientes propios" del ticket).
public class ExpedientesPropiosQueryTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    private static async Task<(Usuario a, Usuario b)> SembrarDosLitigantesAsync(AppDbContext context)
    {
        var a = new Usuario { Nombre = "Litigante A", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        var b = new Usuario { Nombre = "Litigante B", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.AddRange(a, b);
        await context.SaveChangesAsync();
        return (a, b);
    }

    [Fact]
    public async Task IncluyeExpedienteDondeEsTitular()
    {
        using var context = CrearContextoEnMemoria(nameof(IncluyeExpedienteDondeEsTitular));
        var (a, _) = await SembrarDosLitigantesAsync(context);
        var expediente = new Expediente { NumeroExpediente = "1/2026", UsuarioAsignadoId = a.Id, CreadoPorId = a.Id };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        var resultado = await ScraperAcuerdosService.AplicarFiltroExpedientesPropios(context.Expedientes, a.Id).ToListAsync();

        Assert.Contains(resultado, e => e.Id == expediente.Id);
    }

    [Fact]
    public async Task IncluyeExpedienteDondeEsSoloColaborador()
    {
        using var context = CrearContextoEnMemoria(nameof(IncluyeExpedienteDondeEsSoloColaborador));
        var (a, b) = await SembrarDosLitigantesAsync(context);
        var expediente = new Expediente { NumeroExpediente = "1/2026", UsuarioAsignadoId = b.Id, CreadoPorId = b.Id };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        context.ExpedienteAccesos.Add(new ExpedienteAcceso { ExpedienteId = expediente.Id, UsuarioId = a.Id });
        await context.SaveChangesAsync();

        var resultado = await ScraperAcuerdosService.AplicarFiltroExpedientesPropios(context.Expedientes, a.Id).ToListAsync();

        Assert.Contains(resultado, e => e.Id == expediente.Id);
    }

    [Fact]
    public async Task ExcluyeExpedientesCerrados()
    {
        using var context = CrearContextoEnMemoria(nameof(ExcluyeExpedientesCerrados));
        var (a, _) = await SembrarDosLitigantesAsync(context);
        var expediente = new Expediente
        {
            NumeroExpediente = "1/2026", UsuarioAsignadoId = a.Id, CreadoPorId = a.Id, Estado = EstadoExpediente.Cerrado
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        var resultado = await ScraperAcuerdosService.AplicarFiltroExpedientesPropios(context.Expedientes, a.Id).ToListAsync();

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ExcluyeExpedientesAjenosSinNingunaRelacion()
    {
        using var context = CrearContextoEnMemoria(nameof(ExcluyeExpedientesAjenosSinNingunaRelacion));
        var (a, b) = await SembrarDosLitigantesAsync(context);
        var expedienteDeB = new Expediente { NumeroExpediente = "1/2026", UsuarioAsignadoId = b.Id, CreadoPorId = b.Id };
        context.Expedientes.Add(expedienteDeB);
        await context.SaveChangesAsync();

        var resultado = await ScraperAcuerdosService.AplicarFiltroExpedientesPropios(context.Expedientes, a.Id).ToListAsync();

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task DosLitigantesQueComparteJuzgado_CadaUnoSoloVeElSuyo()
    {
        // Caso real que motiva expedienteIdsFiltro en EjecutarScrapingAsync: A y B
        // tienen expedientes DISTINTOS en el MISMO juzgado ("1ro Civil Hermosillo").
        // Una corrida manual de A nunca debe incluir el expediente de B.
        using var context = CrearContextoEnMemoria(nameof(DosLitigantesQueComparteJuzgado_CadaUnoSoloVeElSuyo));
        var (a, b) = await SembrarDosLitigantesAsync(context);
        var expedienteA = new Expediente
        {
            NumeroExpediente = "1/2026", Juzgado = "1ro Civil Hermosillo", UsuarioAsignadoId = a.Id, CreadoPorId = a.Id
        };
        var expedienteB = new Expediente
        {
            NumeroExpediente = "2/2026", Juzgado = "1ro Civil Hermosillo", UsuarioAsignadoId = b.Id, CreadoPorId = b.Id
        };
        context.Expedientes.AddRange(expedienteA, expedienteB);
        await context.SaveChangesAsync();

        var resultadoA = await ScraperAcuerdosService.AplicarFiltroExpedientesPropios(context.Expedientes, a.Id).ToListAsync();

        Assert.Single(resultadoA);
        Assert.Equal(expedienteA.Id, resultadoA[0].Id);
    }
}
