using DespachoJuridico.API.Services;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-122 (cierre retroactivo, 14 sep 2026): DJ-122 se desplegó el 12 sep 2026 sin
// reevaluar los ocultos ya existentes -- ReevaluarOcultosAsync solo subía Baja a
// Alta, nunca a Media, así que los acuerdos detectados ANTES del despliegue que
// solo mencionaban al banco (sin nombre del demandado) se quedaban ocultos para
// siempre. Casos reales confirmados el 14 de septiembre de 2026, los tres en
// juzgados de Hermosillo con Banco=BBVA México y Partes="ESPECIAL HIPOTECARIO -
// ACCIÓN PERSONAL Y REAL.- BBVA MEXICO, S.A." (sin nombre del demandado): exp.
// 898/2026 (3ro Civil), 883/2026 (2do Civil), 1354/2025 (3ro Civil).
//
// Estas pruebas reproducen la decisión pieza por pieza con los mismos datos
// reales -- ReevaluarOcultosAsync en sí usa IServiceScopeFactory/DbContext reales
// igual que el resto de la clase, así que no se prueba de punta a punta aquí (ver
// ConfianzaMediaTests.cs y DescartarAcuerdoTests.cs para el mismo patrón).
public class ReevaluarMediaTests
{
    private const string PartesSoloBanco = "ESPECIAL HIPOTECARIO - ACCIÓN PERSONAL Y REAL.- BBVA MEXICO, S.A.";

    [Theory]
    [InlineData(154)] // 3ro Civil Hermosillo -- exp. 898/2026 y 1354/2025
    [InlineData(153)] // 2do Civil Hermosillo -- exp. 883/2026
    public void CasoReal_HermosilloAtrapadoAntesDelDespliegue_CalificaParaMedia(int idUnidad)
    {
        // Reproduce la rama Hermosillo de ReevaluarOcultosAsync: número+juzgado ya
        // coincidieron (precondición para haber llegado a Confianza=Baja en
        // Hermosillo en primer lugar), el nombre no coincide, pero el banco sí --
        // debe calificar para Media, sin necesitar JuzgadoForaneoCoincide (eso solo
        // aplica a la rama foránea).
        Assert.Contains(idUnidad, ScraperAcuerdosService.JuzgadosHermosillo);
        Assert.False(ScraperAcuerdosService.EsSerieAuxiliar("Exp."));
        Assert.False(ScraperAcuerdosService.PartesCoinciden("GERARDO BALDERAS PEREZ Y OTRA", PartesSoloBanco));
        Assert.True(ScraperAcuerdosService.MencionaBancoOAlias("BBVA México", PartesSoloBanco));
    }

    [Fact]
    public void ForaneoSinJuzgadoConfirmado_NoCalificaParaMediaAunqueMencioneElBanco()
    {
        // Control de regresión: a diferencia de Hermosillo, un match foráneo por
        // solo número (juzgado del acuerdo distinto al registrado en el expediente)
        // nunca debe subir a Media aunque mencione el banco -- el número por sí
        // solo se repite en todo el estado (ver comentario en EjecutarScrapingAsync).
        var juzgadoCoincide = ScraperAcuerdosService.JuzgadoForaneoCoincide(
            "PRIMERO CIVIL DE NOGALES", "Juzgado 1ro Familiar Nogales");

        Assert.False(juzgadoCoincide);
    }

    [Fact]
    public void SerieAuxiliar_NuncaCalificaParaMediaAunqueMencioneElBancoYElJuzgadoCoincida()
    {
        // Exh./Cuad. tienen numeración propia del juzgado receptor -- coincidir con
        // el expediente es casualidad de número, no identidad real (ver comentario
        // de esSerieAuxiliar en EjecutarScrapingAsync). ReevaluarOcultosAsync debe
        // excluirlos de Media igual que la corrida en vivo.
        Assert.True(ScraperAcuerdosService.EsSerieAuxiliar("Exh."));
        Assert.True(ScraperAcuerdosService.EsSerieAuxiliar("Cuad."));
    }

    [Fact]
    public void SinBancoCapturadoEnElExpediente_NuncaCalificaParaMedia()
    {
        // MencionaBancoOAlias jamás se invoca en producción si expediente.Banco es
        // null (ver comentario de la firma) -- documentado aquí porque
        // ReevaluarOcultosAsync depende explícitamente de esa misma guarda.
        Assert.False(ScraperAcuerdosService.MencionaBancoOAlias("", PartesSoloBanco));
    }
}
