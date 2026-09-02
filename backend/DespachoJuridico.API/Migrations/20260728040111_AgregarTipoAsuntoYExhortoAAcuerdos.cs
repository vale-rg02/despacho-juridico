using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTipoAsuntoYExhortoAAcuerdos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CiudadDestino",
                table: "AcuerdosScrapeados",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EsExhorto",
                table: "AcuerdosScrapeados",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TipoAsunto",
                table: "AcuerdosScrapeados",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CiudadDestino",
                table: "AcuerdosScrapeados");

            migrationBuilder.DropColumn(
                name: "EsExhorto",
                table: "AcuerdosScrapeados");

            migrationBuilder.DropColumn(
                name: "TipoAsunto",
                table: "AcuerdosScrapeados");
        }
    }
}
