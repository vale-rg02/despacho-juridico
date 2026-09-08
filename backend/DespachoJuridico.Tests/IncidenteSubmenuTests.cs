using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.Tests;

// DJ-121: "Incidente" pasa de ser una etapa plana (DJ-119) a un contenedor con
// submenú de 6 opciones — pero, a diferencia de Remate/Almonedas (secuencial y
// excluyente), los incidentes son trámites autónomos que pueden coexistir en el
// mismo expediente. Se confirmó que esto no requiere ningún cambio en
// SelectorEtapaCatalogo.jsx ni en RegistrarEtapa: ninguno de los dos impone
// exclusividad hoy, así que estas pruebas solo cubren el reparentado del
// catálogo y confirman (con datos reales de HistorialEtapa) que dos incidentes
// de tipos distintos conviven sin pisarse. InMemory por el mismo motivo que
// RemateSubmenuTests: hace falta ejecutar consultas reales sobre EtapaCatalogo.
public class IncidenteSubmenuTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    private static readonly string[] OpcionesEsperadas =
    {
        "Intereses Moratorios",
        "Intereses Ordinarios",
        "Intereses Ordinarios y Moratorios",
        "Liquidación de Costas",
        "Nulidad de emplazamiento",
        "Innominado"
    };

    private static async Task<EtapaCatalogo> SembrarIncidenteSinReparentar(AppDbContext context, string tipoJuicio)
    {
        // Simula el estado de una BD sembrada con DJ-119 pero sin DJ-121 todavía:
        // "Incidente" ya existe plano, las 6 opciones ya existen como filas de
        // primer nivel (recién sembradas, antes de que corra la migración).
        var incidente = new EtapaCatalogo { Nombre = "Incidente", TipoJuicio = tipoJuicio, Orden = 5 };
        context.EtapasCatalogo.Add(incidente);
        foreach (var (nombre, orden) in OpcionesEsperadas.Select((n, i) => (n, i + 1)))
            context.EtapasCatalogo.Add(new EtapaCatalogo { Nombre = nombre, TipoJuicio = tipoJuicio, Orden = orden });

        await context.SaveChangesAsync();
        return incidente;
    }

    [Theory]
    [InlineData("Hipotecario")]
    [InlineData("Oral Mercantil")]
    public async Task MigrarIncidenteASubmenu_ReparentaLasSeisOpciones(string tipoJuicio)
    {
        using var context = CrearContextoEnMemoria($"{nameof(MigrarIncidenteASubmenu_ReparentaLasSeisOpciones)}_{tipoJuicio}");
        var incidente = await SembrarIncidenteSinReparentar(context, tipoJuicio);

        await DbSeeder.MigrarIncidenteASubmenuAsync(context);

        var hijas = await context.EtapasCatalogo
            .Where(e => e.EtapaPadreId == incidente.Id)
            .Select(e => e.Nombre)
            .ToListAsync();

        Assert.Equal(6, hijas.Count);
        foreach (var nombre in OpcionesEsperadas)
            Assert.Contains(nombre, hijas);
        Assert.Null((await context.EtapasCatalogo.FindAsync(incidente.Id))!.EtapaPadreId);
    }

    [Fact]
    public async Task MigrarIncidenteASubmenu_RespetaTipoJuicio_NoMezclaHipotecarioConOralMercantil()
    {
        using var context = CrearContextoEnMemoria(nameof(MigrarIncidenteASubmenu_RespetaTipoJuicio_NoMezclaHipotecarioConOralMercantil));
        var hipotecario = await SembrarIncidenteSinReparentar(context, "Hipotecario");
        var oralMercantil = await SembrarIncidenteSinReparentar(context, "Oral Mercantil");

        await DbSeeder.MigrarIncidenteASubmenuAsync(context);

        var hijasHipotecario = await context.EtapasCatalogo.Where(e => e.EtapaPadreId == hipotecario.Id).CountAsync();
        var hijasOralMercantil = await context.EtapasCatalogo.Where(e => e.EtapaPadreId == oralMercantil.Id).CountAsync();

        Assert.Equal(6, hijasHipotecario);
        Assert.Equal(6, hijasOralMercantil);
        Assert.NotEqual(hipotecario.Id, oralMercantil.Id);
    }

    [Fact]
    public async Task MigrarIncidenteASubmenu_EsIdempotente_CorrerloDosVecesNoCambiaNada()
    {
        using var context = CrearContextoEnMemoria(nameof(MigrarIncidenteASubmenu_EsIdempotente_CorrerloDosVecesNoCambiaNada));
        var incidente = await SembrarIncidenteSinReparentar(context, "Hipotecario");

        await DbSeeder.MigrarIncidenteASubmenuAsync(context);
        await DbSeeder.MigrarIncidenteASubmenuAsync(context); // segunda corrida, como en cada arranque de la app

        var hijas = await context.EtapasCatalogo.Where(e => e.EtapaPadreId == incidente.Id).CountAsync();
        Assert.Equal(6, hijas);
    }

    [Fact]
    public async Task HistorialEtapaExistente_ApuntandoAIncidentePlano_SigueLeyendoseIgualDespuesDeReparentar()
    {
        // Caso real posible en producción (DJ-119 ya desplegado): si Mario ya
        // registró un "Incidente" con el modelo plano antes de este cambio, ese
        // HistorialEtapa debe seguir siendo válido y legible después — no se
        // reclasifica a ninguna de las 6 opciones nuevas sin confirmación de Mario.
        using var context = CrearContextoEnMemoria(nameof(HistorialEtapaExistente_ApuntandoAIncidentePlano_SigueLeyendoseIgualDespuesDeReparentar));
        var incidente = await SembrarIncidenteSinReparentar(context, "Hipotecario");

        var usuario = new Usuario { Nombre = "Mario Acedo", Email = "mario@despacho.com", PasswordHash = "x" };
        var expediente = new Expediente { NumeroExpediente = "1/2026", ParteDemandada = "Juan Pérez", TipoJuicio = "Hipotecario", CreadoPorId = 1 };
        context.Usuarios.Add(usuario);
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        var historial = new HistorialEtapa
        {
            ExpedienteId = expediente.Id,
            EtapaCatalogoId = incidente.Id,
            FechaInicio = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc),
            RegistradoPorId = usuario.Id,
            Notas = "Registrado antes de DJ-121, con el modelo plano"
        };
        context.HistorialEtapas.Add(historial);
        await context.SaveChangesAsync();

        await DbSeeder.MigrarIncidenteASubmenuAsync(context);

        var leidaDespues = await context.HistorialEtapas
            .Include(h => h.EtapaCatalogo)
            .SingleAsync(h => h.Id == historial.Id);

        Assert.Equal("Incidente", leidaDespues.EtapaCatalogo!.Nombre);
        Assert.Equal(incidente.Id, leidaDespues.EtapaCatalogoId);
        Assert.Equal("Registrado antes de DJ-121, con el modelo plano", leidaDespues.Notas);
    }

    [Fact]
    public async Task DosIncidentesDeTiposDistintos_CoexistenEnElHistorialSinPisarse()
    {
        // El criterio de aceptación central: registrar un segundo incidente de
        // tipo distinto no reemplaza ni oculta el primero — ambos quedan visibles.
        using var context = CrearContextoEnMemoria(nameof(DosIncidentesDeTiposDistintos_CoexistenEnElHistorialSinPisarse));
        var incidente = await SembrarIncidenteSinReparentar(context, "Hipotecario");
        await DbSeeder.MigrarIncidenteASubmenuAsync(context);

        var interesesMoratorios = await context.EtapasCatalogo.SingleAsync(e => e.Nombre == "Intereses Moratorios" && e.EtapaPadreId == incidente.Id);
        var nulidadEmplazamiento = await context.EtapasCatalogo.SingleAsync(e => e.Nombre == "Nulidad de emplazamiento" && e.EtapaPadreId == incidente.Id);

        var usuario = new Usuario { Nombre = "Mario Acedo", Email = "mario@despacho.com", PasswordHash = "x" };
        var expediente = new Expediente { NumeroExpediente = "1/2026", ParteDemandada = "Juan Pérez", TipoJuicio = "Hipotecario", CreadoPorId = 1 };
        context.Usuarios.Add(usuario);
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        // Dos registros independientes, exactamente como los haría RegistrarEtapa
        // (siempre inserta, nunca busca ni reemplaza uno existente).
        context.HistorialEtapas.Add(new HistorialEtapa
        {
            ExpedienteId = expediente.Id,
            EtapaCatalogoId = interesesMoratorios.Id,
            FechaInicio = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            RegistradoPorId = usuario.Id
        });
        context.HistorialEtapas.Add(new HistorialEtapa
        {
            ExpedienteId = expediente.Id,
            EtapaCatalogoId = nulidadEmplazamiento.Id,
            FechaInicio = new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc),
            RegistradoPorId = usuario.Id
        });
        await context.SaveChangesAsync();

        var historialCompleto = await context.HistorialEtapas
            .Include(h => h.EtapaCatalogo)
            .Where(h => h.ExpedienteId == expediente.Id)
            .OrderByDescending(h => h.FechaInicio)
            .ToListAsync();

        Assert.Equal(2, historialCompleto.Count);
        Assert.Contains(historialCompleto, h => h.EtapaCatalogo!.Nombre == "Intereses Moratorios");
        Assert.Contains(historialCompleto, h => h.EtapaCatalogo!.Nombre == "Nulidad de emplazamiento");
    }

    [Fact]
    public async Task TresIncidentesDeIntereses_ConvivenSinExcluirseComoAlmonedasSi()
    {
        // Refuerza la diferencia explícita del ticket: "Intereses Moratorios",
        // "Intereses Ordinarios" e "Intereses Ordinarios y Moratorios" no son una
        // secuencia donde uno reemplaza al otro — las tres pueden registrarse para
        // el mismo expediente.
        using var context = CrearContextoEnMemoria(nameof(TresIncidentesDeIntereses_ConvivenSinExcluirseComoAlmonedasSi));
        var incidente = await SembrarIncidenteSinReparentar(context, "Oral Mercantil");
        await DbSeeder.MigrarIncidenteASubmenuAsync(context);

        var opciones = await context.EtapasCatalogo
            .Where(e => e.EtapaPadreId == incidente.Id && e.Nombre.StartsWith("Intereses"))
            .ToListAsync();
        Assert.Equal(3, opciones.Count);

        var usuario = new Usuario { Nombre = "Mario Acedo", Email = "mario@despacho.com", PasswordHash = "x" };
        var expediente = new Expediente { NumeroExpediente = "2/2026", ParteDemandada = "Ana López", TipoJuicio = "Oral Mercantil", CreadoPorId = 1 };
        context.Usuarios.Add(usuario);
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        foreach (var opcion in opciones)
        {
            context.HistorialEtapas.Add(new HistorialEtapa
            {
                ExpedienteId = expediente.Id,
                EtapaCatalogoId = opcion.Id,
                FechaInicio = DateTime.UtcNow,
                RegistradoPorId = usuario.Id
            });
        }
        await context.SaveChangesAsync();

        var totalRegistrado = await context.HistorialEtapas.CountAsync(h => h.ExpedienteId == expediente.Id);
        Assert.Equal(3, totalRegistrado);
    }

    [Fact]
    public async Task MigrarIncidenteASubmenu_NoAfectaElReparentadoDeRemate_SinRegresion()
    {
        // Confirma que agregar el submenú de Incidente no toca ni interfiere con
        // el submenú de Remate ya existente (DJ-76) — deben convivir en el mismo
        // catálogo sin cruzarse.
        using var context = CrearContextoEnMemoria(nameof(MigrarIncidenteASubmenu_NoAfectaElReparentadoDeRemate_SinRegresion));

        var remate = new EtapaCatalogo { Nombre = "Remate", TipoJuicio = "Hipotecario", Orden = 13 };
        var primeraAlmoneda = new EtapaCatalogo { Nombre = "1ra Almoneda", TipoJuicio = "Hipotecario", Orden = 13 };
        context.EtapasCatalogo.AddRange(remate, primeraAlmoneda);
        await context.SaveChangesAsync();

        var incidente = await SembrarIncidenteSinReparentar(context, "Hipotecario");

        await DbSeeder.MigrarAlmonedasBajoRemateAsync(context);
        await DbSeeder.MigrarIncidenteASubmenuAsync(context);

        Assert.Equal(remate.Id, (await context.EtapasCatalogo.FindAsync(primeraAlmoneda.Id))!.EtapaPadreId);
        var hijasDeIncidente = await context.EtapasCatalogo.Where(e => e.EtapaPadreId == incidente.Id).CountAsync();
        Assert.Equal(6, hijasDeIncidente);

        // Remate sigue siendo excluyente/secuencial en su propio uso real: nada
        // de este cambio le agrega ni le quita nada a su comportamiento.
        var hijasDeRemate = await context.EtapasCatalogo.Where(e => e.EtapaPadreId == remate.Id).ToListAsync();
        Assert.Single(hijasDeRemate);
        Assert.Equal("1ra Almoneda", hijasDeRemate[0].Nombre);
    }
}
