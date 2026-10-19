using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRevisionCandidatoFraseGenerica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RevisionesCandidatoFraseGenerica",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidatoNormalizado = table.Column<string>(type: "text", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: true),
                    RevisadoPorId = table.Column<int>(type: "integer", nullable: false),
                    RevisadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionesCandidatoFraseGenerica", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevisionesCandidatoFraseGenerica_Usuarios_RevisadoPorId",
                        column: x => x.RevisadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RevisionesCandidatoFraseGenerica_CandidatoNormalizado",
                table: "RevisionesCandidatoFraseGenerica",
                column: "CandidatoNormalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevisionesCandidatoFraseGenerica_RevisadoPorId",
                table: "RevisionesCandidatoFraseGenerica",
                column: "RevisadoPorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RevisionesCandidatoFraseGenerica");
        }
    }
}
