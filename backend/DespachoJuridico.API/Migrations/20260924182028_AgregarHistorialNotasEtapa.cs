using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarHistorialNotasEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Notas",
                table: "HistorialEtapas",
                newName: "NotasLegado");

            migrationBuilder.CreateTable(
                name: "NotasEtapa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HistorialEtapaId = table.Column<int>(type: "integer", nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreadoPorId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasEtapa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasEtapa_HistorialEtapas_HistorialEtapaId",
                        column: x => x.HistorialEtapaId,
                        principalTable: "HistorialEtapas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotasEtapa_Usuarios_CreadoPorId",
                        column: x => x.CreadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotasEtapa_CreadoPorId",
                table: "NotasEtapa",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasEtapa_HistorialEtapaId",
                table: "NotasEtapa",
                column: "HistorialEtapaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotasEtapa");

            migrationBuilder.RenameColumn(
                name: "NotasLegado",
                table: "HistorialEtapas",
                newName: "Notas");
        }
    }
}
