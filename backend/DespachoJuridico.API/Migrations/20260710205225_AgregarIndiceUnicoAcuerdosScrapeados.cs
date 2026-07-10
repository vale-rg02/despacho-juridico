using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIndiceUnicoAcuerdosScrapeados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AcuerdosScrapeados_ExpedienteId",
                table: "AcuerdosScrapeados");

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdosScrapeados_ExpedienteId_FechaAcuerdo_Sintesis",
                table: "AcuerdosScrapeados",
                columns: new[] { "ExpedienteId", "FechaAcuerdo", "Sintesis" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AcuerdosScrapeados_ExpedienteId_FechaAcuerdo_Sintesis",
                table: "AcuerdosScrapeados");

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdosScrapeados_ExpedienteId",
                table: "AcuerdosScrapeados",
                column: "ExpedienteId");
        }
    }
}
