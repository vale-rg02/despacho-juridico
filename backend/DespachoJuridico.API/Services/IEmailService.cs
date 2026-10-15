using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DespachoJuridico.API.Services;

public interface IEmailService
{
    Task EnviarAsync(string destinatarioEmail, string destinatarioNombre, string asunto, string cuerpoHtml);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IWebHostEnvironment _environment;

    public EmailService(
        IConfiguration config, ILogger<EmailService> logger, IHttpClientFactory httpClientFactory,
        IWebHostEnvironment environment)
    {
        _config = config;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _environment = environment;
    }

    public async Task EnviarAsync(string destinatarioEmail, string destinatarioNombre, string asunto, string cuerpoHtml)
    {
        // DJ-135 (hallazgo de DJ-133): antes de este gate, EnviarAsync llamaba a
        // Resend sin importar el entorno -- la única razón por la que un envío de
        // prueba en desarrollo no llegaba a un correo real era que
        // Email:ResendApiKey no estaba configurada localmente, algo incidental,
        // no una protección real. Fuera de Production, por defecto NO se envía
        // nada real; solo se permite si el destinatario está en la lista
        // explícita de Email:DestinatariosPermitidosEnPruebas (pensada para
        // probar con el equipo, nunca con litigantes). Se usa "!IsProduction()"
        // en vez de "IsDevelopment()" a propósito: cualquier entorno futuro que
        // no sea exactamente Production (ej. un ambiente de revisión/staging)
        // también queda bloqueado por defecto, no solo "Development".
        if (!_environment.IsProduction() && !EstaPermitidoFueraDeProduccion(destinatarioEmail, _config))
        {
            _logger.LogWarning(
                "Correo bloqueado fuera de Production (entorno: {Entorno}). Asunto: {Asunto}. Destinatario: {DestinatarioEnmascarado}. Tamaño del cuerpo: {TamanoCuerpo} caracteres.",
                _environment.EnvironmentName, asunto, EnmascararEmail(destinatarioEmail), cuerpoHtml.Length);
            return;
        }

        var apiKey = _config["Email:ResendApiKey"];

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            from = $"{_config["Email:FromName"]} <{_config["Email:From"]}>",
            to = new[] { destinatarioEmail },
            subject = asunto,
            html = cuerpoHtml
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://api.resend.com/emails", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Error al enviar correo a {Email}: {Error}", destinatarioEmail, error);
            throw new Exception($"Resend error: {error}");
        }

        _logger.LogInformation("Correo enviado a {Email}: {Asunto}", destinatarioEmail, asunto);
    }

    // DJ-135: lista explícita de direcciones a las que sí se permite un correo
    // real fuera de Production -- para que el equipo pueda probar un correo de
    // verdad (ej. DJ-140) sin arriesgar que le llegue algo a un litigante real
    // por accidente. Vacía o ausente = no se envía a nadie fuera de Production.
    internal static bool EstaPermitidoFueraDeProduccion(string destinatarioEmail, IConfiguration config)
    {
        var permitidos = ObtenerDestinatariosPermitidos(config);
        return permitidos.Contains(destinatarioEmail.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    internal static List<string> ObtenerDestinatariosPermitidos(IConfiguration config)
    {
        var crudo = config["Email:DestinatariosPermitidosEnPruebas"];
        if (string.IsNullOrWhiteSpace(crudo)) return new List<string>();

        return crudo.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    // DJ-135: nunca el correo completo en logs -- solo primera letra + dominio.
    internal static string EnmascararEmail(string email)
    {
        var arroba = email.IndexOf('@');
        if (arroba <= 0) return "***";
        return $"{email[0]}***{email[arroba..]}";
    }
}