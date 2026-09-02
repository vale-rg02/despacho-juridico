using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class CorregirMateriaTipoJuicioHipotecario : Migration
    {
        // "Civil" se usaba por error como clave del catálogo de etapas que en
        // realidad corresponde al juicio hipotecario (sus pasos son de remate:
        // Certificado de Gravamen, Avalúos, Diligencia de Remate, Lanzamiento).
        // El litigante reportó que Materia/Tipo de juicio salían al revés en el
        // formulario ("Hipotecario" como materia, "Civil" como tipo de juicio);
        // esto corrige tanto el catálogo como los expedientes ya guardados.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""EtapasCatalogo"" SET ""TipoJuicio"" = 'Hipotecario' WHERE ""TipoJuicio"" = 'Civil';");

            // Solo se corrige la Materia de los expedientes que tenían exactamente
            // el patrón invertido reportado (Materia='Hipotecario' + TipoJuicio='Civil').
            // Expedientes con otra Materia (ej. 'Mercantil') y TipoJuicio='Civil' no se
            // tocan en su Materia — solo heredan el renombrado de clave de abajo.
            migrationBuilder.Sql(@"UPDATE ""Expedientes"" SET ""Materia"" = 'Civil' WHERE ""Materia"" = 'Hipotecario' AND ""TipoJuicio"" = 'Civil';");

            migrationBuilder.Sql(@"UPDATE ""Expedientes"" SET ""TipoJuicio"" = 'Hipotecario' WHERE ""TipoJuicio"" = 'Civil';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""Expedientes"" SET ""TipoJuicio"" = 'Civil' WHERE ""TipoJuicio"" = 'Hipotecario';");

            migrationBuilder.Sql(@"UPDATE ""Expedientes"" SET ""Materia"" = 'Hipotecario' WHERE ""Materia"" = 'Civil' AND ""TipoJuicio"" = 'Civil';");

            migrationBuilder.Sql(@"UPDATE ""EtapasCatalogo"" SET ""TipoJuicio"" = 'Civil' WHERE ""TipoJuicio"" = 'Hipotecario';");
        }
    }
}
