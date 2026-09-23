using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSedeYCatalogosSedeJuzgado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sede",
                table: "Expedientes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SedesCatalogo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SedesCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JuzgadosCatalogo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    SedeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JuzgadosCatalogo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JuzgadosCatalogo_SedesCatalogo_SedeId",
                        column: x => x.SedeId,
                        principalTable: "SedesCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JuzgadosCatalogo_SedeId_Nombre",
                table: "JuzgadosCatalogo",
                columns: new[] { "SedeId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SedesCatalogo_Nombre",
                table: "SedesCatalogo",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JuzgadosCatalogo");

            migrationBuilder.DropTable(
                name: "SedesCatalogo");

            migrationBuilder.DropColumn(
                name: "Sede",
                table: "Expedientes");
        }
    }
}
