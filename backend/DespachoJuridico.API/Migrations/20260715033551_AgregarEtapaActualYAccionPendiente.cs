using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEtapaActualYAccionPendiente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se usa IF NOT EXISTS porque algunos ambientes (incluida producción)
            // pudieron haber recibido estas columnas manualmente en el pasado,
            // sin que ninguna migración anterior las registrara (ver AppDbContextModelSnapshot
            // vs. el historial real de migraciones — la migración "SyncModel" quedó vacía).
            migrationBuilder.Sql(
                "ALTER TABLE \"Expedientes\" ADD COLUMN IF NOT EXISTS \"AccionPendiente\" text;");

            migrationBuilder.Sql(
                "ALTER TABLE \"Expedientes\" ADD COLUMN IF NOT EXISTS \"EtapaActual\" text;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"Expedientes\" DROP COLUMN IF EXISTS \"AccionPendiente\";");

            migrationBuilder.Sql(
                "ALTER TABLE \"Expedientes\" DROP COLUMN IF EXISTS \"EtapaActual\";");
        }
    }
}
