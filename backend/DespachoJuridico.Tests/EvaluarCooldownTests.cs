using DespachoJuridico.API.Controllers;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-102: EvaluarCooldown decide si el botón "Actualizar expedientes" del
// litigante puede dispararse ahora mismo -- pura y sin BD, mismo patrón que
// ProximaCorridaProgramada (ScraperAcuerdosServiceTests).
public class EvaluarCooldownTests
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan UmbralObsoleto = TimeSpan.FromMinutes(10);

    [Fact]
    public void NuncaCorrioAntes_PermiteDeInmediato()
    {
        var ahora = new DateTime(2026, 9, 23, 12, 0, 0);

        var (puedeIniciar, _, cooldownRestante, _) = ScraperController.EvaluarCooldown(
            enProgreso: false, iniciadoEn: null, ultimaConsultaEn: null, ahora, Cooldown, UmbralObsoleto);

        Assert.True(puedeIniciar);
        Assert.Equal(0, cooldownRestante);
    }

    [Fact]
    public void CooldownActivo_Bloquea_ConSegundosRestantesCorrectos()
    {
        var ahora = new DateTime(2026, 9, 23, 12, 10, 0);
        var ultimaConsultaEn = new DateTime(2026, 9, 23, 12, 0, 0); // hace 10 min, faltan 5

        var (puedeIniciar, _, cooldownRestante, mensaje) = ScraperController.EvaluarCooldown(
            enProgreso: false, iniciadoEn: null, ultimaConsultaEn, ahora, Cooldown, UmbralObsoleto);

        Assert.False(puedeIniciar);
        Assert.Equal(300, cooldownRestante);
        Assert.NotNull(mensaje);
    }

    [Fact]
    public void CooldownVencido_Permite()
    {
        var ahora = new DateTime(2026, 9, 23, 12, 15, 1);
        var ultimaConsultaEn = new DateTime(2026, 9, 23, 12, 0, 0); // hace 15 min y 1 seg

        var (puedeIniciar, _, cooldownRestante, _) = ScraperController.EvaluarCooldown(
            enProgreso: false, iniciadoEn: null, ultimaConsultaEn, ahora, Cooldown, UmbralObsoleto);

        Assert.True(puedeIniciar);
        Assert.Equal(0, cooldownRestante);
    }

    [Fact]
    public void CooldownExactoEnElLimite_Permite()
    {
        var ahora = new DateTime(2026, 9, 23, 12, 15, 0);
        var ultimaConsultaEn = new DateTime(2026, 9, 23, 12, 0, 0); // exactamente 15 min

        var (puedeIniciar, _, _, _) = ScraperController.EvaluarCooldown(
            enProgreso: false, iniciadoEn: null, ultimaConsultaEn, ahora, Cooldown, UmbralObsoleto);

        Assert.True(puedeIniciar);
    }

    [Fact]
    public void EnProgresoReciente_Bloquea()
    {
        var ahora = new DateTime(2026, 9, 23, 12, 1, 0);
        var iniciadoEn = new DateTime(2026, 9, 23, 12, 0, 0); // hace 1 min

        var (puedeIniciar, progresoObsoleto, _, mensaje) = ScraperController.EvaluarCooldown(
            enProgreso: true, iniciadoEn, ultimaConsultaEn: null, ahora, Cooldown, UmbralObsoleto);

        Assert.False(puedeIniciar);
        Assert.False(progresoObsoleto);
        Assert.NotNull(mensaje);
    }

    [Fact]
    public void EnProgresoConIniciadoEnMuyViejo_SeTrataComoObsoletoYPermite()
    {
        // El proceso murió a medio de una corrida (ej. redeploy) sin limpiar el
        // flag -- no debe dejar al usuario bloqueado para siempre.
        var ahora = new DateTime(2026, 9, 23, 12, 11, 0);
        var iniciadoEn = new DateTime(2026, 9, 23, 12, 0, 0); // hace 11 min, > umbral de 10

        var (puedeIniciar, progresoObsoleto, _, _) = ScraperController.EvaluarCooldown(
            enProgreso: true, iniciadoEn, ultimaConsultaEn: null, ahora, Cooldown, UmbralObsoleto);

        Assert.True(puedeIniciar);
        Assert.True(progresoObsoleto);
    }

    [Fact]
    public void EnProgresoObsoleto_PeroConCooldownVigente_SigueBloqueando()
    {
        // El progreso viejo ya no bloquea por sí mismo, pero si además hay una
        // última consulta reciente, el cooldown normal sigue aplicando.
        var ahora = new DateTime(2026, 9, 23, 12, 11, 0);
        var iniciadoEn = new DateTime(2026, 9, 23, 12, 0, 0); // obsoleto
        var ultimaConsultaEn = new DateTime(2026, 9, 23, 12, 5, 0); // hace 6 min, faltan 9

        var (puedeIniciar, progresoObsoleto, cooldownRestante, _) = ScraperController.EvaluarCooldown(
            enProgreso: true, iniciadoEn, ultimaConsultaEn, ahora, Cooldown, UmbralObsoleto);

        Assert.True(progresoObsoleto);
        Assert.False(puedeIniciar);
        Assert.Equal(540, cooldownRestante);
    }
}
