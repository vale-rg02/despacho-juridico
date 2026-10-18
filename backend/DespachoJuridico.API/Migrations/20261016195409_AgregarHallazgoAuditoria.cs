using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarHallazgoAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoReporteAuditoriaEnviadoEn",
                table: "EstadosScraper",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HallazgosAuditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Severidad = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    NumeroExpediente = table.Column<string>(type: "text", nullable: true),
                    NombreJuzgado = table.Column<string>(type: "text", nullable: true),
                    RegistradoPorId = table.Column<int>(type: "integer", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallazgosAuditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HallazgosAuditoria_Usuarios_RegistradoPorId",
                        column: x => x.RegistradoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HallazgosAuditoria_RegistradoPorId",
                table: "HallazgosAuditoria",
                column: "RegistradoPorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HallazgosAuditoria");

            migrationBuilder.DropColumn(
                name: "UltimoReporteAuditoriaEnviadoEn",
                table: "EstadosScraper");
        }
    }
}
