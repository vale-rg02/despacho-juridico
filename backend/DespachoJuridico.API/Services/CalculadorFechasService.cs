using DespachoJuridico.API.Models.Enums;

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
    public bool EsDiaHabil(DateTime fecha)
    {
        if (fecha.DayOfWeek == DayOfWeek.Saturday || fecha.DayOfWeek == DayOfWeek.Sunday)
            return false;
        var festivos = ObtenerFestivos(fecha.Year);
        return !festivos.Contains(fecha.Date);
    }

    public DateTime SumarDiasHabiles(DateTime fechaInicio, int dias)
    {
        var fecha = fechaInicio.Date;
        var diasSumados = 0;
        while (diasSumados < dias)
        {
            fecha = fecha.AddDays(1);
            if (EsDiaHabil(fecha)) diasSumados++;
        }
        return fecha;
    }

    public DateTime SumarDiasNaturales(DateTime fechaInicio, int dias)
    {
        return fechaInicio.Date.AddDays(dias);
    }

    public DateTime? CalcularFechaLimite(DateTime fechaInicio, int? terminoDias, bool esDiasHabiles)
    {
        if (terminoDias == null) return null;
        return esDiasHabiles
            ? SumarDiasHabiles(fechaInicio, terminoDias.Value)
            : SumarDiasNaturales(fechaInicio, terminoDias.Value);
    }

    private static HashSet<DateTime> ObtenerFestivos(int año)
    {
        var festivos = new HashSet<DateTime>
        {
            new DateTime(año, 1, 1),
            new DateTime(año, 5, 1),
            new DateTime(año, 9, 16),
            new DateTime(año, 12, 25),
        };
        festivos.Add(PrimerLunesDelMes(año, 2));
        festivos.Add(TercerLunesDelMes(año, 3));
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
        return primerLunes.AddDays(14);
    }
}