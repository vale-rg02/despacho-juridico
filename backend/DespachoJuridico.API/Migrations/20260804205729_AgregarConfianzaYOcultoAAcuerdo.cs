using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfianzaYOcultoAAcuerdo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Confianza",
                table: "AcuerdosScrapeados",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Oculto",
                table: "AcuerdosScrapeados",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confianza",
                table: "AcuerdosScrapeados");

            migrationBuilder.DropColumn(
                name: "Oculto",
                table: "AcuerdosScrapeados");
        }
    }
}
