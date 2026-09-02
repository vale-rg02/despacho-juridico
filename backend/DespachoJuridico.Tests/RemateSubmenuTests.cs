using DespachoJuridico.API.Data;
using DespachoJuridico.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.Tests;

// DJ-76: Remate pasa de tener 1ra/2da/3ra Almoneda como etapas independientes a
// ser un submenú (EtapaCatalogo.EtapaPadreId). A diferencia de las pruebas de
// búsqueda (DJ-110), aquí sí hace falta ejecutar consultas reales — no solo
// traducir SQL — así que se usa el proveedor InMemory: nada de lo que prueba
// esta migración depende de una función específica de Postgres (ILIKE,
// unaccent), es CRUD simple sobre EtapaCatalogo/HistorialEtapa.
public class RemateSubmenuTests
{
    private static AppDbContext CrearContextoEnMemoria(string nombreBD)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nombreBD)
            .Options;
        return new AppDbContext(opciones);
    }

    private static async Task<(EtapaCatalogo Remate, EtapaCatalogo Primera, EtapaCatalogo Segunda, EtapaCatalogo Tercera)>
        SembrarCatalogoSinReparentar(AppDbContext context, string tipoJuicio)
    {
        // Simula el estado de una BD sembrada ANTES de DJ-76: las almonedas ya
        // existen como etapas de primer nivel (EtapaPadreId=null), igual que en
        // producción antes de correr la migración.
        var remate = new EtapaCatalogo { Nombre = "Remate", TipoJuicio = tipoJuicio, Orden = 13 };
        var primera = new EtapaCatalogo { Nombre = "1ra Almoneda", TipoJuicio = tipoJuicio, Orden = 13 };
        var segunda = new EtapaCatalogo { Nombre = "2da Almoneda", TipoJuicio = tipoJuicio, Orden = 14 };
        var tercera = new EtapaCatalogo { Nombre = "3ra Almoneda", TipoJuicio = tipoJuicio, Orden = 15 };

        context.EtapasCatalogo.AddRange(remate, primera, segunda, tercera);
        await context.SaveChangesAsync();

        return (remate, primera, segunda, tercera);
    }

    [Fact]
    public async Task MigrarAlmonedasBajoRemate_ReparentaLasTresAlmonedasExistentes()
    {
        using var context = CrearContextoEnMemoria(nameof(MigrarAlmonedasBajoRemate_ReparentaLasTresAlmonedasExistentes));
        var (remate, primera, segunda, tercera) = await SembrarCatalogoSinReparentar(context, "Hipotecario");

        await DbSeeder.MigrarAlmonedasBajoRemateAsync(context);

        Assert.Equal(remate.Id, (await context.EtapasCatalogo.FindAsync(primera.Id))!.EtapaPadreId);
        Assert.Equal(remate.Id, (await context.EtapasCatalogo.FindAsync(segunda.Id))!.EtapaPadreId);
        Assert.Equal(remate.Id, (await context.EtapasCatalogo.FindAsync(tercera.Id))!.EtapaPadreId);
        Assert.Null((await context.EtapasCatalogo.FindAsync(remate.Id))!.EtapaPadreId);
    }

    [Fact]
    public async Task MigrarAlmonedasBajoRemate_RespetaTipoJuicio_NoMezclaHipotecarioConOralMercantil()
    {
        using var context = CrearContextoEnMemoria(nameof(MigrarAlmonedasBajoRemate_RespetaTipoJuicio_NoMezclaHipotecarioConOralMercantil));
        var hipotecario = await SembrarCatalogoSinReparentar(context, "Hipotecario");
        var oralMercantil = await SembrarCatalogoSinReparentar(context, "Oral Mercantil");

        await DbSeeder.MigrarAlmonedasBajoRemateAsync(context);

        Assert.Equal(hipotecario.Remate.Id, (await context.EtapasCatalogo.FindAsync(hipotecario.Primera.Id))!.EtapaPadreId);
        Assert.Equal(oralMercantil.Remate.Id, (await context.EtapasCatalogo.FindAsync(oralMercantil.Primera.Id))!.EtapaPadreId);
        Assert.NotEqual(hipotecario.Remate.Id, oralMercantil.Remate.Id);
    }

    [Fact]
    public async Task MigrarAlmonedasBajoRemate_EsIdempotente_CorrerlaDosVecesNoCambiaNada()
    {
        using var context = CrearContextoEnMemoria(nameof(MigrarAlmonedasBajoRemate_EsIdempotente_CorrerlaDosVecesNoCambiaNada));
        var (remate, primera, _, _) = await SembrarCatalogoSinReparentar(context, "Hipotecario");

        await DbSeeder.MigrarAlmonedasBajoRemateAsync(context);
        await DbSeeder.MigrarAlmonedasBajoRemateAsync(context); // segunda corrida, como en cada arranque de la app

        Assert.Equal(remate.Id, (await context.EtapasCatalogo.FindAsync(primera.Id))!.EtapaPadreId);
    }

    [Theory]
    [InlineData("1ra Almoneda")]
    [InlineData("2da Almoneda")]
    [InlineData("3ra Almoneda")]
    public async Task RegistrarHistorialEtapa_ParaCadaAlmoneda_GuardaYLeeCorrectamenteTrasLaMigracion(string nombreAlmoneda)
    {
        // Cubre "registro nuevo de cada una de las 3 almonedas" del criterio de
        // aceptación: después de reparentar el catálogo, registrar una etapa nueva
        // contra la hoja específica (nunca contra "Remate" directo) debe seguir
        // guardando y leyendo el nombre correcto — HistorialEtapa nunca supo ni le
        // importó la jerarquía, por eso nada se rompe.
        using var context = CrearContextoEnMemoria($"{nameof(RegistrarHistorialEtapa_ParaCadaAlmoneda_GuardaYLeeCorrectamenteTrasLaMigracion)}_{nombreAlmoneda}");
        var (_, primera, segunda, tercera) = await SembrarCatalogoSinReparentar(context, "Hipotecario");
        await DbSeeder.MigrarAlmonedasBajoRemateAsync(context);

        var almonedaElegida = nombreAlmoneda switch
        {
            "1ra Almoneda" => primera,
            "2da Almoneda" => segunda,
            _ => tercera
        };

        var usuario = new Usuario { Nombre = "Mario Acedo", Email = "mario@despacho.com", PasswordHash = "x" };
        var expediente = new Expediente { NumeroExpediente = "1/2026", ParteDemandada = "Juan Pérez", CreadoPorId = 1 };
        context.Usuarios.Add(usuario);
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        context.HistorialEtapas.Add(new HistorialEtapa
        {
            ExpedienteId = expediente.Id,
            EtapaCatalogoId = almonedaElegida.Id,
            FechaInicio = DateTime.UtcNow,
            RegistradoPorId = usuario.Id
        });
        await context.SaveChangesAsync();

        var leida = await context.HistorialEtapas
            .Include(h => h.EtapaCatalogo)
            .SingleAsync(h => h.ExpedienteId == expediente.Id);

        Assert.Equal(nombreAlmoneda, leida.EtapaCatalogo!.Nombre);
        Assert.Equal(almonedaElegida.Id, leida.EtapaCatalogoId);
    }

    [Fact]
    public async Task HistorialEtapaExistente_AntesDeLaMigracion_SigueLeyendoseIgualDespues()
    {
        // El caso más importante del criterio de aceptación: un HistorialEtapa
        // creado con el modelo viejo (cuando la almoneda todavía era de primer
        // nivel) debe leerse exactamente igual después de reparentar el catálogo —
        // nunca se toca la fila de HistorialEtapa, solo la de EtapaCatalogo.
        using var context = CrearContextoEnMemoria(nameof(HistorialEtapaExistente_AntesDeLaMigracion_SigueLeyendoseIgualDespues));
        var (_, primera, _, _) = await SembrarCatalogoSinReparentar(context, "Hipotecario");

        var usuario = new Usuario { Nombre = "Mario Acedo", Email = "mario@despacho.com", PasswordHash = "x" };
        var expediente = new Expediente { NumeroExpediente = "1/2026", ParteDemandada = "Juan Pérez", CreadoPorId = 1 };
        context.Usuarios.Add(usuario);
        context.Expedientes.Add(expediente);
        await context.SaveChangesAsync();

        var historial = new HistorialEtapa
        {
            ExpedienteId = expediente.Id,
            EtapaCatalogoId = primera.Id,
            FechaInicio = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            RegistradoPorId = usuario.Id,
            Notas = "Registrado antes de DJ-76"
        };
        context.HistorialEtapas.Add(historial);
        await context.SaveChangesAsync();

        // Ahora se corre la migración — como pasaría al desplegar DJ-76 sobre
        // una BD que ya tenía este historial capturado.
        await DbSeeder.MigrarAlmonedasBajoRemateAsync(context);

        var leidaDespues = await context.HistorialEtapas
            .Include(h => h.EtapaCatalogo)
            .SingleAsync(h => h.Id == historial.Id);

        Assert.Equal("1ra Almoneda", leidaDespues.EtapaCatalogo!.Nombre);
        Assert.Equal(primera.Id, leidaDespues.EtapaCatalogoId);
        Assert.Equal("Registrado antes de DJ-76", leidaDespues.Notas);
    }

    [Fact]
    public async Task Catalogo_TrasLaMigracion_DistingueEtapasDePrimerNivelDeSubetapas()
    {
        // El submenú del frontend depende de poder filtrar por EtapaPadreId — esta
        // prueba confirma que la consulta que usaría GetAll separa correctamente
        // "Remate" (primer nivel) de sus 3 hijas, en el orden esperado.
        using var context = CrearContextoEnMemoria(nameof(Catalogo_TrasLaMigracion_DistingueEtapasDePrimerNivelDeSubetapas));
        var (remate, primera, segunda, tercera) = await SembrarCatalogoSinReparentar(context, "Hipotecario");
        await DbSeeder.MigrarAlmonedasBajoRemateAsync(context);

        var todas = await context.EtapasCatalogo.Where(e => e.TipoJuicio == "Hipotecario").OrderBy(e => e.Orden).ToListAsync();
        var primerNivel = todas.Where(e => e.EtapaPadreId == null).ToList();
        var hijasDeRemate = todas.Where(e => e.EtapaPadreId == remate.Id).OrderBy(e => e.Orden).ToList();

        Assert.Single(primerNivel);
        Assert.Equal("Remate", primerNivel[0].Nombre);

        Assert.Equal(3, hijasDeRemate.Count);
        Assert.Equal(["1ra Almoneda", "2da Almoneda", "3ra Almoneda"], hijasDeRemate.Select(e => e.Nombre));
    }
}
