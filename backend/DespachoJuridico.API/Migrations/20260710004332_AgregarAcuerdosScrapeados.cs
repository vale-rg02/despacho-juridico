using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAcuerdosScrapeados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcuerdosScrapeados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExpedienteId = table.Column<int>(type: "integer", nullable: false),
                    NumeroExpediente = table.Column<string>(type: "text", nullable: false),
                    IdUnidad = table.Column<int>(type: "integer", nullable: false),
                    NombreJuzgado = table.Column<string>(type: "text", nullable: false),
                    Partes = table.Column<string>(type: "text", nullable: false),
                    Sintesis = table.Column<string>(type: "text", nullable: false),
                    FechaAcuerdo = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaDetectado = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NotificacionEnviada = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcuerdosScrapeados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcuerdosScrapeados_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdosScrapeados_ExpedienteId",
                table: "AcuerdosScrapeados",
                column: "ExpedienteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcuerdosScrapeados");
        }
    }
}
