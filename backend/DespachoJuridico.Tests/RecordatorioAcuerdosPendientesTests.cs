using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Xunit;

namespace DespachoJuridico.Tests;

// Recordatorio pasivo (DJ-122): un acuerdo "Media" sin confirmar ni descartar debe
// aparecer destacado en un correo agrupado después de N días — nunca ocultarse ni
// subir a "Alta" por el solo paso del tiempo. Estas pruebas cubren el criterio de
// elegibilidad (EsElegibleParaRecordatorio), que es lo único con lógica de decisión
// real en RecordatorioAcuerdosPendientesService — el resto del servicio usa
// IServiceScopeFactory/DbContext reales, igual que ReevaluarOcultosAsync y
// RevisionFechasService (ver ReevaluarMediaTests.cs para el mismo patrón).
public class RecordatorioAcuerdosPendientesTests
{
    private const int DiasUmbral = 3;

    private static AcuerdoScrapeado NuevoAcuerdo(string? confianza, DateTime fechaDetectado,
        bool descartadoManualmente = false, bool recordatorioPendienteEnviado = false) => new()
    {
        NumeroExpediente = "401/2026",
        NombreJuzgado = "Juzgado 1ro Civil Nogales",
        Partes = "ESPECIAL HIPOTECARIO.- BBVA MEXICO SA",
        Sintesis = "Se radica.-",
        FechaAcuerdo = DateOnly.FromDateTime(fechaDetectado),
        FechaDetectado = fechaDetectado,
        Confianza = confianza,
        DescartadoManualmente = descartadoManualmente,
        RecordatorioPendienteEnviado = recordatorioPendienteEnviado
    };

    [Fact]
    public void AcuerdoMediaDeMenosDeTresDias_NoEsElegible()
    {
        var ahora = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var acuerdo = NuevoAcuerdo("Media", ahora.AddDays(-2));

        Assert.False(RecordatorioAcuerdosPendientesService.EsElegibleParaRecordatorio(acuerdo, ahora, DiasUmbral));
    }

    [Fact]
    public void AcuerdoMediaDeMasDeTresDiasSinResolver_EsElegible()
    {
        var ahora = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var acuerdo = NuevoAcuerdo("Media", ahora.AddDays(-3).AddMinutes(-1));

        Assert.True(RecordatorioAcuerdosPendientesService.EsElegibleParaRecordatorio(acuerdo, ahora, DiasUmbral));
    }

    [Fact]
    public void AcuerdoExactamenteEnElUmbral_EsElegible()
    {
        // >= diasUmbral, no > diasUmbral -- el límite cuenta como cumplido.
        var ahora = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var acuerdo = NuevoAcuerdo("Media", ahora.AddDays(-DiasUmbral));

        Assert.True(RecordatorioAcuerdosPendientesService.EsElegibleParaRecordatorio(acuerdo, ahora, DiasUmbral));
    }

    [Fact]
    public void AcuerdoYaConfirmado_ConfianzaAlta_NoEsElegibleAunqueTengaMasDeTresDias()
    {
        // Confirmar (DJ-122) sube Media -> Alta -- ya no es "sin resolver".
        var ahora = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var acuerdo = NuevoAcuerdo("Alta", ahora.AddDays(-10));

        Assert.False(RecordatorioAcuerdosPendientesService.EsElegibleParaRecordatorio(acuerdo, ahora, DiasUmbral));
    }

    [Fact]
    public void AcuerdoYaDescartado_NoEsElegibleAunqueSigaEnMedia()
    {
        // DescartadoManualmente no cambia Confianza (ver DescartarAcuerdoTests.cs) --
        // por eso se checa aparte, no basta con mirar Confianza.
        var ahora = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var acuerdo = NuevoAcuerdo("Media", ahora.AddDays(-10), descartadoManualmente: true);

        Assert.False(RecordatorioAcuerdosPendientesService.EsElegibleParaRecordatorio(acuerdo, ahora, DiasUmbral));
    }

    [Fact]
    public void AcuerdoYaRecordadoAntes_NoVuelveASerElegible()
    {
        // Una sola vez por acuerdo -- evitar fatiga de notificación (ver comentario
        // de RecordatorioPendienteEnviado en el modelo).
        var ahora = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var acuerdo = NuevoAcuerdo("Media", ahora.AddDays(-10), recordatorioPendienteEnviado: true);

        Assert.False(RecordatorioAcuerdosPendientesService.EsElegibleParaRecordatorio(acuerdo, ahora, DiasUmbral));
    }

    [Fact]
    public void AcuerdoBajaOSinEvaluar_NuncaEsElegible()
    {
        var ahora = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(RecordatorioAcuerdosPendientesService.EsElegibleParaRecordatorio(
            NuevoAcuerdo("Baja", ahora.AddDays(-10)), ahora, DiasUmbral));
        Assert.False(RecordatorioAcuerdosPendientesService.EsElegibleParaRecordatorio(
            NuevoAcuerdo(null, ahora.AddDays(-10)), ahora, DiasUmbral));
    }
}
