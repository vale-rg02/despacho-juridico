using DespachoJuridico.API.Services;

namespace DespachoJuridico.Tests;

// Test double compartido por los tests que ejercitan código que manda correo
// (AcuerdosController, etc.) -- no hay bandeja de pruebas real en el proyecto
// de tests, así que solo se registra qué se hubiera enviado.
internal class FakeEmailService : IEmailService
{
    public List<(string Email, string Nombre, string Asunto, string Cuerpo)> Enviados { get; } = new();

    public Task EnviarAsync(string destinatarioEmail, string destinatarioNombre, string asunto, string cuerpoHtml)
    {
        Enviados.Add((destinatarioEmail, destinatarioNombre, asunto, cuerpoHtml));
        return Task.CompletedTask;
    }
}
