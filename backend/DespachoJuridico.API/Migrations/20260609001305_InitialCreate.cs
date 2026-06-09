using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bancos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Direccion = table.Column<string>(type: "text", nullable: true),
                    Telefono = table.Column<string>(type: "text", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bancos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EtapasCatalogo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    TipoJuicio = table.Column<string>(type: "text", nullable: true),
                    TerminoDias = table.Column<int>(type: "integer", nullable: true),
                    EsDiasHabiles = table.Column<bool>(type: "boolean", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtapasCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Rol = table.Column<string>(type: "text", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Expedientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumeroExpediente = table.Column<string>(type: "text", nullable: false),
                    ParteDemandada = table.Column<string>(type: "text", nullable: false),
                    BancoId = table.Column<int>(type: "integer", nullable: true),
                    Juzgado = table.Column<string>(type: "text", nullable: true),
                    Materia = table.Column<string>(type: "text", nullable: true),
                    TipoJuicio = table.Column<string>(type: "text", nullable: true),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    Prioridad = table.Column<string>(type: "text", nullable: false),
                    UsuarioAsignadoId = table.Column<int>(type: "integer", nullable: true),
                    ExpedienteRelacionadoId = table.Column<int>(type: "integer", nullable: true),
                    Notas = table.Column<string>(type: "text", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreadoPorId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expedientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expedientes_Bancos_BancoId",
                        column: x => x.BancoId,
                        principalTable: "Bancos",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Expedientes_Expedientes_ExpedienteRelacionadoId",
                        column: x => x.ExpedienteRelacionadoId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Expedientes_Usuarios_CreadoPorId",
                        column: x => x.CreadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expedientes_Usuarios_UsuarioAsignadoId",
                        column: x => x.UsuarioAsignadoId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BitacoraCambios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExpedienteId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Accion = table.Column<string>(type: "text", nullable: false),
                    Detalle = table.Column<string>(type: "text", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BitacoraCambios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BitacoraCambios_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BitacoraCambios_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HistorialEtapas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExpedienteId = table.Column<int>(type: "integer", nullable: false),
                    EtapaCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    FechaInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaLimite = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaCompletada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Atendido = table.Column<bool>(type: "boolean", nullable: false),
                    RegistradoPorId = table.Column<int>(type: "integer", nullable: false),
                    Notas = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEtapas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialEtapas_EtapasCatalogo_EtapaCatalogoId",
                        column: x => x.EtapaCatalogoId,
                        principalTable: "EtapasCatalogo",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HistorialEtapas_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistorialEtapas_Usuarios_RegistradoPorId",
                        column: x => x.RegistradoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExpedienteId = table.Column<int>(type: "integer", nullable: false),
                    HistorialEtapaId = table.Column<int>(type: "integer", nullable: true),
                    Mensaje = table.Column<string>(type: "text", nullable: false),
                    Canal = table.Column<string>(type: "text", nullable: false),
                    DestinatarioEmail = table.Column<string>(type: "text", nullable: true),
                    Enviada = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEnvio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notificaciones_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notificaciones_HistorialEtapas_HistorialEtapaId",
                        column: x => x.HistorialEtapaId,
                        principalTable: "HistorialEtapas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BitacoraCambios_ExpedienteId",
                table: "BitacoraCambios",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_BitacoraCambios_UsuarioId",
                table: "BitacoraCambios",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Expedientes_BancoId",
                table: "Expedientes",
                column: "BancoId");

            migrationBuilder.CreateIndex(
                name: "IX_Expedientes_CreadoPorId",
                table: "Expedientes",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Expedientes_ExpedienteRelacionadoId",
                table: "Expedientes",
                column: "ExpedienteRelacionadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Expedientes_UsuarioAsignadoId",
                table: "Expedientes",
                column: "UsuarioAsignadoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEtapas_EtapaCatalogoId",
                table: "HistorialEtapas",
                column: "EtapaCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEtapas_ExpedienteId",
                table: "HistorialEtapas",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEtapas_RegistradoPorId",
                table: "HistorialEtapas",
                column: "RegistradoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_ExpedienteId",
                table: "Notificaciones",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_HistorialEtapaId",
                table: "Notificaciones",
                column: "HistorialEtapaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BitacoraCambios");

            migrationBuilder.DropTable(
                name: "Notificaciones");

            migrationBuilder.DropTable(
                name: "HistorialEtapas");

            migrationBuilder.DropTable(
                name: "EtapasCatalogo");

            migrationBuilder.DropTable(
                name: "Expedientes");

            migrationBuilder.DropTable(
                name: "Bancos");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
