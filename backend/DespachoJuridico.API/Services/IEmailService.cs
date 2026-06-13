using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace DespachoJuridico.API.Services;

public interface IEmailService
{
    Task EnviarAsync(string destinatarioEmail, string destinatarioNombre, string asunto, string cuerpoHtml);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task EnviarAsync(string destinatarioEmail, string destinatarioNombre, string asunto, string cuerpoHtml)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress(_config["Email:FromName"], _config["Email:From"]));
        mensaje.To.Add(new MailboxAddress(destinatarioNombre, destinatarioEmail));
        mensaje.Subject = asunto;
        mensaje.Body = new TextPart("html") { Text = cuerpoHtml };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            _config["Email:Host"],
            _config.GetValue<int>("Email:Port"),
            SecureSocketOptions.StartTls);

        await client.AuthenticateAsync(
            _config["Email:Username"],
            _config["Email:Password"]);

        await client.SendAsync(mensaje);
        await client.DisconnectAsync(true);

        _logger.LogInformation("Correo enviado a {Email}: {Asunto}", destinatarioEmail, asunto);
    }
}