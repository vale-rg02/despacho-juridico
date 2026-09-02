using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.Tests;

// DJ-78: "Término" y "Término para Amparo" quedaron en el catálogo con
// TipoJuicio=NULL desde la importación masiva del Excel original
// (Data/MigracionExcel.cs crea la fila del catálogo solo por nombre, sin tipo
// de juicio). MigrarTerminoATipoJuicioAsync reasigna cada HistorialEtapa que
// apunte a esa fila huérfana hacia la fila correcta por TipoJuicio (usando el
// TipoJuicio real del expediente dueño de ese historial) y borra la huérfana
// una vez que ya nadie la referencia. Igual que en DJ-76 (RemateSubmenuTests),
// se usa InMemory porque hace falta ejecutar consultas reales, no solo
// traducir SQL.
public class TerminoTipoJuicioTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    private static async Task<Usuario> SembrarUsuarioAsync(AppDbContext context)
    {
        var usuario = new Usuario { Nombre = "Mario Acedo", Email = $"{Guid.NewGuid()}@despacho.com", PasswordHash = "x" };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();
        return usuario;
    }

    private static async Task<Expediente> SembrarExpedienteAsync(AppDbContext context, string numero, string? tipoJuicio, int creadoPorId)
    {
        var expediente = new Expediente
        {
            NumeroExpediente = numero,
            ParteDemandada = "Parte de Prueba",
            TipoJuicio = tipoJuicio,
            CreadoPorId = creadoPorId
        };
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();
        return expediente;
    }

    [Fact]
    public async Task MigrarTermino_ReasignaHistorialSegunTipoJuicioDelExpediente()
    {
        // Simula el estado real encontrado en producción: una fila huérfana de
        // "Término" (TipoJuicio=NULL), y las dos filas destino ya sembradas por
        // SeedEtapasCatalogoAsync (Hipotecario y Oral Mercantil), como pasaría en
        // una BD ya sembrada antes de correr esta migración.
        using var context = CrearContextoEnMemoria(nameof(MigrarTermino_ReasignaHistorialSegunTipoJuicioDelExpediente));
        var usuario = await SembrarUsuarioAsync(context);

        var huerfana = new EtapaCatalogo { Nombre = "Término", TipoJuicio = null, Orden = 3 };
        var destinoHipotecario = new EtapaCatalogo { Nombre = "Término", TipoJuicio = "Hipotecario", Orden = 3 };
        var destinoOralMercantil = new EtapaCatalogo { Nombre = "Término", TipoJuicio = "Oral Mercantil", Orden = 3 };
        context.EtapasCatalogo.AddRange(huerfana, destinoHipotecario, destinoOralMercantil);
        await context.SaveChangesAsync();

        var expHipotecario = await SembrarExpedienteAsync(context, "1/2026", "Hipotecario", usuario.Id);
        var expOralMercantil = await SembrarExpedienteAsync(context, "2/2026", "Oral Mercantil", usuario.Id);

        var historialHipotecario = new HistorialEtapa { ExpedienteId = expHipotecario.Id, EtapaCatalogoId = huerfana.Id, FechaInicio = DateTime.UtcNow, RegistradoPorId = usuario.Id };
        var historialOralMercantil = new HistorialEtapa { ExpedienteId = expOralMercantil.Id, EtapaCatalogoId = huerfana.Id, FechaInicio = DateTime.UtcNow, RegistradoPorId = usuario.Id };
        context.HistorialEtapas.AddRange(historialHipotecario, historialOralMercantil);
        await context.SaveChangesAsync();

        await DbSeeder.MigrarTerminoATipoJuicioAsync(context);

        Assert.Equal(destinoHipotecario.Id, (await context.HistorialEtapas.FindAsync(historialHipotecario.Id))!.EtapaCatalogoId);
        Assert.Equal(destinoOralMercantil.Id, (await context.HistorialEtapas.FindAsync(historialOralMercantil.Id))!.EtapaCatalogoId);
    }

    [Fact]
    public async Task MigrarTermino_BorraLaFilaHuerfanaCuandoYaNadieLaReferencia()
    {
        using var context = CrearContextoEnMemoria(nameof(MigrarTermino_BorraLaFilaHuerfanaCuandoYaNadieLaReferencia));
        var usuario = await SembrarUsuarioAsync(context);

        var huerfana = new EtapaCatalogo { Nombre = "Término", TipoJuicio = null, Orden = 3 };
        var destino = new EtapaCatalogo { Nombre = "Término", TipoJuicio = "Hipotecario", Orden = 3 };
        context.EtapasCatalogo.AddRange(huerfana, destino);
        await context.SaveChangesAsync();

        var expediente = await SembrarExpedienteAsync(context, "1/2026", "Hipotecario", usuario.Id);
        context.HistorialEtapas.Add(new HistorialEtapa { ExpedienteId = expediente.Id, EtapaCatalogoId = huerfana.Id, FechaInicio = DateTime.UtcNow, RegistradoPorId = usuario.Id });
        await context.SaveChangesAsync();

        await DbSeeder.MigrarTerminoATipoJuicioAsync(context);

        Assert.Null(await context.EtapasCatalogo.FindAsync(huerfana.Id));
    }

    [Fact]
    public async Task MigrarTermino_EsIdempotente_CorrerlaDosVecesNoRompeNiDuplicaCambios()
    {
        using var context = CrearContextoEnMemoria(nameof(MigrarTermino_EsIdempotente_CorrerlaDosVecesNoRompeNiDuplicaCambios));
        var usuario = await SembrarUsuarioAsync(context);

        var huerfana = new EtapaCatalogo { Nombre = "Término", TipoJuicio = null, Orden = 3 };
        var destino = new EtapaCatalogo { Nombre = "Término", TipoJuicio = "Hipotecario", Orden = 3 };
        context.EtapasCatalogo.AddRange(huerfana, destino);
        await context.SaveChangesAsync();

        var expediente = await SembrarExpedienteAsync(context, "1/2026", "Hipotecario", usuario.Id);
        var historial = new HistorialEtapa { ExpedienteId = expediente.Id, EtapaCatalogoId = huerfana.Id, FechaInicio = DateTime.UtcNow, RegistradoPorId = usuario.Id };
        context.HistorialEtapas.Add(historial);
        await context.SaveChangesAsync();

        await DbSeeder.MigrarTerminoATipoJuicioAsync(context);
        await DbSeeder.MigrarTerminoATipoJuicioAsync(context); // segunda corrida, como en cada arranque de la app

        Assert.Equal(destino.Id, (await context.HistorialEtapas.FindAsync(historial.Id))!.EtapaCatalogoId);
        Assert.Null(await context.EtapasCatalogo.FindAsync(huerfana.Id));
    }

    [Fact]
    public async Task HistorialEtapaExistente_AntesDeLaMigracion_SigueLeyendoseIgualDespues()
    {
        // El caso más importante del criterio de aceptación (igual que DJ-76): un
        // HistorialEtapa creado con el modelo viejo (apuntando a la fila huérfana)
        // debe leerse exactamente igual después de la migración — mismo nombre de
        // etapa, mismas notas — solo con el TipoJuicio correcto detrás.
        using var context = CrearContextoEnMemoria(nameof(HistorialEtapaExistente_AntesDeLaMigracion_SigueLeyendoseIgualDespues));
        var usuario = await SembrarUsuarioAsync(context);

        var huerfana = new EtapaCatalogo { Nombre = "Término para Amparo", TipoJuicio = null, Orden = 9 };
        var destino = new EtapaCatalogo { Nombre = "Término para Amparo", TipoJuicio = "Oral Mercantil", Orden = 9 };
        context.EtapasCatalogo.AddRange(huerfana, destino);
        await context.SaveChangesAsync();

        var expediente = await SembrarExpedienteAsync(context, "586/2020", "Oral Mercantil", usuario.Id);
        var historial = new HistorialEtapa
        {
            ExpedienteId = expediente.Id,
            EtapaCatalogoId = huerfana.Id,
            FechaInicio = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            RegistradoPorId = usuario.Id,
            Notas = "Importado del Excel original"
        };
        context.HistorialEtapas.Add(historial);
        await context.SaveChangesAsync();

        await DbSeeder.MigrarTerminoATipoJuicioAsync(context);

        var leidaDespues = await context.HistorialEtapas
            .Include(h => h.EtapaCatalogo)
            .SingleAsync(h => h.Id == historial.Id);

        Assert.Equal("Término para Amparo", leidaDespues.EtapaCatalogo!.Nombre);
        Assert.Equal("Oral Mercantil", leidaDespues.EtapaCatalogo!.TipoJuicio);
        Assert.Equal(destino.Id, leidaDespues.EtapaCatalogoId);
        Assert.Equal("Importado del Excel original", leidaDespues.Notas);
    }

    [Fact]
    public async Task MigrarTermino_TerminoParaAmparo_NuncaSeReasignaAHipotecario()
    {
        // Caso real que motivó no asumir: a diferencia de "Término" (que sí se usa
        // en ambos tipos), "Término para Amparo" solo tiene evidencia real en Oral
        // Mercantil (141 de 141 en producción) — si por error un expediente
        // Hipotecario tuviera un historial huérfano de "Término para Amparo" (no
        // hay evidencia de que exista, pero si apareciera), no debe inventarse una
        // fila para Hipotecario ni reasignarse a la de Oral Mercantil por error.
        using var context = CrearContextoEnMemoria(nameof(MigrarTermino_TerminoParaAmparo_NuncaSeReasignaAHipotecario));
        var usuario = await SembrarUsuarioAsync(context);

        var huerfana = new EtapaCatalogo { Nombre = "Término para Amparo", TipoJuicio = null, Orden = 9 };
        var destinoOralMercantil = new EtapaCatalogo { Nombre = "Término para Amparo", TipoJuicio = "Oral Mercantil", Orden = 9 };
        context.EtapasCatalogo.AddRange(huerfana, destinoOralMercantil);
        await context.SaveChangesAsync();

        var expHipotecario = await SembrarExpedienteAsync(context, "1/2026", "Hipotecario", usuario.Id);
        var historial = new HistorialEtapa { ExpedienteId = expHipotecario.Id, EtapaCatalogoId = huerfana.Id, FechaInicio = DateTime.UtcNow, RegistradoPorId = usuario.Id };
        context.HistorialEtapas.Add(historial);
        await context.SaveChangesAsync();

        await DbSeeder.MigrarTerminoATipoJuicioAsync(context);

        // Sin fila destino para Hipotecario, se deja intacta para revisión manual
        // en vez de adivinar — la huérfana tampoco se borra porque todavía la referencian.
        Assert.Equal(huerfana.Id, (await context.HistorialEtapas.FindAsync(historial.Id))!.EtapaCatalogoId);
        Assert.NotNull(await context.EtapasCatalogo.FindAsync(huerfana.Id));
    }

    [Fact]
    public async Task MigrarTermino_ExpedienteSinTipoJuicio_NoRompeYDejaHuerfanaIntacta()
    {
        // Caso real posible: expedientes importados con Materia vacía nunca
        // recibieron TipoJuicio (ver comentario en la migración
        // CorregirTipoJuicioExpedientesImportados) — sin TipoJuicio no hay a dónde
        // reasignar, así que debe quedar intacto para revisión manual, no crashear
        // ni borrarse la huérfana mientras algo la siga referenciando.
        using var context = CrearContextoEnMemoria(nameof(MigrarTermino_ExpedienteSinTipoJuicio_NoRompeYDejaHuerfanaIntacta));
        var usuario = await SembrarUsuarioAsync(context);

        var huerfana = new EtapaCatalogo { Nombre = "Término", TipoJuicio = null, Orden = 3 };
        context.EtapasCatalogo.Add(huerfana);
        await context.SaveChangesAsync();

        var expedienteSinTipo = await SembrarExpedienteAsync(context, "000000", null, usuario.Id);
        var historial = new HistorialEtapa { ExpedienteId = expedienteSinTipo.Id, EtapaCatalogoId = huerfana.Id, FechaInicio = DateTime.UtcNow, RegistradoPorId = usuario.Id };
        context.HistorialEtapas.Add(historial);
        await context.SaveChangesAsync();

        await DbSeeder.MigrarTerminoATipoJuicioAsync(context);

        Assert.Equal(huerfana.Id, (await context.HistorialEtapas.FindAsync(historial.Id))!.EtapaCatalogoId);
        Assert.NotNull(await context.EtapasCatalogo.FindAsync(huerfana.Id));
    }

    [Fact]
    public async Task MigrarTermino_SinFilasHuerfanas_NoHaceNada()
    {
        // Si ya se migró antes (o nunca hubo huérfanas), correrla de nuevo no debe
        // fallar ni tocar nada — se ejecuta en cada arranque de la app.
        using var context = CrearContextoEnMemoria(nameof(MigrarTermino_SinFilasHuerfanas_NoHaceNada));
        var destino = new EtapaCatalogo { Nombre = "Término", TipoJuicio = "Hipotecario", Orden = 3 };
        context.EtapasCatalogo.Add(destino);
        await context.SaveChangesAsync();

        await DbSeeder.MigrarTerminoATipoJuicioAsync(context);

        Assert.Single(context.EtapasCatalogo);
    }
}
