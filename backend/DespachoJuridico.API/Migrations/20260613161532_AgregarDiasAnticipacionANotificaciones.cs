using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDiasAnticipacionANotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiasAnticipacion",
                table: "Notificaciones",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiasAnticipacion",
                table: "Notificaciones");
        }
    }
}
