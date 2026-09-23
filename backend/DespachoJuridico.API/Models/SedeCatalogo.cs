namespace DespachoJuridico.API.Models;

// DJ-112: catálogo de municipios ("Sede") que cubre ADISON. Fuente de verdad
// para el combobox de Sede y para filtrar JuzgadoCatalogo -- Expediente.Sede
// guarda una copia en texto (validada contra esta tabla al crear/editar), no
// una FK, para no tocar el código que ya lee Expediente.Sede/Juzgado como
// texto plano (correos, bitácora, matching de acuerdos).
public class SedeCatalogo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public ICollection<JuzgadoCatalogo> Juzgados { get; set; } = [];
}
