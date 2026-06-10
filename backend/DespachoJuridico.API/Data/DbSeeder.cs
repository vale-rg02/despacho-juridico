using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using DespachoJuridico.API.Models.Enums;

namespace DespachoJuridico.API.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        // Solo ejecuta si no hay datos
        if (context.Usuarios.Any()) return;

        // Usuario socio
        var socio = new Usuario
        {
            Nombre = "Socio Principal",
            Email = "socio@despacho.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Rol = RolUsuario.Socio
        };

        // Usuario litigante (Carlos)
        var carlos = new Usuario
        {
            Nombre = "Carlos",
            Email = "carlos@despacho.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("carlos123"),
            Rol = RolUsuario.Litigante
        };

        context.Usuarios.AddRange(socio, carlos);

        // Bancos básicos
        context.Bancos.AddRange(
            new Banco { Nombre = "BBVA México", Telefono = "800-226-2663" },
            new Banco { Nombre = "HSBC", Telefono = "800-712-4722" },
            new Banco { Nombre = "Santander", Telefono = "800-501-0000" },
            new Banco { Nombre = "Banco Azteca", Telefono = "800-912-3456" }
        );

        // Catálogo de etapas — Civil/Hipotecario
        context.EtapasCatalogo.AddRange(
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
}