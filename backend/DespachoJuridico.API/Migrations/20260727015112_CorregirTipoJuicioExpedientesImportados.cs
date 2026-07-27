using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class CorregirTipoJuicioExpedientesImportados : Migration
    {
        // Los expedientes importados masivamente desde Excel nunca recibieron
        // "TipoJuicio" (el Excel no traía esa columna o el script de importación
        // no la mapeó) — solo "Materia" quedó poblada. Sin TipoJuicio, el catálogo
        // de etapas no se filtra y el litigante ve las etapas de TODOS los tipos
        // de juicio mezcladas al registrar una etapa nueva.
        //
        // Solo se corrigen los casos donde Materia deja claro el tipo de juicio
        // y el expediente no tiene ya un TipoJuicio válido. Los 2 registros con
        // Materia también vacía (parecen residuos del import, ej. NumeroExpediente
        // "000000"/"00001") se dejan intactos para revisión manual.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Expedientes"" SET ""TipoJuicio"" = 'Hipotecario'
                WHERE (""TipoJuicio"" IS NULL OR ""TipoJuicio"" = '')
                  AND UPPER(""Materia"") = 'HIPOTECARIO';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""Expedientes"" SET ""TipoJuicio"" = 'Oral Mercantil'
                WHERE (""TipoJuicio"" IS NULL OR ""TipoJuicio"" = '')
                  AND UPPER(""Materia"") = 'MERCANTIL';
            ");

            // Familiar/Arrendamiento no tienen catálogo de etapas todavía (DJ-68/69
            // pendiente), pero fijar el TipoJuicio ya evita que se les mezclen las
            // etapas de Hipotecario/Oral Mercantil mientras tanto.
            migrationBuilder.Sql(@"
                UPDATE ""Expedientes"" SET ""TipoJuicio"" = 'Familiar'
                WHERE (""TipoJuicio"" IS NULL OR ""TipoJuicio"" = '')
                  AND UPPER(""Materia"") = 'FAMILIAR';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""Expedientes"" SET ""TipoJuicio"" = 'Arrendamiento'
                WHERE (""TipoJuicio"" IS NULL OR ""TipoJuicio"" = '')
                  AND UPPER(""Materia"") = 'ARRENDAMIENTO';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No reversible de forma segura: no hay manera de distinguir los
            // registros que estaban vacíos antes de este fix de los que ya tenían
            // un TipoJuicio correcto por otra vía (ej. los 10 Hipotecario/48 Oral
            // Mercantil ya bien etiquetados) sin borrar información legítima.
        }
    }
}
