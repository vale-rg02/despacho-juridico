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
            migrationBuilder.AddColumn<string>(
                name: "AccionPendiente",
                table: "Expedientes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EtapaActual",
                table: "Expedientes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccionPendiente",
                table: "Expedientes");

            migrationBuilder.DropColumn(
                name: "EtapaActual",
                table: "Expedientes");
        }
    }
}
