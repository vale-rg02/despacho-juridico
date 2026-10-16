using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarBitacoraEvaluacionScraper : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BitacoraDescarteItemizado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CorridaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaHoraCorrida = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdUnidad = table.Column<int>(type: "integer", nullable: false),
                    NombreJuzgado = table.Column<string>(type: "text", nullable: false),
                    NumeroExpediente = table.Column<string>(type: "text", nullable: false),
                    Razon = table.Column<string>(type: "text", nullable: false),
                    EtapaFalla = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BitacoraDescarteItemizado", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BitacoraEvaluacionScraper",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CorridaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaHoraCorrida = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaConsultada = table.Column<DateOnly>(type: "date", nullable: false),
                    IdUnidad = table.Column<int>(type: "integer", nullable: false),
                    NombreJuzgado = table.Column<string>(type: "text", nullable: false),
                    Origen = table.Column<string>(type: "text", nullable: false),
                    DryRun = table.Column<bool>(type: "boolean", nullable: false),
                    ConsultaExitosa = table.Column<bool>(type: "boolean", nullable: false),
                    MotivoFallo = table.Column<string>(type: "text", nullable: true),
                    Devueltos = table.Column<int>(type: "integer", nullable: false),
                    Evaluaciones = table.Column<int>(type: "integer", nullable: false),
                    GuardadoVisibleAlta = table.Column<int>(type: "integer", nullable: false),
                    GuardadoVisibleMedia = table.Column<int>(type: "integer", nullable: false),
                    GuardadoOcultoBaja = table.Column<int>(type: "integer", nullable: false),
                    YaExistia = table.Column<int>(type: "integer", nullable: false),
                    DescartadoSinCandidato = table.Column<int>(type: "integer", nullable: false),
                    DescartadoNumeroJuzgadoNoCoincide = table.Column<int>(type: "integer", nullable: false),
                    DescartadoExpedienteCerrado = table.Column<int>(type: "integer", nullable: false),
                    DescartadoFueraDeAlcanceFiltro = table.Column<int>(type: "integer", nullable: false),
                    DescartadoExcepcion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BitacoraEvaluacionScraper", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BitacoraDescarteItemizado");

            migrationBuilder.DropTable(
                name: "BitacoraEvaluacionScraper");
        }
    }
}
