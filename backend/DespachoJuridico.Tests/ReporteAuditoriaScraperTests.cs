using DespachoJuridico.API.DTOs;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Services;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-140: piezas de decisión 100% puras del reporte de auditoría -- sin EF,
// sin IO. La orquestación completa (EF real) se prueba en
// ReporteAuditoriaScraperIntegracionTests.cs, mismo patrón que DJ-139.
public class CalcularDiasHabilesSinSeveridadTests
{
    private static readonly HashSet<DateOnly> SinDiasNoHabiles = new();

    [Fact]
    public void SinHallazgos_CuentaTodosLosDiasHabilesDesdeInicioVentana()
    {
        // Lunes 2026-10-12 a viernes 2026-10-16 -- 5 días hábiles, sin fin de semana en medio.
        var (s1, s2, s3) = ReporteAuditoriaScraperService.CalcularDiasHabilesSinSeveridad(
            new(), hoy: new DateOnly(2026, 10, 16), inicioVentana: new DateOnly(2026, 10, 12), SinDiasNoHabiles);

        Assert.Equal(5, s1);
        Assert.Equal(5, s2);
        Assert.Equal(5, s3);
    }

    [Fact]
    public void FinDeSemana_NoCuenta()
    {
        // Viernes 2026-10-16 a lunes 2026-10-19 -- sábado y domingo no cuentan.
        var (s1, _, _) = ReporteAuditoriaScraperService.CalcularDiasHabilesSinSeveridad(
            new(), hoy: new DateOnly(2026, 10, 19), inicioVentana: new DateOnly(2026, 10, 16), SinDiasNoHabiles);

        Assert.Equal(2, s1); // viernes 16 y lunes 19
    }

    [Fact]
    public void DiaNoHabilConfigurado_NoCuenta()
    {
        var noHabiles = new HashSet<DateOnly> { new DateOnly(2026, 10, 14) };
        var (s1, _, _) = ReporteAuditoriaScraperService.CalcularDiasHabilesSinSeveridad(
            new(), hoy: new DateOnly(2026, 10, 14), inicioVentana: new DateOnly(2026, 10, 12), noHabiles);

        // 12 (lun), 13 (mar) cuentan; 14 (mié) está en noHabiles -- no suma ese día.
        Assert.Equal(2, s1);
    }

    [Fact]
    public void UnS1_ReiniciaLosTresContadores()
    {
        var hallazgos = new List<(DateOnly, string)> { (new DateOnly(2026, 10, 14), "S1") };

        var (s1, s2, s3) = ReporteAuditoriaScraperService.CalcularDiasHabilesSinSeveridad(
            hallazgos, hoy: new DateOnly(2026, 10, 16), inicioVentana: new DateOnly(2026, 10, 12), SinDiasNoHabiles);

        // 12, 13 cuentan; 14 (S1) reinicia todo a 0; 15, 16 vuelven a sumar -> 2.
        Assert.Equal(2, s1);
        Assert.Equal(2, s2);
        Assert.Equal(2, s3);
    }

    [Fact]
    public void UnS2_SoloReiniciaSuPropioContador()
    {
        var hallazgos = new List<(DateOnly, string)> { (new DateOnly(2026, 10, 14), "S2") };

        var (s1, s2, s3) = ReporteAuditoriaScraperService.CalcularDiasHabilesSinSeveridad(
            hallazgos, hoy: new DateOnly(2026, 10, 16), inicioVentana: new DateOnly(2026, 10, 12), SinDiasNoHabiles);

        Assert.Equal(5, s1); // S1 nunca ocurrió -- sigue contando los 5 días hábiles
        Assert.Equal(2, s2); // reiniciado en 14, vuelve a sumar 15 y 16
        Assert.Equal(5, s3);
    }

    [Fact]
    public void UnS3_SoloReiniciaSuPropioContador()
    {
        var hallazgos = new List<(DateOnly, string)> { (new DateOnly(2026, 10, 14), "S3") };

        var (s1, s2, s3) = ReporteAuditoriaScraperService.CalcularDiasHabilesSinSeveridad(
            hallazgos, hoy: new DateOnly(2026, 10, 16), inicioVentana: new DateOnly(2026, 10, 12), SinDiasNoHabiles);

        Assert.Equal(5, s1);
        Assert.Equal(5, s2);
        Assert.Equal(2, s3);
    }
}

