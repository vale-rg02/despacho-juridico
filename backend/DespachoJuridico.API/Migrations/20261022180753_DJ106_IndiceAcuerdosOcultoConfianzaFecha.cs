using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class DJ106_IndiceAcuerdosOcultoConfianzaFecha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AcuerdosScrapeados_Oculto_Confianza_FechaDetectado",
                table: "AcuerdosScrapeados",
                columns: new[] { "Oculto", "Confianza", "FechaDetectado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AcuerdosScrapeados_Oculto_Confianza_FechaDetectado",
                table: "AcuerdosScrapeados");
        }
    }
}
