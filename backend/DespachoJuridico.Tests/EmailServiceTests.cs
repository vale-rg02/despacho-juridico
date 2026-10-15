using System.Net;
using System.Text;
using DespachoJuridico.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DespachoJuridico.Tests;

// DJ-135 (hallazgo de DJ-133): EmailService.EnviarAsync llamaba a Resend sin
// importar el entorno -- la única razón por la que un envío de prueba en
// desarrollo no llegaba a un correo real era que Email:ResendApiKey no estaba
// configurada localmente, algo incidental, no una protección real. Estas
// pruebas verifican el gate real: fuera de Production, por defecto no se
// llama a Resend, salvo que el destinatario esté en la lista explícita de
// Email:DestinatariosPermitidosEnPruebas. Ninguna de estas pruebas toca la
// red -- usan un HttpMessageHandler falso.
public class EmailServiceTests
{
    private const string Asunto = "Asunto de prueba";
    private const string Cuerpo = "<p>Contenido de prueba que nunca debe aparecer en un log.</p>";

    private class EntornoDePrueba : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "DespachoJuridico.Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private class HandlerFalso : HttpMessageHandler
    {
        public List<HttpRequestMessage> Solicitudes { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Solicitudes.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"fake\"}", Encoding.UTF8, "application/json")
            });
        }
    }

    private class FabricaHttpFalsa : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public FabricaHttpFalsa(HttpMessageHandler handler) => _client = new HttpClient(handler);
        public HttpClient CreateClient(string name) => _client;
    }

    private class LoggerCapturador<T> : ILogger<T>
    {
        public List<string> Mensajes { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Mensajes.Add(formatter(state, exception));

        private class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private static EmailService CrearServicio(
        HandlerFalso handler, string entorno, LoggerCapturador<EmailService>? logger = null,
        string? destinatariosPermitidos = null)
    {
        var valoresConfig = new Dictionary<string, string?>
        {
            ["Email:ResendApiKey"] = "clave-de-prueba-valida",
            ["Email:From"] = "notificaciones@despacho-juridico.com",
            ["Email:FromName"] = "Despacho Jurídico"
        };
        if (destinatariosPermitidos != null)
            valoresConfig["Email:DestinatariosPermitidosEnPruebas"] = destinatariosPermitidos;

        var config = new ConfigurationBuilder().AddInMemoryCollection(valoresConfig).Build();

        return new EmailService(
            config,
            logger ?? new LoggerCapturador<EmailService>(),
            new FabricaHttpFalsa(handler),
            new EntornoDePrueba { EnvironmentName = entorno });
    }

    [Fact]
    public async Task Development_ConApiKeyValida_NoHaceNingunaLlamadaHttp()
    {
        var handler = new HandlerFalso();
        var servicio = CrearServicio(handler, Environments.Development);

        await servicio.EnviarAsync("litigante@real.com", "Litigante", Asunto, Cuerpo);

        Assert.Empty(handler.Solicitudes);
    }

    [Fact]
    public async Task Production_SiLlamaAResend_PruebaDeRegresion()
    {
        var handler = new HandlerFalso();
        var servicio = CrearServicio(handler, Environments.Production);

        await servicio.EnviarAsync("litigante@real.com", "Litigante", Asunto, Cuerpo);

        var solicitud = Assert.Single(handler.Solicitudes);
        Assert.Equal("https://api.resend.com/emails", solicitud.RequestUri!.ToString());
    }

    [Fact]
    public async Task FueraDeProduccion_ConListaPermitida_DestinatarioEnLaLista_SiEnvia()
    {
        var handler = new HandlerFalso();
        var servicio = CrearServicio(handler, Environments.Development, destinatariosPermitidos: "equipo@despacho.com,otro@despacho.com");

        await servicio.EnviarAsync("equipo@despacho.com", "Equipo", Asunto, Cuerpo);

        Assert.Single(handler.Solicitudes);
    }

    [Fact]
    public async Task FueraDeProduccion_ConListaPermitida_DestinatarioFueraDeLaLista_SeDescarta()
    {
        var handler = new HandlerFalso();
        var servicio = CrearServicio(handler, Environments.Development, destinatariosPermitidos: "equipo@despacho.com");

        await servicio.EnviarAsync("litigante@real.com", "Litigante", Asunto, Cuerpo);

        Assert.Empty(handler.Solicitudes);
    }

    [Fact]
    public async Task FueraDeProduccion_SinListaConfigurada_NoEnviaANadieYLoRegistraEnElLog()
    {
        var handler = new HandlerFalso();
        var logger = new LoggerCapturador<EmailService>();
        var servicio = CrearServicio(handler, Environments.Development, logger);

        await servicio.EnviarAsync("litigante@real.com", "Litigante", Asunto, Cuerpo);

        Assert.Empty(handler.Solicitudes);
        Assert.Contains(logger.Mensajes, m => m.Contains("bloqueado", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ElLogDeBloqueo_NuncaContieneElCuerpoDelCorreo()
    {
        var handler = new HandlerFalso();
        var logger = new LoggerCapturador<EmailService>();
        var servicio = CrearServicio(handler, Environments.Development, logger);

        await servicio.EnviarAsync("litigante@real.com", "Litigante", Asunto, Cuerpo);

        Assert.All(logger.Mensajes, m => Assert.DoesNotContain("Contenido de prueba", m));
    }

    [Theory]
    [InlineData("valeria.rg.24@gmail.com", "v***@gmail.com")]
    [InlineData("a@b.com", "a***@b.com")]
    public void EnmascararEmail_MuestraSoloPrimeraLetraYDominio(string entrada, string esperado)
    {
        Assert.Equal(esperado, EmailService.EnmascararEmail(entrada));
    }

    [Fact]
    public void ObtenerDestinatariosPermitidos_SinConfigurar_RegresaListaVacia()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Empty(EmailService.ObtenerDestinatariosPermitidos(config));
    }

    [Fact]
    public void EstaPermitidoFueraDeProduccion_ComparacionNoDistingueMayusculas()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:DestinatariosPermitidosEnPruebas"] = "Equipo@Despacho.com"
            })
            .Build();

        Assert.True(EmailService.EstaPermitidoFueraDeProduccion("equipo@despacho.com", config));
    }
}
