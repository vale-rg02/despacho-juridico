using DespachoJuridico.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Mapea la función unaccent() de Postgres (extensión "unaccent") — permite que
    // una búsqueda por "Mexico" encuentre "México" y viceversa. Solo se puede usar
    // dentro de una expresión LINQ traducida por EF Core (ver
    // ExpedientesController.AplicarFiltroBusqueda); llamarla fuera de una consulta
    // lanza NotSupportedException a propósito.
    [DbFunction("unaccent", IsBuiltIn = true)]
    public static string Unaccent(string texto) => throw new NotSupportedException();

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Banco> Bancos => Set<Banco>();
    public DbSet<Expediente> Expedientes => Set<Expediente>();
    public DbSet<EtapaCatalogo> EtapasCatalogo => Set<EtapaCatalogo>();
    public DbSet<HistorialEtapa> HistorialEtapas => Set<HistorialEtapa>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<BitacoraCambio> BitacoraCambios => Set<BitacoraCambio>();
    public DbSet<AcuerdoScrapeado> AcuerdosScrapeados { get; set; }
    public DbSet<Cita> Citas => Set<Cita>();
    public DbSet<ExpedienteAcceso> ExpedienteAccesos => Set<ExpedienteAcceso>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Expediente tiene dos FKs a Usuario � hay que decirle a EF cu�l es cu�l
        modelBuilder.Entity<Expediente>()
            .HasOne(e => e.UsuarioAsignado)
            .WithMany(u => u.ExpedientesAsignados)
            .HasForeignKey(e => e.UsuarioAsignadoId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Expediente>()
            .HasOne(e => e.CreadoPor)
            .WithMany(u => u.ExpedientesCreados)
            .HasForeignKey(e => e.CreadoPorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Autoreferencia (expediente relacionado)
        modelBuilder.Entity<Expediente>()
            .HasOne(e => e.ExpedienteRelacionado)
            .WithMany()
            .HasForeignKey(e => e.ExpedienteRelacionadoId)
            .OnDelete(DeleteBehavior.SetNull);

        // Enums guardados como string en la BD (m�s legible que n�meros)
        modelBuilder.Entity<Expediente>()
            .Property(e => e.Estado)
            .HasConversion<string>();

        modelBuilder.Entity<Expediente>()
            .Property(e => e.Prioridad)
            .HasConversion<string>();

        modelBuilder.Entity<Usuario>()
            .Property(u => u.Rol)
            .HasConversion<string>();

        modelBuilder.Entity<Notificacion>()
            .Property(n => n.Canal)
            .HasConversion<string>();

        // Evita duplicados a nivel de BD si el cron y un trigger manual llegaran a solaparse
        modelBuilder.Entity<AcuerdoScrapeado>()
            .HasIndex(a => new { a.ExpedienteId, a.FechaAcuerdo, a.Sintesis })
            .IsUnique();

        // Si se elimina el expediente, la cita se conserva sin vincular (no se borra la cita)
        modelBuilder.Entity<Cita>()
            .HasOne(c => c.Expediente)
            .WithMany()
            .HasForeignKey(c => c.ExpedienteId)
            .OnDelete(DeleteBehavior.SetNull);

        // Sin esto quedaba en NO ACTION por default: no se podía eliminar una etapa
        // que ya hubiera generado notificaciones. Se conserva el historial de la
        // notificación, solo se desvincula de la etapa eliminada.
        modelBuilder.Entity<Notificacion>()
            .HasOne(n => n.HistorialEtapa)
            .WithMany(h => h.Notificaciones)
            .HasForeignKey(n => n.HistorialEtapaId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ExpedienteAcceso>()
            .HasOne(a => a.Expediente)
            .WithMany(e => e.Accesos)
            .HasForeignKey(a => a.ExpedienteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExpedienteAcceso>()
            .HasOne(a => a.Usuario)
            .WithMany()
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Evita agregar al mismo usuario dos veces como colaborador del mismo expediente
        modelBuilder.Entity<ExpedienteAcceso>()
            .HasIndex(a => new { a.ExpedienteId, a.UsuarioId })
            .IsUnique();
    }
}