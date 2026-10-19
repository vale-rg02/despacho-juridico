using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarNotificacionOmitidaYAlertaReintento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AlertaReintentoEnviada",
                table: "AcuerdosScrapeados",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotificacionOmitida",
                table: "AcuerdosScrapeados",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // DJ-141: backfill de los 4 acuerdos que ya se promovieron con
            // notificar=false antes de que existiera este campo (recuperación de
            // los 8 S1 de la revisión retroactiva) -- sin esto, ReintentoNotificacionesService
            // les mandaría un correo tardío "nuevo acuerdo" al litigante, justo lo que
            // se quería evitar al usar notificar=false en primer lugar.
            migrationBuilder.Sql(@"
                UPDATE ""AcuerdosScrapeados""
                SET ""NotificacionOmitida"" = true
                WHERE ""Id"" IN (3746, 5059, 4586, 3189);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlertaReintentoEnviada",
                table: "AcuerdosScrapeados");

            migrationBuilder.DropColumn(
                name: "NotificacionOmitida",
                table: "AcuerdosScrapeados");
        }
    }
}
