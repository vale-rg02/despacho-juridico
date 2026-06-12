using DespachoJuridico.API.Services;
using Xunit;

namespace DespachoJuridico.Tests;

public class CalculadorFechasServiceTests
{
    private readonly ICalculadorFechasService _calculador = new CalculadorFechasService();

    [Fact]
    public void SumarDiasHabiles_CasoSimple_SaltaFinDeSemana()
    {
        // Jueves 11 jun 2026 + 7 días hábiles = lunes 22 jun 2026
        var fechaInicio = new DateTime(2026, 6, 11);

        var resultado = _calculador.SumarDiasHabiles(fechaInicio, 7);

        Assert.Equal(new DateTime(2026, 6, 22), resultado);
    }

    [Fact]
    public void SumarDiasHabiles_CruzandoFestivo_SaltaDiaFestivo()
    {
        // Viernes 11 sep 2026 + 5 días hábiles, saltando el 16 sep (Independencia) = lunes 21 sep 2026
        var fechaInicio = new DateTime(2026, 9, 11);

        var resultado = _calculador.SumarDiasHabiles(fechaInicio, 5);

        Assert.Equal(new DateTime(2026, 9, 21), resultado);
    }

    [Fact]
    public void SumarDiasNaturales_NoSaltaNada()
    {
        // Jueves 11 jun 2026 + 180 días naturales = martes 8 dic 2026
        var fechaInicio = new DateTime(2026, 6, 11);

        var resultado = _calculador.SumarDiasNaturales(fechaInicio, 180);

        Assert.Equal(new DateTime(2026, 12, 8), resultado);
    }

    [Fact]
    public void EsDiaHabil_FinDeSemana_RegresaFalse()
    {
        var sabado = new DateTime(2026, 6, 13);
        var domingo = new DateTime(2026, 6, 14);

        Assert.False(_calculador.EsDiaHabil(sabado));
        Assert.False(_calculador.EsDiaHabil(domingo));
    }

    [Fact]
    public void EsDiaHabil_DiaFestivoOficial_RegresaFalse()
    {
        // 16 de septiembre - Día de la Independencia
        var diaIndependencia = new DateTime(2026, 9, 16);

        Assert.False(_calculador.EsDiaHabil(diaIndependencia));
    }

    [Fact]
    public void EsDiaHabil_DiaNormal_RegresaTrue()
    {
        // Jueves cualquiera sin festivo
        var diaNormal = new DateTime(2026, 6, 11);

        Assert.True(_calculador.EsDiaHabil(diaNormal));
    }

    [Fact]
    public void CalcularFechaLimite_TerminoNull_RegresaNull()
    {
        var fechaInicio = new DateTime(2026, 6, 11);

        var resultado = _calculador.CalcularFechaLimite(fechaInicio, null, esDiasHabiles: true);

        Assert.Null(resultado);
    }

    [Fact]
    public void CalcularFechaLimite_DiasHabiles_CalculaCorrectamente()
    {
        var fechaInicio = new DateTime(2026, 6, 11);

        var resultado = _calculador.CalcularFechaLimite(fechaInicio, 7, esDiasHabiles: true);

        Assert.Equal(new DateTime(2026, 6, 22), resultado);
    }

    [Fact]
    public void CalcularFechaLimite_DiasNaturales_CalculaCorrectamente()
    {
        var fechaInicio = new DateTime(2026, 6, 11);

        var resultado = _calculador.CalcularFechaLimite(fechaInicio, 180, esDiasHabiles: false);

        Assert.Equal(new DateTime(2026, 12, 8), resultado);
    }

    [Theory]
    [InlineData(2026, 1, 1)]   // Año Nuevo
    [InlineData(2026, 5, 1)]   // Día del Trabajo
    [InlineData(2026, 12, 25)] // Navidad
    public void EsDiaHabil_FestivosFijos_RegresaFalse(int año, int mes, int dia)
    {
        var fecha = new DateTime(año, mes, dia);

        Assert.False(_calculador.EsDiaHabil(fecha));
    }
}