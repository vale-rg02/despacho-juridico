using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class CorregirOnDeleteNotificacionHistorialEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notificaciones_HistorialEtapas_HistorialEtapaId",
                table: "Notificaciones");

            migrationBuilder.AddForeignKey(
                name: "FK_Notificaciones_HistorialEtapas_HistorialEtapaId",
                table: "Notificaciones",
                column: "HistorialEtapaId",
                principalTable: "HistorialEtapas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notificaciones_HistorialEtapas_HistorialEtapaId",
                table: "Notificaciones");

            migrationBuilder.AddForeignKey(
                name: "FK_Notificaciones_HistorialEtapas_HistorialEtapaId",
                table: "Notificaciones",
                column: "HistorialEtapaId",
                principalTable: "HistorialEtapas",
                principalColumn: "Id");
        }
    }
}
