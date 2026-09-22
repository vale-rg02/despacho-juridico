using DespachoJuridico.API.Services;
using Xunit;

namespace DespachoJuridico.Tests;

public class ScraperAcuerdosServiceTests
{
    // Horario por defecto: cada 2 horas (120 min), de 00:05 a 18:05.
    private const int IntervaloMinutos = 120;
    private const int HoraInicio = 0;
    private const int HoraFin = 18;

    [Fact]
    public void ProximaCorridaProgramada_AntesDeLaPrimeraCorrida_RegresaHoyA00_05()
    {
        var ahora = new DateTime(2026, 8, 19, 0, 0, 0);

        var proxima = ScraperAcuerdosService.ProximaCorridaProgramada(ahora, HoraInicio, HoraFin, IntervaloMinutos);

        Assert.Equal(new DateTime(2026, 8, 19, 0, 5, 0), proxima);
    }

    [Fact]
    public void ProximaCorridaProgramada_EntreDosCorridas_RegresaLaSiguienteDeHoy()
    {
        // 16:37 — ya pasaron las de 00:05...16:05, la siguiente es 18:05 de hoy.
        var ahora = new DateTime(2026, 8, 19, 16, 37, 0);

        var proxima = ScraperAcuerdosService.ProximaCorridaProgramada(ahora, HoraInicio, HoraFin, IntervaloMinutos);

        Assert.Equal(new DateTime(2026, 8, 19, 18, 5, 0), proxima);
    }

    [Fact]
    public void ProximaCorridaProgramada_DespuesDeLaUltimaCorrida_RegresaMananaA00_05()
    {
        // 19:00 — ya pasó la corrida de las 18:05, no debe volver a correr hoy.
        var ahora = new DateTime(2026, 8, 19, 19, 0, 0);

        var proxima = ScraperAcuerdosService.ProximaCorridaProgramada(ahora, HoraInicio, HoraFin, IntervaloMinutos);

        Assert.Equal(new DateTime(2026, 8, 20, 0, 5, 0), proxima);
    }

    [Fact]
    public void ProximaCorridaProgramada_ExactoEnHoraDeCorrida_SaltaALaSiguiente()
    {
        // Si "ahora" cae justo en el minuto de una corrida programada, no debe
        // repetirla — pasa a la siguiente (candidato debe ser estrictamente > ahora).
        var ahora = new DateTime(2026, 8, 19, 0, 5, 0);

        var proxima = ScraperAcuerdosService.ProximaCorridaProgramada(ahora, HoraInicio, HoraFin, IntervaloMinutos);

        Assert.Equal(new DateTime(2026, 8, 19, 2, 5, 0), proxima);
    }

    [Fact]
    public void ProximaCorridaProgramada_CruceDeAnio_RegresaPrimeroDeEneroA00_05()
    {
        var ahora = new DateTime(2026, 12, 31, 20, 0, 0);

        var proxima = ScraperAcuerdosService.ProximaCorridaProgramada(ahora, HoraInicio, HoraFin, IntervaloMinutos);

        Assert.Equal(new DateTime(2027, 1, 1, 0, 5, 0), proxima);
    }

    [Fact]
    public void ProximaCorridaProgramada_IntervaloDistinto_GeneraHorarioCorrecto()
    {
        // Configuración alterna: cada 3 horas (180 min), de 6:00 a 20:00 (sin el
        // minuto :05 aplicado aquí para simplificar — HoraInicio/HoraFin son
        // enteros de hora).
        var ahora = new DateTime(2026, 8, 19, 6, 30, 0);

        var proxima = ScraperAcuerdosService.ProximaCorridaProgramada(ahora, horaInicio: 6, horaFin: 20, intervaloMinutos: 180);

        // 6:05 ya pasó (6:30 > 6:05) — la siguiente es 9:05.
        Assert.Equal(new DateTime(2026, 8, 19, 9, 5, 0), proxima);
    }

    [Fact]
    public void ProximaCorridaProgramada_JustoAntesDelInicio_RegresaHoraInicio()
    {
        var ahora = new DateTime(2026, 8, 19, 0, 4, 59);

        var proxima = ScraperAcuerdosService.ProximaCorridaProgramada(ahora, HoraInicio, HoraFin, IntervaloMinutos);

        Assert.Equal(new DateTime(2026, 8, 19, 0, 5, 0), proxima);
    }

    [Fact]
    public void ProximaCorridaProgramada_Intervalo90Min_CaeExactoEn18_05()
    {
        // DJ-104 Paso 1: cada 90 min desde 00:05 debe caer exacto en 18:05
        // (1080 min de ventana / 90 = 12 pasos), sin desalinearse con la ventana.
        var ahora = new DateTime(2026, 8, 19, 16, 40, 0);

        var proxima = ScraperAcuerdosService.ProximaCorridaProgramada(ahora, HoraInicio, HoraFin, intervaloMinutos: 90);

        // Corridas del día: 00:05, 01:35, 03:05, ..., 16:35, 18:05.
        Assert.Equal(new DateTime(2026, 8, 19, 18, 5, 0), proxima);
    }
}
