using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        await SeedUsuariosAsync(context);
        await SeedBancosAsync(context);
        await SeedEtapasCatalogoAsync(context);
        await SeedExpedientesAsync(context);
    }

    private static async Task SeedUsuariosAsync(AppDbContext context)
    {
        if (await context.Usuarios.AnyAsync()) return;

        context.Usuarios.AddRange(
            new Usuario
            {
                Nombre = "Socio Principal",
                Email = "socio@despacho.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Rol = RolUsuario.Socio
            },
            new Usuario
            {
                Nombre = "Carlos",
                Email = "carlos@despacho.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("carlos123"),
                Rol = RolUsuario.Litigante
            }
        );

        await context.SaveChangesAsync();
    }

    private static async Task SeedBancosAsync(AppDbContext context)
    {
        if (await context.Bancos.AnyAsync()) return;

        context.Bancos.AddRange(
            new Banco { Nombre = "BBVA México", Telefono = "800-226-2663" },
            new Banco { Nombre = "HSBC", Telefono = "800-712-4722" },
            new Banco { Nombre = "Santander", Telefono = "800-501-0000" },
            new Banco { Nombre = "Banco Azteca", Telefono = "800-912-3456" }
        );

        await context.SaveChangesAsync();
    }

    private static async Task SeedEtapasCatalogoAsync(AppDbContext context)
    {
        if (await context.EtapasCatalogo.AnyAsync()) return;

        context.EtapasCatalogo.AddRange(
            // Civil / Hipotecario
            new EtapaCatalogo { Nombre = "Demanda", TipoJuicio = "Civil", Orden = 1, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Radicación", TipoJuicio = "Civil", Orden = 2, TerminoDias = 7, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Emplazamiento", TipoJuicio = "Civil", Orden = 3, TerminoDias = 180, EsDiasHabiles = false },
            new EtapaCatalogo { Nombre = "Contestación", TipoJuicio = "Civil", Orden = 4, TerminoDias = 5, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Acusar Rebeldía", TipoJuicio = "Civil", Orden = 5, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Pruebas", TipoJuicio = "Civil", Orden = 6, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Alegatos", TipoJuicio = "Civil", Orden = 7, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Sentencia", TipoJuicio = "Civil", Orden = 8, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Amparo", TipoJuicio = "Civil", Orden = 9, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Certificado de Gravamen", TipoJuicio = "Civil", Orden = 10, TerminoDias = 180, EsDiasHabiles = false },
            new EtapaCatalogo { Nombre = "Avalúos", TipoJuicio = "Civil", Orden = 11, TerminoDias = 180, EsDiasHabiles = false },
            new EtapaCatalogo { Nombre = "Diligencia de Remate", TipoJuicio = "Civil", Orden = 12, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Lanzamiento", TipoJuicio = "Civil", Orden = 13, TerminoDias = null, EsDiasHabiles = true },

            // Oral Mercantil
            new EtapaCatalogo { Nombre = "Demanda", TipoJuicio = "Oral Mercantil", Orden = 1, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Radicación", TipoJuicio = "Oral Mercantil", Orden = 2, TerminoDias = 7, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Emplazamiento", TipoJuicio = "Oral Mercantil", Orden = 3, TerminoDias = 120, EsDiasHabiles = false },
            new EtapaCatalogo { Nombre = "Contestación", TipoJuicio = "Oral Mercantil", Orden = 4, TerminoDias = 9, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Audiencia Preliminar", TipoJuicio = "Oral Mercantil", Orden = 5, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Audiencia de Juicio", TipoJuicio = "Oral Mercantil", Orden = 6, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Audiencia de Sentencia", TipoJuicio = "Oral Mercantil", Orden = 7, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Sentencia", TipoJuicio = "Oral Mercantil", Orden = 8, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Amparo", TipoJuicio = "Oral Mercantil", Orden = 9, TerminoDias = null, EsDiasHabiles = true }
        );

        await context.SaveChangesAsync();
    }

    private static async Task SeedExpedientesAsync(AppDbContext context)
    {
        if (await context.Expedientes.AnyAsync()) return;

        // Buscamos usuarios y bancos ya sembrados (o existentes) para vincularlos
        var carlos = await context.Usuarios.FirstAsync(u => u.Email == "carlos@despacho.com");
        var socio = await context.Usuarios.FirstAsync(u => u.Email == "socio@despacho.com");

        var bbva = await context.Bancos.FirstAsync(b => b.Nombre == "BBVA México");
        var hsbc = await context.Bancos.FirstAsync(b => b.Nombre == "HSBC");
        var santander = await context.Bancos.FirstAsync(b => b.Nombre == "Santander");
        var azteca = await context.Bancos.FirstAsync(b => b.Nombre == "Banco Azteca");

        var ahora = DateTime.UtcNow;

        context.Expedientes.AddRange(
            new Expediente
            {
                NumeroExpediente = "673/2019",
                ParteDemandada = "Juan García López",
                BancoId = hsbc.Id,
                Juzgado = "1ro Civil",
                Materia = "Hipotecario",
                TipoJuicio = "Civil",
                Estado = EstadoExpediente.Abierto,
                Prioridad = Prioridad.Urgente,
                UsuarioAsignadoId = carlos.Id,
                Notas = "Cliente con antecedentes de pagos tardíos.",
                CreadoPorId = carlos.Id,
                CreadoEn = ahora,
                ActualizadoEn = ahora
            },
            new Expediente
            {
                NumeroExpediente = "412/2021",
                ParteDemandada = "BBVA México",
                BancoId = bbva.Id,
                Juzgado = "1ro Oral Mercantil",
                Materia = "Mercantil",
                TipoJuicio = "Oral Mercantil",
                Estado = EstadoExpediente.Abierto,
                Prioridad = Prioridad.Normal,
                UsuarioAsignadoId = carlos.Id,
                CreadoPorId = carlos.Id,
                CreadoEn = ahora,
                ActualizadoEn = ahora
            },
            new Expediente
            {
                NumeroExpediente = "891/2020",
                ParteDemandada = "María Rodríguez",
                BancoId = santander.Id,
                Juzgado = "2do Civil",
                Materia = "Hipotecario",
                TipoJuicio = "Civil",
                Estado = EstadoExpediente.Abierto,
                Prioridad = Prioridad.Prioritario,
                UsuarioAsignadoId = carlos.Id,
                Notas = "Pendiente recibir documentos.",
                CreadoPorId = carlos.Id,
                CreadoEn = ahora,
                ActualizadoEn = ahora
            },
            new Expediente
            {
                NumeroExpediente = "234/2022",
                ParteDemandada = "Banco Azteca",
                BancoId = azteca.Id,
                Juzgado = "2do Oral Mercantil",
                Materia = "Mercantil",
                TipoJuicio = "Oral Mercantil",
                Estado = EstadoExpediente.Cerrado,
                Prioridad = Prioridad.Normal,
                UsuarioAsignadoId = socio.Id,
                CreadoPorId = socio.Id,
                CreadoEn = ahora,
                ActualizadoEn = ahora
            },
            new Expediente
            {
                NumeroExpediente = "150/2023",
                ParteDemandada = "Roberto Sánchez Mena",
                BancoId = hsbc.Id,
                Juzgado = "3ro Civil",
                Materia = "Hipotecario",
                TipoJuicio = "Civil",
                Estado = EstadoExpediente.Pausado,
                Prioridad = Prioridad.Normal,
                UsuarioAsignadoId = carlos.Id,
                Notas = "Pausado en espera de resolución de amparo.",
                CreadoPorId = carlos.Id,
                CreadoEn = ahora,
                ActualizadoEn = ahora
            }
        );

        await context.SaveChangesAsync();
    }
}