namespace DespachoJuridico.API.Services;

public interface ICalculadorFechasService
{
    bool EsDiaHabil(DateTime fecha);
    DateTime SumarDiasHabiles(DateTime fechaInicio, int dias);
    DateTime SumarDiasNaturales(DateTime fechaInicio, int dias);
    DateTime? CalcularFechaLimite(DateTime fechaInicio, int? terminoDias, bool esDiasHabiles);
}

public class CalculadorFechasService : ICalculadorFechasService
{
    /// <summary>
    /// Determina si una fecha es día hábil: no es sábado, domingo, ni festivo oficial.
    /// </summary>
    public bool EsDiaHabil(DateTime fecha)
    {
        if (fecha.DayOfWeek == DayOfWeek.Saturday || fecha.DayOfWeek == DayOfWeek.Sunday)
            return false;

        var festivos = ObtenerFestivos(fecha.Year);
        return !festivos.Contains(fecha.Date);
    }

    /// <summary>
    /// Suma N días hábiles a una fecha, saltando fines de semana y festivos.
    /// </summary>
    public DateTime SumarDiasHabiles(DateTime fechaInicio, int dias)
    {
        var fecha = fechaInicio.Date;
        var diasSumados = 0;

        while (diasSumados < dias)
        {
            fecha = fecha.AddDays(1);
            if (EsDiaHabil(fecha))
            {
                diasSumados++;
            }
        }

        return fecha;
    }

    /// <summary>
    /// Suma N días naturales (calendario) a una fecha, sin excepciones.
    /// </summary>
    public DateTime SumarDiasNaturales(DateTime fechaInicio, int dias)
    {
        return fechaInicio.Date.AddDays(dias);
    }

    /// <summary>
    /// Calcula la fecha límite según el término de una etapa.
    /// Devuelve null si el término aún no está definido (termino_dias == null).
    /// </summary>
    public DateTime? CalcularFechaLimite(DateTime fechaInicio, int? terminoDias, bool esDiasHabiles)
    {
        if (terminoDias == null)
            return null;

        return esDiasHabiles
            ? SumarDiasHabiles(fechaInicio, terminoDias.Value)
            : SumarDiasNaturales(fechaInicio, terminoDias.Value);
    }

    /// <summary>
    /// Festivos oficiales de México según el Art. 74 de la Ley Federal del Trabajo,
    /// calculados dinámicamente para el año solicitado.
    /// </summary>
    private static HashSet<DateTime> ObtenerFestivos(int año)
    {
        var festivos = new HashSet<DateTime>
        {
            new DateTime(año, 1, 1),    // Año Nuevo
            new DateTime(año, 5, 1),    // Día del Trabajo
            new DateTime(año, 9, 16),   // Día de la Independencia
            new DateTime(año, 12, 25),  // Navidad
        };

        // Primer lunes de febrero (conmemora el 5 de febrero - Constitución)
        festivos.Add(PrimerLunesDelMes(año, 2));

        // Tercer lunes de marzo (conmemora el 21 de marzo - Natalicio de Juárez)
        festivos.Add(TercerLunesDelMes(año, 3));

        // Tercer lunes de noviembre (conmemora el 20 de noviembre - Revolución)
        festivos.Add(TercerLunesDelMes(año, 11));

        return festivos;
    }

    private static DateTime PrimerLunesDelMes(int año, int mes)
    {
        var fecha = new DateTime(año, mes, 1);
        while (fecha.DayOfWeek != DayOfWeek.Monday)
            fecha = fecha.AddDays(1);
        return fecha;
    }

    private static DateTime TercerLunesDelMes(int año, int mes)
    {
        var primerLunes = PrimerLunesDelMes(año, mes);
        return primerLunes.AddDays(14); // +2 semanas = tercer lunes
    }
}