using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarJerarquiaEtapaCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EtapaPadreId",
                table: "EtapasCatalogo",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EtapasCatalogo_EtapaPadreId",
                table: "EtapasCatalogo",
                column: "EtapaPadreId");

            migrationBuilder.AddForeignKey(
                name: "FK_EtapasCatalogo_EtapasCatalogo_EtapaPadreId",
                table: "EtapasCatalogo",
                column: "EtapaPadreId",
                principalTable: "EtapasCatalogo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EtapasCatalogo_EtapasCatalogo_EtapaPadreId",
                table: "EtapasCatalogo");

            migrationBuilder.DropIndex(
                name: "IX_EtapasCatalogo_EtapaPadreId",
                table: "EtapasCatalogo");

            migrationBuilder.DropColumn(
                name: "EtapaPadreId",
                table: "EtapasCatalogo");
        }
    }
}
