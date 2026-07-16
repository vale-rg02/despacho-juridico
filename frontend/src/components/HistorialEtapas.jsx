import { formatearFecha } from '../utils/formato'

// El backend guarda fecha y hora "etiquetadas" como UTC sin convertirlas de
// verdad (mismo patrón que fechaLimite/fechaAcuerdo en el resto de la app), así
// que se arma el Date con sus componentes locales (sin conversión de zona) en
// vez de parsear el ISO con new Date(), que desfasaría la hora según la zona
// horaria del navegador.
function formatearFechaConHora(fechaStr) {
  if (!fechaStr) return '—'
  const match = fechaStr.match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/)
  if (!match) return '—'
  const [, anio, mes, dia, horas, minutos] = match
  const fecha = new Date(Number(anio), Number(mes) - 1, Number(dia), Number(horas), Number(minutos))

  const soloFecha = fecha.toLocaleDateString('es-MX', { day: '2-digit', month: 'short', year: 'numeric' })
  if (horas === '00' && minutos === '00') return soloFecha

  const hora = fecha.toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit' })
  return `${soloFecha}, ${hora}`
}

function diasRestantes(fechaLimite) {
  if (!fechaLimite) return null
  const hoy = new Date()
  hoy.setHours(0, 0, 0, 0)
  const limite = new Date(fechaLimite)
  const diff = Math.ceil((limite - hoy) / (1000 * 60 * 60 * 24))
  return diff
}

function EstadoFecha({ etapa }) {
  if (etapa.fechaCompletada) {
    return (
      <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-800">
        Completada
      </span>
    )
  }
  if (!etapa.fechaLimite) {
    return <span className="text-xs text-muted-foreground">Sin plazo</span>
  }

  const dias = diasRestantes(etapa.fechaLimite)
  if (dias < 0) {
    return (
      <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-red-50 text-red-700">
        Vencida hace {Math.abs(dias)} día{Math.abs(dias) !== 1 ? 's' : ''}
      </span>
    )
  }
  if (dias <= 7) {
    return (
      <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-amber-50 text-amber-800">
        Vence en {dias} día{dias !== 1 ? 's' : ''}
      </span>
    )
  }
  return <span className="text-xs text-muted-foreground">Vence en {dias} días</span>
}

function HistorialEtapas({ etapas, onCompletar, onRevertir, onEditar, onEliminar }) {
  if (etapas.length === 0) {
    return (
      <p className="text-sm text-muted-foreground text-center py-4">
        Sin etapas registradas todavía
      </p>
    )
  }

  return (
    <div className="space-y-3">
      {etapas.map(etapa => {
        const activa = !etapa.fechaCompletada
        return (
          <div
            key={etapa.id}
            className={`rounded-md p-3 border ${
              activa ? 'bg-accent/5 border-accent/20' : 'border-border'
            }`}
          >
            <div className="flex justify-between items-start mb-1.5">
              <span className="text-sm font-medium text-foreground">{etapa.etapaNombre ?? 'Etapa'}</span>
              <EstadoFecha etapa={etapa} />
            </div>
            <div className="flex flex-wrap gap-x-4 text-xs text-muted-foreground mb-1" style={{ fontFamily: "'DM Mono', monospace" }}>
              <span>Inicio: {formatearFechaConHora(etapa.fechaInicio)}</span>
              <span>Límite: {etapa.fechaLimite ? formatearFechaConHora(etapa.fechaLimite) : '—'}</span>
              {etapa.fechaCompletada && (
                <span>Completada: {formatearFecha(etapa.fechaCompletada)}</span>
              )}
            </div>
            {etapa.notas && (
              <p className="text-sm text-foreground/80 mt-1">{etapa.notas}</p>
            )}
            <div className="flex flex-wrap items-center gap-3 mt-2">
              {activa ? (
                <button
                  onClick={() => onCompletar(etapa.id)}
                  className="text-xs px-3 py-1 rounded-md border border-accent text-accent hover:bg-accent hover:text-white transition font-medium"
                >
                  Marcar como completada
                </button>
              ) : (
                <button
                  onClick={() => {
                    if (window.confirm('¿Estás seguro de que deseas revertir esta etapa a pendiente?')) {
                      onRevertir(etapa.id)
                    }
                  }}
                  className="text-xs px-3 py-1 rounded-md border border-red-300 text-red-500 hover:bg-red-500 hover:text-white transition font-medium"
                >
                  Revertir a pendiente
                </button>
              )}
              <button
                onClick={() => onEditar(etapa)}
                className="text-xs px-3 py-1 rounded-md border border-border text-muted-foreground hover:bg-secondary hover:text-foreground transition font-medium"
              >
                Editar
              </button>
              <button
                onClick={() => {
                  if (window.confirm('¿Estás seguro de que deseas eliminar esta etapa? Esta acción no se puede deshacer.')) {
                    onEliminar(etapa.id)
                  }
                }}
                className="text-xs px-3 py-1 rounded-md border border-red-300 text-red-500 hover:bg-red-500 hover:text-white transition font-medium"
              >
                Eliminar
              </button>
            </div>
          </div>
        )
      })}
    </div>
  )
}

export default HistorialEtapas