public class DetectarUnidadesEnCeroTests
{
    private static BitacoraEvaluacionScraper Fila(int idUnidad, DateOnly fecha, int devueltos) => new()
    {
        IdUnidad = idUnidad, NombreJuzgado = "Juzgado X", FechaConsultada = fecha,
        Origen = "Automatica", Devueltos = devueltos
    };

    [Fact]
    public void TresDiasConsecutivosEnCero_AlcanzaElUmbral()
    {
        var hoy = new DateOnly(2026, 10, 16);
        var historial = new List<BitacoraEvaluacionScraper>
        {
            Fila(153, hoy, 0),
            Fila(153, hoy.AddDays(-1), 0),
            Fila(153, hoy.AddDays(-2), 0),
        };

        var resultado = ReporteAuditoriaScraperService.DetectarUnidadesEnCero(historial, hoy, diasUmbral: 3);

        var anomalia = Assert.Single(resultado);
        Assert.Equal(153, anomalia.IdUnidad);
        Assert.Equal(3, anomalia.DiasConsecutivosEnCero);
    }

    [Fact]
    public void DosDiasConsecutivos_NoAlcanzaElUmbralDeTres()
    {
        var hoy = new DateOnly(2026, 10, 16);
        var historial = new List<BitacoraEvaluacionScraper>
        {
            Fila(153, hoy, 0),
            Fila(153, hoy.AddDays(-1), 0),
            Fila(153, hoy.AddDays(-2), 5), // rompe la racha
        };

        var resultado = ReporteAuditoriaScraperService.DetectarUnidadesEnCero(historial, hoy, diasUmbral: 3);

        Assert.Empty(resultado);
    }

    [Fact]
    public void HoyConDevueltos_NuncaEsAnomalia()
    {
        var hoy = new DateOnly(2026, 10, 16);
        var historial = new List<BitacoraEvaluacionScraper>
        {
            Fila(153, hoy, 5),
            Fila(153, hoy.AddDays(-1), 0),
            Fila(153, hoy.AddDays(-2), 0),
        };

        Assert.Empty(ReporteAuditoriaScraperService.DetectarUnidadesEnCero(historial, hoy, diasUmbral: 3));
    }
}

public class DetectarCaidasBruscasTests
{
    private static BitacoraEvaluacionScraper Fila(int idUnidad, DateOnly fecha, int devueltos) => new()
    {
        IdUnidad = idUnidad, NombreJuzgado = "Juzgado X", FechaConsultada = fecha,
        Origen = "Automatica", Devueltos = devueltos
    };

    [Fact]
    public void CaidaPorDebajoDelUmbral_SeDetecta()
    {
        var hoy = new DateOnly(2026, 10, 16);
        var historial = new List<BitacoraEvaluacionScraper>
        {
            Fila(153, hoy, 1), // hoy: 1
            Fila(153, hoy.AddDays(-1), 10),
            Fila(153, hoy.AddDays(-2), 10),
            Fila(153, hoy.AddDays(-3), 10), // mediana de los 3 previos = 10
        };

        var resultado = ReporteAuditoriaScraperService.DetectarCaidasBruscas(historial, hoy, umbralProporcion: 0.5);

        var anomalia = Assert.Single(resultado);
        Assert.Equal(1, anomalia.DevueltosHoy);
        Assert.Equal(10, anomalia.MedianaUltimos7Dias);
    }

    [Fact]
    public void SinSuficienteHistorial_NoSeEvalua()
    {
        var hoy = new DateOnly(2026, 10, 16);
        var historial = new List<BitacoraEvaluacionScraper>
        {
            Fila(153, hoy, 1),
            Fila(153, hoy.AddDays(-1), 10), // solo 1 día previo, menos del mínimo de 3
        };

        Assert.Empty(ReporteAuditoriaScraperService.DetectarCaidasBruscas(historial, hoy, umbralProporcion: 0.5));
    }

    [Fact]
    public void MedianaCero_NuncaEsCaida()
    {
        var hoy = new DateOnly(2026, 10, 16);
        var historial = new List<BitacoraEvaluacionScraper>
        {
            Fila(153, hoy, 0),
            Fila(153, hoy.AddDays(-1), 0),
            Fila(153, hoy.AddDays(-2), 0),
            Fila(153, hoy.AddDays(-3), 0),
        };

        Assert.Empty(ReporteAuditoriaScraperService.DetectarCaidasBruscas(historial, hoy, umbralProporcion: 0.5));
    }

