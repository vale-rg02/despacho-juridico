using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class VistoPorUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Visto",
                table: "AcuerdosScrapeados");

            migrationBuilder.CreateTable(
                name: "AcuerdoVistoPorUsuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcuerdoId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    FechaVisto = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcuerdoVistoPorUsuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcuerdoVistoPorUsuarios_AcuerdosScrapeados_AcuerdoId",
                        column: x => x.AcuerdoId,
                        principalTable: "AcuerdosScrapeados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AcuerdoVistoPorUsuarios_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdoVistoPorUsuarios_AcuerdoId_UsuarioId",
                table: "AcuerdoVistoPorUsuarios",
                columns: new[] { "AcuerdoId", "UsuarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdoVistoPorUsuarios_UsuarioId",
                table: "AcuerdoVistoPorUsuarios",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcuerdoVistoPorUsuarios");

            migrationBuilder.AddColumn<bool>(
                name: "Visto",
                table: "AcuerdosScrapeados",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
