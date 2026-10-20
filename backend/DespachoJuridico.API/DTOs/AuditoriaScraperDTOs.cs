namespace DespachoJuridico.API.DTOs;

// DJ-140: reporte diario de auditoría del scraper -- correo interno al equipo
// técnico, nunca a litigantes. Es solo un aviso: ningún campo de aquí puede
// contener Partes ni nombres (ver HallazgoAuditoriaTests para el guardarraíl
// estructural equivalente al de DJ-139).
public class ReporteAuditoriaScraperResponse
{
    public DateOnly Fecha { get; set; }
    public bool VentanaIniciada { get; set; }

    // Elemento 0: resumen del embudo del día, solo Origen="Automatica" (ver
    // ClasificarOrigenCorrida) -- las corridas no automáticas del día se
    // cuentan aparte, nunca se mezclan en este total.
    public EmbudoJuzgadoResponse ResumenEmbudoAutomatico { get; set; } = new();
    public int CorridasNoAutomaticasDelDia { get; set; }

    // Elemento 1: ocultos Hermosillo con juzgado+número exactos (Confianza=Baja),
    // ya filtrados contra el ruido conocido de JuzgadoCoincide (ver DJ-98).
    public List<AcuerdoOcultoCandidato> Cola1OcultosHermosillo { get; set; } = new();
    public int Cola1ExcluidosPorRuidoConocido { get; set; }

    // Elemento 2: foráneos ocultos con nombre parecido (banda de similitud
    // configurable, justo debajo del umbral real de PartesCoinciden).
    public List<AcuerdoOcultoCandidato> Cola2ForaneosParecidos { get; set; } = new();

    // Elemento 2b: descartes NUMERO_COINCIDE_JUZGADO_NO del día -- itemizados
    // solo los que tienen indicio de problema real de nombre de juzgado;
    // el resto se colapsa en un conteo para mantener la cola manejable.
    public List<ColisionJuzgadoCandidato> Cola2bColisionesConIndicio { get; set; } = new();
    public int Cola2bColisionesSinIndicio { get; set; }

    // Elemento 3: cálculo de DJ-133, reutilizado sin duplicar lógica.
    public ReporteCandidatosResponse FrasesCandidatas { get; set; } = new();

    // Elemento 4: anomalías, en el orden de severidad esperada.
    public List<DescarteItemizadoResponse> NotificacionesFallidas { get; set; } = new();
    public List<EmbudoJuzgadoResponse> ConsultasFallidas { get; set; } = new();
    public List<AnomaliaUnidadEnCero> UnidadesEnCeroVariosDias { get; set; } = new();
    public List<AnomaliaCaidaBrusca> CaidasBruscas { get; set; } = new();

    // Elemento 5: conteo de días hábiles consecutivos sin cada severidad +
    // tabla de hallazgos. Null cuando VentanaIniciada=false.
    public int? DiasHabilesSinS1 { get; set; }
    public int? DiasHabilesSinS2 { get; set; }
    public int? DiasHabilesSinS3 { get; set; }
    public int? AcumuladoVisiblesDesdeInicioVentana { get; set; }
    public List<HallazgoAuditoriaResponse> TablaHallazgos { get; set; } = new();
}

// Un candidato de Cola 1 o Cola 2 -- PuntajeSimilitud solo se llena en Cola 2
// (Cola 1 es coincidencia exacta de juzgado+número, no hay puntaje que mostrar).
public class AcuerdoOcultoCandidato
{
    public string NumeroExpediente { get; set; } = string.Empty;
    public string NombreJuzgado { get; set; } = string.Empty;
    public DateTime FechaDetectado { get; set; }
    public double? PuntajeSimilitud { get; set; }
}

public class ColisionJuzgadoCandidato
{
    // DJ-140: Id de la fila BitacoraDescarteItemizado más reciente del grupo
    // (ver TotalCoincidencias) -- se usa para pedir su recuperación vía
    // POST /scraper/colisiones-juzgado/{id}/reintentar, una vez corregida la
    // causa real (el acuerdo nunca se guardó, no hay nada más que referenciar).
    public int Id { get; set; }
    public string NumeroExpediente { get; set; } = string.Empty;
    public string NombreJuzgadoAdison { get; set; } = string.Empty;
    public string NombreJuzgadoExpediente { get; set; } = string.Empty;
    public double PuntajeSimilitudJuzgado { get; set; }

    // DJ-140 (ajuste, 20 oct 2026): el scraper vuelve a loguear el mismo
    // descarte en cada ciclo mientras la causa no se corrija -- este campo
    // cuenta cuántas filas de BitacoraDescarteItemizado se agruparon en este
    // mismo candidato (mismo NumeroExpediente + NombreJuzgadoAdison), para no
    // repetir la misma línea una vez por ciclo en el correo diario.
    public int TotalCoincidencias { get; set; } = 1;
}

public class AnomaliaUnidadEnCero
{
    public int IdUnidad { get; set; }
    public string NombreJuzgado { get; set; } = string.Empty;
    public int DiasConsecutivosEnCero { get; set; }
}

public class AnomaliaCaidaBrusca
{
    public int IdUnidad { get; set; }
    public string NombreJuzgado { get; set; } = string.Empty;
    public int DevueltosHoy { get; set; }
    public double MedianaUltimos7Dias { get; set; }
}

// DJ-140: registro de un hallazgo humano -- GET/POST solo admin, sin frontend.
public class HallazgoAuditoriaRequest
{
    public DateOnly Fecha { get; set; }
    public string Severidad { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? NumeroExpediente { get; set; }
    public string? NombreJuzgado { get; set; }
}

public class HallazgoAuditoriaResponse
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public string Severidad { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? NumeroExpediente { get; set; }
    public string? NombreJuzgado { get; set; }
    public string RegistradoPorNombre { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; }
}