    [Fact]
    public void DentroDelUmbral_NoEsCaida()
    {
        var hoy = new DateOnly(2026, 10, 16);
        var historial = new List<BitacoraEvaluacionScraper>
        {
            Fila(153, hoy, 6), // 6 >= 10*0.5 -> no es caída
            Fila(153, hoy.AddDays(-1), 10),
            Fila(153, hoy.AddDays(-2), 10),
            Fila(153, hoy.AddDays(-3), 10),
        };

        Assert.Empty(ReporteAuditoriaScraperService.DetectarCaidasBruscas(historial, hoy, umbralProporcion: 0.5));
    }
}

public class ValidarHallazgoTests
{
    private static HallazgoAuditoriaRequest Valido() => new()
    {
        Fecha = new DateOnly(2026, 10, 16), Severidad = "S1", Categoria = "oculto", Descripcion = "falso negativo"
    };

    [Fact]
    public void RequestValido_NoDaError() => Assert.Null(ReporteAuditoriaScraperService.ValidarHallazgo(Valido()));

    [Theory]
    [InlineData("S0")]
    [InlineData("s1")]
    [InlineData("")]
    public void SeveridadInvalida_DaError(string severidad)
    {
        var req = Valido();
        req.Severidad = severidad;
        Assert.NotNull(ReporteAuditoriaScraperService.ValidarHallazgo(req));
    }

    [Fact]
    public void CategoriaVacia_DaError()
    {
        var req = Valido();
        req.Categoria = "";
        Assert.NotNull(ReporteAuditoriaScraperService.ValidarHallazgo(req));
    }

    [Fact]
    public void DescripcionVacia_DaError()
    {
        var req = Valido();
        req.Descripcion = "";
        Assert.NotNull(ReporteAuditoriaScraperService.ValidarHallazgo(req));
    }

    [Fact]
    public void DescripcionLarga_DaError()
    {
        var req = Valido();
        req.Descripcion = new string('a', 81);
        Assert.NotNull(ReporteAuditoriaScraperService.ValidarHallazgo(req));
    }

    [Fact]
    public void DescripcionConDemasiadasPalabras_DaError()
    {
        var req = Valido();
        req.Descripcion = "esto parece mas bien un parrafo completo de texto no una descripcion corta";
        Assert.NotNull(ReporteAuditoriaScraperService.ValidarHallazgo(req));
    }
}

public class ProximoEnvioProgramadoTests
{
    [Fact]
    public void AntesDeLaHora_ProgramaHoyMismo()
    {
        var ahora = new DateTime(2026, 10, 16, 10, 0, 0);
        var resultado = ReporteAuditoriaScraperService.ProximoEnvioProgramado(ahora, new TimeOnly(18, 30));

        Assert.Equal(new DateTime(2026, 10, 16, 18, 30, 0), resultado);
    }

    [Fact]
    public void DespuesDeLaHora_ProgramaMañana()
    {
        var ahora = new DateTime(2026, 10, 16, 19, 0, 0);
        var resultado = ReporteAuditoriaScraperService.ProximoEnvioProgramado(ahora, new TimeOnly(18, 30));

        Assert.Equal(new DateTime(2026, 10, 17, 18, 30, 0), resultado);
    }
}

// Guardarraíl estructural idéntico al de DJ-139 -- si alguien agrega un campo
// "Partes" o "Nombre" a estos modelos/DTOs sin querer, esta prueba truena.
public class AuditoriaScraperDTOsEstructuraTests
{
    [Fact]
    public void NingunCampoDeLosModelosODTOs_MencionaPartesONombre()
    {
        var tipos = new[]
        {
            typeof(HallazgoAuditoria), typeof(HallazgoAuditoriaRequest), typeof(HallazgoAuditoriaResponse),
            typeof(ReporteAuditoriaScraperResponse), typeof(AcuerdoOcultoCandidato), typeof(ColisionJuzgadoCandidato),
            typeof(AnomaliaUnidadEnCero), typeof(AnomaliaCaidaBrusca)
        };

        foreach (var tipo in tipos)
        {
            foreach (var propiedad in tipo.GetProperties())
            {
                Assert.DoesNotContain("Partes", propiedad.Name);
                Assert.DoesNotContain("NombrePart", propiedad.Name);
            }
        }
    }
}
