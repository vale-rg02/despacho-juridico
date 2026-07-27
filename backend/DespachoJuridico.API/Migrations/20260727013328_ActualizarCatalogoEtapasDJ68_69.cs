using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class ActualizarCatalogoEtapasDJ68_69 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Recorre Lanzamiento (Hipotecario) de 13 a 16 para dejar espacio a las 3 almonedas.
            // Idempotente: si ya está en 16, este UPDATE no cambia nada.
            migrationBuilder.Sql(@"
                UPDATE ""EtapasCatalogo"" SET ""Orden"" = 16
                WHERE ""Nombre"" = 'Lanzamiento' AND ""TipoJuicio"" = 'Hipotecario';
            ");

            InsertSiNoExiste(migrationBuilder, "Certificado de Gravamen", "Oral Mercantil", 180, false, 10);
            InsertSiNoExiste(migrationBuilder, "1ra Almoneda", "Oral Mercantil", null, true, 11);
            InsertSiNoExiste(migrationBuilder, "2da Almoneda", "Oral Mercantil", null, true, 12);
            InsertSiNoExiste(migrationBuilder, "3ra Almoneda", "Oral Mercantil", null, true, 13);
            InsertSiNoExiste(migrationBuilder, "Ejecución Forzosa", "Oral Mercantil", null, true, 14);

            InsertSiNoExiste(migrationBuilder, "1ra Almoneda", "Hipotecario", null, true, 13);
            InsertSiNoExiste(migrationBuilder, "2da Almoneda", "Hipotecario", null, true, 14);
            InsertSiNoExiste(migrationBuilder, "3ra Almoneda", "Hipotecario", null, true, 15);
            InsertSiNoExiste(migrationBuilder, "Ejecución Forzosa", "Hipotecario", null, true, 17);
        }

        private static void InsertSiNoExiste(
            MigrationBuilder migrationBuilder, string nombre, string tipoJuicio,
            int? terminoDias, bool esDiasHabiles, int orden)
        {
            var terminoDiasSql = terminoDias.HasValue ? terminoDias.Value.ToString() : "NULL";
            var esDiasHabilesSql = esDiasHabiles ? "true" : "false";

            migrationBuilder.Sql($@"
                INSERT INTO ""EtapasCatalogo"" (""Nombre"", ""TipoJuicio"", ""TerminoDias"", ""EsDiasHabiles"", ""Orden"")
                SELECT '{nombre}', '{tipoJuicio}', {terminoDiasSql}, {esDiasHabilesSql}, {orden}
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""EtapasCatalogo""
                    WHERE ""Nombre"" = '{nombre}' AND ""TipoJuicio"" = '{tipoJuicio}'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""EtapasCatalogo""
                WHERE (""Nombre"" IN ('1ra Almoneda', '2da Almoneda', '3ra Almoneda', 'Ejecución Forzosa')
                       AND ""TipoJuicio"" IN ('Hipotecario', 'Oral Mercantil'))
                   OR (""Nombre"" = 'Certificado de Gravamen' AND ""TipoJuicio"" = 'Oral Mercantil');

                UPDATE ""EtapasCatalogo"" SET ""Orden"" = 13
                WHERE ""Nombre"" = 'Lanzamiento' AND ""TipoJuicio"" = 'Hipotecario';
            ");
        }
    }
}
