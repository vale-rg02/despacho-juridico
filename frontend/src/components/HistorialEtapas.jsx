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
    return <span className="text-xs font-medium text-green-600">Completada</span>
  }
  if (!etapa.fechaLimite) {
    return <span className="text-xs text-gray-400">Sin plazo</span>
  }

  const dias = diasRestantes(etapa.fechaLimite)
  if (dias < 0) {
    return <span className="text-xs font-medium text-red-600">Vencida hace {Math.abs(dias)} día{Math.abs(dias) !== 1 ? 's' : ''}</span>
  }
  if (dias <= 7) {
    return <span className="text-xs font-medium text-yellow-600">Vence en {dias} día{dias !== 1 ? 's' : ''}</span>
  }
  return <span className="text-xs text-gray-400">Vence en {dias} días</span>
}

function HistorialEtapas({ etapas, onCompletar }) {
  if (etapas.length === 0) {
    return (
      <p className="text-sm text-gray-400 text-center py-4">
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
            className={`border rounded-md p-3 ${
              activa ? 'border-blue-200 bg-blue-50' : 'border-gray-100 bg-white'
            }`}
          >
            <div className="flex justify-between items-start mb-1">
              <span className="text-sm font-semibold text-gray-800">{etapa.etapaNombre ?? 'Etapa'}</span>
              <EstadoFecha etapa={etapa} />
            </div>
            <div className="flex flex-wrap gap-x-4 text-xs text-gray-500 mb-1">
              <span>Inicio: {formatearFecha(etapa.fechaInicio)}</span>
              <span>Límite: {etapa.fechaLimite ? formatearFecha(etapa.fechaLimite) : '—'}</span>
              {etapa.fechaCompletada && (
                <span>Completada: {formatearFecha(etapa.fechaCompletada)}</span>
              )}
            </div>
            {etapa.notas && (
              <p className="text-sm text-gray-600 mt-1">{etapa.notas}</p>
            )}
            {activa && (
              <button
                onClick={() => onCompletar(etapa.id)}
                className="text-xs text-blue-600 hover:underline mt-2"
              >
                Marcar como completada
              </button>
            )}
          </div>
        )
      })}
    </div>
  )
}

export default HistorialEtapas