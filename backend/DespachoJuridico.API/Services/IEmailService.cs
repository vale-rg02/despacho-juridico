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

    public EmailService(IConfiguration config, ILogger<EmailService> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task EnviarAsync(string destinatarioEmail, string destinatarioNombre, string asunto, string cuerpoHtml)
    {
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
}