using DespachoJuridico.API.Services;
using Xunit;

namespace DespachoJuridico.Tests;

// Link directo desde el correo de notificación de acuerdos (Alta/Media) hacia
// la sección de Acuerdos del expediente -- Opción A (ruta protegida normal +
// return URL post-login, ver RutaProtegida.jsx/Login.jsx en el frontend). Aquí
// solo se prueba la construcción de la URL, que es la única lógica de decisión
// dentro de ScraperAcuerdosService -- el resto (armar el HTML, mandar el
// correo) requiere IEmailService real, mismo patrón que EnviarRecordatorioAsync
// (no se prueba de punta a punta, ver ReevaluarMediaTests.cs).
public class LinkAcuerdoEmailTests
{
    [Theory]
    [InlineData("https://app.acedoehijos.com", 159, "https://app.acedoehijos.com/expedientes/159#acuerdos")]
    [InlineData("https://app.acedoehijos.com/", 159, "https://app.acedoehijos.com/expedientes/159#acuerdos")]
    [InlineData("http://localhost:5173", 1, "http://localhost:5173/expedientes/1#acuerdos")]
    public void ConstruirUrlAcuerdo_ArmaLaUrlCorrecta(string frontendBaseUrl, int expedienteId, string esperado)
    {
        Assert.Equal(esperado, ScraperAcuerdosService.ConstruirUrlAcuerdo(frontendBaseUrl, expedienteId));
    }

    [Fact]
    public void ConstruirAsuntoNotificacion_Media_UsaElTextoDecididoConNumeroDeExpediente()
    {
        Assert.Equal(
            "Posible acuerdo del expediente (Exp. 401/2026) — confirma para notificar",
            ScraperAcuerdosService.ConstruirAsuntoNotificacion("401/2026", esSugerido: true));
    }

    [Fact]
    public void ConstruirAsuntoNotificacion_Alta_SigueIgualQueAntes()
    {
        // Control de regresión: este cambio solo tocaba el texto de Media.
        Assert.Equal(
            "Nuevo acuerdo judicial — Exp. 401/2026",
            ScraperAcuerdosService.ConstruirAsuntoNotificacion("401/2026", esSugerido: false));
    }
}
