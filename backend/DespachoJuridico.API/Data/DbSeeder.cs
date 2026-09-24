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
        await MigrarAlmonedasBajoRemateAsync(context);
        await MigrarIncidenteASubmenuAsync(context);
        await MigrarTerminoATipoJuicioAsync(context);
        await SeedSedesYJuzgadosAsync(context);
        await SeedExpedientesAsync(context);
        await MigrarNotasEtapaLegadoAsync(context);
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
        // Cada banco se revisa individualmente (en vez de "si ya hay alguno, no tocar nada")
        // para poder agregar bancos nuevos a bases de datos ya sembradas, como producción.
        var bancos = new[]
        {
            new Banco { Nombre = "BBVA México", Telefono = "800-226-2663" },
            new Banco { Nombre = "HSBC", Telefono = "800-712-4722" },
            new Banco { Nombre = "Santander", Telefono = "800-501-0000" },
            new Banco { Nombre = "Banco Azteca", Telefono = "800-912-3456" },
            new Banco { Nombre = "Scotiabank", Telefono = "800-704-5900" }
        };

        var nombresExistentes = await context.Bancos.Select(b => b.Nombre).ToListAsync();

        foreach (var banco in bancos)
        {
            if (!nombresExistentes.Contains(banco.Nombre))
                context.Bancos.Add(banco);
        }

        await context.SaveChangesAsync();
    }

    // internal (no private) para que las pruebas puedan sembrar solo el catálogo
    // de etapas sin correr todo SeedAsync (mismo criterio que MigrarAlmonedasBajoRemateAsync/
    // MigrarTerminoATipoJuicioAsync, ya internal por la misma razón).
    internal static async Task SeedEtapasCatalogoAsync(AppDbContext context)
    {
        // Cada etapa se revisa individualmente por (Nombre, TipoJuicio), en vez de
        // "si ya hay alguna, no tocar nada", para poder agregar etapas nuevas al
        // catálogo de bases de datos ya sembradas, como producción (ver DJ-68/69).
        var etapas = new[]
        {
            // Hipotecario (materia Civil) — incluye pasos de remate (Certificado
            // de Gravamen, Avalúos, Diligencia de Remate, Almonedas, Lanzamiento,
            // Ejecución Forzosa), propios de un juicio hipotecario
            new EtapaCatalogo { Nombre = "Demanda", TipoJuicio = "Hipotecario", Orden = 1, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Radicación", TipoJuicio = "Hipotecario", Orden = 2, TerminoDias = 7, EsDiasHabiles = true },
            // DJ-78: "Término" venía huérfana (TipoJuicio=NULL) desde la importación
            // masiva del Excel original (Data/MigracionExcel.cs creaba la fila del
            // catálogo solo por nombre, sin tipo de juicio). Se replica aquí por
            // TipoJuicio siguiendo el mismo patrón que ya usa el resto del catálogo
            // (Demanda/Radicación/etc. también están duplicadas por tipo) — evidencia
            // real en HistorialEtapas: 94 usos en Hipotecario, 140 en Oral Mercantil.
            // Ver MigrarTerminoATipoJuicioAsync más abajo, que reasigna el historial
            // ya existente de la fila huérfana a esta.
            new EtapaCatalogo { Nombre = "Término", TipoJuicio = "Hipotecario", Orden = 3, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Emplazamiento", TipoJuicio = "Hipotecario", Orden = 3, TerminoDias = 180, EsDiasHabiles = false },
            new EtapaCatalogo { Nombre = "Contestación", TipoJuicio = "Hipotecario", Orden = 4, TerminoDias = 5, EsDiasHabiles = true },
            // DJ-119: pedido por Mario para Hipotecario y Oral Mercantil — no se
            // agrega a Especial/Ordinario Mercantil (sin catálogo propio todavía,
            // cero expedientes activos reales, ver materiaTipoJuicio.js) ni al resto
            // de tipos, por falta de evidencia de que aplique ahí igual (mismo
            // criterio de DJ-78). Un incidente procesal no ocurre en un punto fijo
            // de la secuencia — puede surgir en varios momentos del juicio — así
            // que, sin evidencia real de cuándo lo registra el despacho (no se pudo
            // consultar producción para esto, ver ticket), se posiciona comparte
            // Orden con la etapa siguiente a Contestación (igual que Emplazamiento/
            // Término ya comparten Orden 3): el dropdown no fuerza una secuencia
            // estricta, HistorialEtapas se muestra por FechaInicio real, no por
            // Orden, así que esto no le impide registrarse en cualquier momento.
            new EtapaCatalogo { Nombre = "Incidente", TipoJuicio = "Hipotecario", Orden = 5, TerminoDias = null, EsDiasHabiles = true },
            // DJ-121: submenú de "Incidente" pedido por Mario — a diferencia de
            // Remate/Almonedas (secuencial y excluyente: 1ra → si no se vende → 2da
            // → 3ra), cada tipo de incidente es un trámite autónomo del juicio
            // principal; varios pueden coexistir a la vez en el mismo expediente
            // (ej. un incidente de liquidación de intereses y uno de nulidad de
            // emplazamiento no se excluyen entre sí). Se confirmó que esto YA
            // funciona sin cambios en SelectorEtapaCatalogo.jsx ni en RegistrarEtapa:
            // ninguno de los dos impone exclusividad hoy — cada registro de etapa
            // es una fila independiente en HistorialEtapas, y la lista siempre
            // muestra todas. Se reparentan más abajo, en MigrarIncidenteASubmenuAsync
            // (mismo patrón que MigrarAlmonedasBajoRemateAsync).
            new EtapaCatalogo { Nombre = "Intereses Moratorios", TipoJuicio = "Hipotecario", Orden = 1, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Intereses Ordinarios", TipoJuicio = "Hipotecario", Orden = 2, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Intereses Ordinarios y Moratorios", TipoJuicio = "Hipotecario", Orden = 3, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Liquidación de Costas", TipoJuicio = "Hipotecario", Orden = 4, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Nulidad de emplazamiento", TipoJuicio = "Hipotecario", Orden = 5, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Innominado", TipoJuicio = "Hipotecario", Orden = 6, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Acusar Rebeldía", TipoJuicio = "Hipotecario", Orden = 5, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Pruebas", TipoJuicio = "Hipotecario", Orden = 6, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Alegatos", TipoJuicio = "Hipotecario", Orden = 7, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Sentencia", TipoJuicio = "Hipotecario", Orden = 8, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Amparo", TipoJuicio = "Hipotecario", Orden = 9, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Certificado de Gravamen", TipoJuicio = "Hipotecario", Orden = 10, TerminoDias = 180, EsDiasHabiles = false },
            new EtapaCatalogo { Nombre = "Avalúos", TipoJuicio = "Hipotecario", Orden = 11, TerminoDias = 180, EsDiasHabiles = false },
            new EtapaCatalogo { Nombre = "Diligencia de Remate", TipoJuicio = "Hipotecario", Orden = 12, TerminoDias = null, EsDiasHabiles = true },
            // DJ-76: "Remate" agrupa a las 3 almonedas en un submenú — ver
            // MigrarAlmonedasBajoRemateAsync más abajo, que reparenta las almonedas
            // ya existentes en bases de datos sembradas antes de este cambio (como
            // producción) sin tocar ningún HistorialEtapa.
            new EtapaCatalogo { Nombre = "Remate", TipoJuicio = "Hipotecario", Orden = 13, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "1ra Almoneda", TipoJuicio = "Hipotecario", Orden = 13, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "2da Almoneda", TipoJuicio = "Hipotecario", Orden = 14, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "3ra Almoneda", TipoJuicio = "Hipotecario", Orden = 15, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Lanzamiento", TipoJuicio = "Hipotecario", Orden = 16, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Ejecución Forzosa", TipoJuicio = "Hipotecario", Orden = 17, TerminoDias = null, EsDiasHabiles = true },

            // Oral Mercantil
            new EtapaCatalogo { Nombre = "Demanda", TipoJuicio = "Oral Mercantil", Orden = 1, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Radicación", TipoJuicio = "Oral Mercantil", Orden = 2, TerminoDias = 7, EsDiasHabiles = true },
            // DJ-78: ver comentario en la versión de Hipotecario, arriba.
            new EtapaCatalogo { Nombre = "Término", TipoJuicio = "Oral Mercantil", Orden = 3, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Emplazamiento", TipoJuicio = "Oral Mercantil", Orden = 3, TerminoDias = 120, EsDiasHabiles = false },
            new EtapaCatalogo { Nombre = "Contestación", TipoJuicio = "Oral Mercantil", Orden = 4, TerminoDias = 9, EsDiasHabiles = true },
            // DJ-119: ver comentario en la versión de Hipotecario, arriba.
            new EtapaCatalogo { Nombre = "Incidente", TipoJuicio = "Oral Mercantil", Orden = 5, TerminoDias = null, EsDiasHabiles = true },
            // DJ-121: ver comentario en la versión de Hipotecario, arriba.
            new EtapaCatalogo { Nombre = "Intereses Moratorios", TipoJuicio = "Oral Mercantil", Orden = 1, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Intereses Ordinarios", TipoJuicio = "Oral Mercantil", Orden = 2, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Intereses Ordinarios y Moratorios", TipoJuicio = "Oral Mercantil", Orden = 3, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Liquidación de Costas", TipoJuicio = "Oral Mercantil", Orden = 4, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Nulidad de emplazamiento", TipoJuicio = "Oral Mercantil", Orden = 5, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Innominado", TipoJuicio = "Oral Mercantil", Orden = 6, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Audiencia Preliminar", TipoJuicio = "Oral Mercantil", Orden = 5, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Audiencia de Juicio", TipoJuicio = "Oral Mercantil", Orden = 6, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Audiencia de Sentencia", TipoJuicio = "Oral Mercantil", Orden = 7, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Sentencia", TipoJuicio = "Oral Mercantil", Orden = 8, TerminoDias = null, EsDiasHabiles = true },
            // DJ-78: "Término para Amparo" venía huérfana igual que "Término" (misma
            // causa, ver comentario arriba) — pero a diferencia de "Término", la
            // evidencia en HistorialEtapas muestra que SOLO se usó en Oral Mercantil
            // (141 de 141 usos; cero en Hipotecario), así que no se replica ahí.
            new EtapaCatalogo { Nombre = "Término para Amparo", TipoJuicio = "Oral Mercantil", Orden = 9, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Amparo", TipoJuicio = "Oral Mercantil", Orden = 9, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Certificado de Gravamen", TipoJuicio = "Oral Mercantil", Orden = 10, TerminoDias = 180, EsDiasHabiles = false },
            new EtapaCatalogo { Nombre = "Remate", TipoJuicio = "Oral Mercantil", Orden = 11, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "1ra Almoneda", TipoJuicio = "Oral Mercantil", Orden = 11, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "2da Almoneda", TipoJuicio = "Oral Mercantil", Orden = 12, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "3ra Almoneda", TipoJuicio = "Oral Mercantil", Orden = 13, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Ejecución Forzosa", TipoJuicio = "Oral Mercantil", Orden = 14, TerminoDias = null, EsDiasHabiles = true },

            // Jurisdicción Voluntaria (materia Civil) — sin plazos definidos por ahora
            new EtapaCatalogo { Nombre = "Radicación", TipoJuicio = "Jurisdiccion Voluntaria", Orden = 1, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Notificación", TipoJuicio = "Jurisdiccion Voluntaria", Orden = 2, TerminoDias = null, EsDiasHabiles = true },

            // DJ-78: Familiar y Arrendamiento todavía no tienen catálogo de etapas
            // propio (pendiente, DJ-68/69) — pero sí hay historial real importado
            // que usa "Término" bajo esos tipos (3 y 1 caso respectivamente), así
            // que se les crea esta única entrada para no dejarlos huérfanos. No
            // implica que el catálogo completo de esos tipos ya esté resuelto.
            new EtapaCatalogo { Nombre = "Término", TipoJuicio = "Familiar", Orden = 1, TerminoDias = null, EsDiasHabiles = true },
            new EtapaCatalogo { Nombre = "Término", TipoJuicio = "Arrendamiento", Orden = 1, TerminoDias = null, EsDiasHabiles = true }
        };

        var existentes = await context.EtapasCatalogo
            .Select(e => new { e.Nombre, e.TipoJuicio })
            .ToListAsync();

        foreach (var etapa in etapas)
        {
            if (!existentes.Any(e => e.Nombre == etapa.Nombre && e.TipoJuicio == etapa.TipoJuicio))
                context.EtapasCatalogo.Add(etapa);
        }

        await context.SaveChangesAsync();
    }

    // DJ-76: reparenta las almonedas ya existentes (de bases de datos sembradas
    // antes de este cambio, como producción) bajo su "Remate" correspondiente por
    // TipoJuicio. Es puramente metadata del catálogo — HistorialEtapa sigue
    // apuntando exactamente a la misma fila de "1ra/2da/3ra Almoneda" que ya
    // apuntaba antes, así que ningún registro histórico se toca ni se reinterpreta.
    // Idempotente: solo actualiza filas cuyo EtapaPadreId todavía sea null.
    internal static async Task MigrarAlmonedasBajoRemateAsync(AppDbContext context)
    {
        var remates = await context.EtapasCatalogo
            .Where(e => e.Nombre == "Remate")
            .ToListAsync();

        foreach (var remate in remates)
        {
            var almonedasSinPadre = await context.EtapasCatalogo
                .Where(e =>
                    e.TipoJuicio == remate.TipoJuicio &&
                    e.EtapaPadreId == null &&
                    (e.Nombre == "1ra Almoneda" || e.Nombre == "2da Almoneda" || e.Nombre == "3ra Almoneda"))
                .ToListAsync();

            foreach (var almoneda in almonedasSinPadre)
                almoneda.EtapaPadreId = remate.Id;
        }

        await context.SaveChangesAsync();
    }

    // DJ-121: reparenta las 6 opciones de incidente (ya sembradas arriba como
    // filas de primer nivel, mismo motivo que las almonedas: no se conoce el Id
    // de "Incidente" al construir el arreglo de SeedEtapasCatalogoAsync) bajo su
    // "Incidente" correspondiente por TipoJuicio. A diferencia de Remate, esto NO
    // vuelve excluyentes a las opciones entre sí — HistorialEtapa sigue
    // permitiendo una fila por cada incidente registrado, sin límite ni reemplazo
    // (confirmado: ni SelectorEtapaCatalogo.jsx ni RegistrarEtapa imponen
    // exclusividad, ver comentario en SeedEtapasCatalogoAsync). Un HistorialEtapa
    // ya existente que apunte directo a "Incidente" (si Mario llegó a registrar
    // alguno con el modelo plano de DJ-119 antes de este cambio) sigue siendo
    // válido — "Incidente" no se borra, solo gana hijas — deliberadamente no se
    // reclasifica a ninguna de las 6 opciones nuevas sin que Mario lo confirme
    // (mismo criterio de no forzar migraciones sin certeza que ya se usó en DJ-78).
    // Idempotente: solo actualiza filas cuyo EtapaPadreId todavía sea null.
    internal static async Task MigrarIncidenteASubmenuAsync(AppDbContext context)
    {
        var nombresOpciones = new[]
        {
            "Intereses Moratorios",
            "Intereses Ordinarios",
            "Intereses Ordinarios y Moratorios",
            "Liquidación de Costas",
            "Nulidad de emplazamiento",
            "Innominado"
        };

        var incidentes = await context.EtapasCatalogo
            .Where(e => e.Nombre == "Incidente")
            .ToListAsync();

        foreach (var incidente in incidentes)
        {
            var opcionesSinPadre = await context.EtapasCatalogo
                .Where(e =>
                    e.TipoJuicio == incidente.TipoJuicio &&
                    e.EtapaPadreId == null &&
                    nombresOpciones.Contains(e.Nombre))
                .ToListAsync();

            foreach (var opcion in opcionesSinPadre)
                opcion.EtapaPadreId = incidente.Id;
        }

        await context.SaveChangesAsync();
    }

    // DJ-78: "Término" y "Término para Amparo" quedaron huérfanas (TipoJuicio=NULL)
    // desde la importación masiva del Excel original (ver Data/MigracionExcel.cs,
    // que crea la fila del catálogo solo por nombre, sin tipo de juicio). Este
    // método reasigna cada HistorialEtapa que todavía apunte a la fila huérfana
    // hacia la fila correcta por TipoJuicio (según el TipoJuicio real del propio
    // expediente, ya corregido por la migración CorregirTipoJuicioExpedientesImportados),
    // y borra la fila huérfana una vez que ya nadie la referencia. Idempotente:
    // si la huérfana ya no existe (ya se migró antes), no hace nada.
    internal static async Task MigrarTerminoATipoJuicioAsync(AppDbContext context)
    {
        var nombresAMigrar = new[] { "Término", "Término para Amparo" };

        var huerfanas = await context.EtapasCatalogo
            .Where(e => e.TipoJuicio == null && nombresAMigrar.Contains(e.Nombre))
            .ToListAsync();

        foreach (var huerfana in huerfanas)
        {
            var historialAfectado = await context.HistorialEtapas
                .Include(h => h.Expediente)
                .Where(h => h.EtapaCatalogoId == huerfana.Id)
                .ToListAsync();

            foreach (var grupo in historialAfectado.GroupBy(h => h.Expediente!.TipoJuicio))
            {
                var tipoJuicio = grupo.Key;
                var destino = await context.EtapasCatalogo
                    .FirstOrDefaultAsync(e => e.Nombre == huerfana.Nombre && e.TipoJuicio == tipoJuicio);

                if (destino == null)
                {
                    // No hay a dónde reasignar (TipoJuicio nulo/inesperado en el
                    // expediente) — se deja intacto para revisión manual en vez de
                    // adivinar, y por lo tanto la huérfana tampoco se borra todavía.
                    continue;
                }

                foreach (var historial in grupo)
                    historial.EtapaCatalogoId = destino.Id;
            }

            await context.SaveChangesAsync();

            var quedanReferencias = await context.HistorialEtapas.AnyAsync(h => h.EtapaCatalogoId == huerfana.Id);
            if (!quedanReferencias)
                context.EtapasCatalogo.Remove(huerfana);
        }

        await context.SaveChangesAsync();
    }

    // Historial de notas por etapa: NotasLegado (antes "Notas", ver
    // HistorialEtapa.cs) era un solo campo que se sobreescribía en cada edición
    // -- no hay forma de recuperar lo que decía antes de la última edición, solo
    // el valor vigente al momento del deploy. Esa nota vigente se migra como la
    // primera NotaEtapa de su historial (con la fecha más cercana disponible,
    // FechaInicio, y el mismo usuario que registró la etapa). Idempotente: una
    // vez migrada, la etapa ya tiene NotaEtapa y no se vuelve a tocar -- permite
    // correr esto en cada arranque sin duplicar.
    internal static async Task MigrarNotasEtapaLegadoAsync(AppDbContext context)
    {
        var pendientes = await context.HistorialEtapas
            .Where(h => h.NotasLegado != null && h.NotasLegado != "")
            .Where(h => !context.NotasEtapa.Any(n => n.HistorialEtapaId == h.Id))
            .ToListAsync();

        foreach (var historial in pendientes)
        {
            context.NotasEtapa.Add(new NotaEtapa
            {
                HistorialEtapaId = historial.Id,
                Texto = historial.NotasLegado!,
                CreadoEn = historial.FechaInicio,
                CreadoPorId = historial.RegistradoPorId
            });
        }

        if (pendientes.Count > 0)
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
                Materia = "Civil",
                TipoJuicio = "Hipotecario",
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
                Materia = "Civil",
                TipoJuicio = "Hipotecario",
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
                Materia = "Civil",
                TipoJuicio = "Hipotecario",
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

    // DJ-112/DJ-87: catálogo de Sede (municipio) y Juzgado dependiente. Fuente:
    // el diccionario interno de ADISON en ScraperAcuerdosService.Juzgados (uso
    // exclusivo del scraper, ~84 entradas) -- copia deliberada, no lectura en
    // vivo, para no arriesgar la lógica de matching ya afinada ahí (ver DJ-103).
    // Se excluye el IdUnidad 297 ("Juzgado Familiar Competencia Especializada")
    // por no tener municipio confirmado (ver comentario "SIN DISTRITO
    // CONFIRMADO" en ese diccionario) -- no aparece como opción seleccionable
    // hasta que se confirme su sede real.
    //
    // Idempotente por (Sede, Juzgado) individual, igual que
    // SeedEtapasCatalogoAsync, para poder agregar entradas nuevas a este
    // catálogo en despliegues futuros sin que lo ya sembrado bloquee lo nuevo.
    internal static async Task SeedSedesYJuzgadosAsync(AppDbContext context)
    {
        var catalogo = new (string Sede, string[] Juzgados)[]
        {
            ("Hermosillo", new[]
            {
                "1ro Civil Hermosillo", "2do Civil Hermosillo", "3ro Civil Hermosillo",
                "1ro Mercantil Hermosillo", "2do Mercantil Hermosillo", "3ro Mercantil Hermosillo", "4to Mercantil Hermosillo",
                "1ro Oral Mercantil Hermosillo", "2do Oral Mercantil Hermosillo",
                "Arrendamiento Hermosillo",
                "1ro Familiar Hermosillo", "2do Familiar Hermosillo", "3ro Familiar Hermosillo", "4to Familiar Hermosillo",
                "Juzgado Especializado Violencia de Género Hermosillo",
                "1er Tribunal Colegiado 1er Circuito", "2do Tribunal Colegiado 1er Circuito",
                "Secretaría General de Acuerdos Hermosillo",
                "1ro Penal Hermosillo", "2do Penal Hermosillo", "3ro Penal Hermosillo", "5to Penal Hermosillo",
                "Juzgado Oral Penal Hermosillo",
                "Juzgado Adolescentes Hermosillo",
                "Tribunal Unitario Regional Adolescentes/Penal Oral Hermosillo",
                "Juzgado Ejecución de Sanciones Hermosillo",
                "1er Tribunal Laboral Hermosillo", "2do Tribunal Laboral Hermosillo", "3er Tribunal Laboral Hermosillo",
            }),
            ("Cajeme", new[]
            {
                "1ro Civil Cajeme", "2do Civil Cajeme", "3ro Civil Cajeme", "4to Civil Cajeme",
                "1ro Familiar Cajeme", "2do Familiar Cajeme", "3ro Familiar Cajeme",
                "Juzgado Especial Familiar Cajeme",
                "1ro Penal Cajeme", "Juzgado Oral Penal Cajeme",
                "2do Mixto Cajeme", "1ro Mixto Cajeme",
                "1er Tribunal Colegiado 2do Circuito",
                "Tribunal Laboral Cajeme",
            }),
            ("Agua Prieta", new[]
            {
                "Juzgado 1ro Civil/Mercantil/Penal Agua Prieta",
                "Juzgado 1ro Mixto Agua Prieta",
                "Juzgado Oral Penal Agua Prieta",
            }),
            ("Álamos", new[] { "Juzgado Mixto Álamos" }),
            ("Caborca", new[]
            {
                "Juzgado 1ro Civil Caborca",
                "Juzgado Oral Penal Caborca",
                "Juzgado Mixto Especializado Caborca",
                "1er Tribunal Colegiado Caborca",
            }),
            ("Cananea", new[] { "Juzgado Mixto Cananea", "Sala Oral Penal Cananea" }),
            ("Cumpas", new[] { "Juzgado Mixto Cumpas" }),
            ("Guaymas", new[]
            {
                "Juzgado 1ro Civil Guaymas",
                "Juzgado Civil/Familiar Especializado Guaymas",
                "Juzgado 1ro Familiar Guaymas",
                "Juzgado Oral Penal Guaymas",
                "Tribunal Laboral Guaymas",
            }),
            ("Huatabampo", new[]
            {
                "Juzgado 1ro Civil Huatabampo",
                "Juzgado 1ro Penal Huatabampo",
                "Sala Oral Penal Huatabampo",
            }),
            ("Magdalena", new[] { "Juzgado Mixto Magdalena" }),
            ("Navojoa", new[]
            {
                "Juzgado 1ro Civil Navojoa",
                "Juzgado 1ro Familiar Navojoa",
                "Juzgado Oral Penal Navojoa",
                "Tribunal Laboral Navojoa",
            }),
            ("Nogales", new[]
            {
                "Juzgado 1ro Civil Nogales",
                "Juzgado 1ro Familiar Nogales",
                "Juzgado 2do Familiar Nogales",
                "Juzgado Oral Penal Nogales",
                "Tribunal Laboral Nogales",
            }),
            ("Puerto Peñasco", new[]
            {
                "Juzgado 1ro Civil Puerto Peñasco",
                "Sala Oral Penal Puerto Peñasco",
                "Tribunal Laboral Puerto Peñasco",
                "Juzgado 1ro Penal Puerto Peñasco",
            }),
            ("Sahuaripa", new[] { "Juzgado Mixto Sahuaripa" }),
            ("San Luis Río Colorado", new[]
            {
                "Juzgado 1ro Civil San Luis Río Colorado",
                "Juzgado Familiar San Luis Río Colorado",
                "Juzgado Penal/Familiar San Luis Río Colorado",
                "Juzgado Oral Penal San Luis Río Colorado",
                "Tribunal Laboral San Luis Río Colorado",
            }),
            ("Ures", new[] { "Juzgado Mixto Ures" }),
        };

        foreach (var (nombreSede, juzgados) in catalogo)
        {
            var sede = await context.SedesCatalogo.FirstOrDefaultAsync(s => s.Nombre == nombreSede);
            if (sede == null)
            {
                sede = new SedeCatalogo { Nombre = nombreSede };
                context.SedesCatalogo.Add(sede);
                await context.SaveChangesAsync(); // necesita Id antes de agregar sus juzgados
            }

            foreach (var nombreJuzgado in juzgados)
            {
                var existe = await context.JuzgadosCatalogo
                    .AnyAsync(j => j.SedeId == sede.Id && j.Nombre == nombreJuzgado);
                if (!existe)
                    context.JuzgadosCatalogo.Add(new JuzgadoCatalogo { Nombre = nombreJuzgado, SedeId = sede.Id });
            }
        }

        await context.SaveChangesAsync();
    }
}