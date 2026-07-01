import { formatearFecha } from '../utils/formato'

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

function HistorialEtapas({ etapas, onCompletar, onRevertir }) {
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
              <span>Inicio: {formatearFecha(etapa.fechaInicio)}</span>
              <span>Límite: {etapa.fechaLimite ? formatearFecha(etapa.fechaLimite) : '—'}</span>
              {etapa.fechaCompletada && (
                <span>Completada: {formatearFecha(etapa.fechaCompletada)}</span>
              )}
            </div>
            {etapa.notas && (
              <p className="text-sm text-foreground/80 mt-1">{etapa.notas}</p>
            )}
            {activa ? (
              <button
                onClick={() => onCompletar(etapa.id)}
                className="text-xs text-accent hover:underline mt-2 font-medium"
              >
                Marcar como completada
              </button>
            ) : (
              <button
                onClick={() => onRevertir(etapa.id)}
                className="text-xs text-red-400 hover:underline mt-2 font-medium"
              >
                Revertir a pendiente
              </button>
            )}
          </div>
        )
      })}
    </div>
  )
}

export default HistorialEtapas