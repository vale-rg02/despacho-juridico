using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DespachoJuridico.API.Migrations
{
    /// <inheritdoc />
    public partial class EliminarAcuerdosForaneosMalEmparejados : Migration
    {
        // El commit c44cef7 ("ampliar scraper de acuerdos a todos los distritos de
        // Sonora") agregó juzgados foráneos que se emparejan con expedientes SOLO
        // por número de expediente, sin validar el juzgado (a diferencia de los de
        // Hermosillo, que sí lo validan). El número de expediente se repite entre
        // juzgados de todo el estado, así que esto generó falsos positivos: acuerdos
        // de casos ajenos se guardaron emparejados con expedientes del despacho.
        //
        // Antes de ese commit el scraper solo conocía los juzgados de Hermosillo, así
        // que CUALQUIER registro con un IdUnidad fuera de ese conjunto es, por
        // definición, producto del match defectuoso — no existe forma de que sea un
        // registro legítimo anterior. Se eliminan todos; el emparejamiento se corrigió
        // en ScraperAcuerdosService (ahora exige también coincidencia de partes).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""AcuerdosScrapeados""
                WHERE ""IdUnidad"" NOT IN (152,153,154,155,156,157,158,159,160,161,174,175,276,277,296,905);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No reversible: no se puede reconstruir la información scrapeada eliminada.
        }
    }
}
