using DespachoJuridico.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DespachoJuridico.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Banco> Bancos => Set<Banco>();
    public DbSet<Expediente> Expedientes => Set<Expediente>();
    public DbSet<EtapaCatalogo> EtapasCatalogo => Set<EtapaCatalogo>();
    public DbSet<HistorialEtapa> HistorialEtapas => Set<HistorialEtapa>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<BitacoraCambio> BitacoraCambios => Set<BitacoraCambio>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Expediente tiene dos FKs a Usuario — hay que decirle a EF cuál es cuál
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

        // Enums guardados como string en la BD (más legible que números)
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
    }
}