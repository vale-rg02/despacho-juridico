using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRecordatorioPendienteEnviadoAAcuerdo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RecordatorioPendienteEnviado",
                table: "AcuerdosScrapeados",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordatorioPendienteEnviado",
                table: "AcuerdosScrapeados");
        }
    }
}
