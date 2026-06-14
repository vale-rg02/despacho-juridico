import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { getAlertas, marcarAtendida } from '../services/notificaciones'
import { formatearFecha } from '../utils/formato'

function PanelAlertas() {
  const navigate = useNavigate()
  const [alertas, setAlertas] = useState([])
  const [abierto, setAbierto] = useState(false)
  const [cargando, setCargando] = useState(true)
  const contenedorRef = useRef(null)

  useEffect(() => {
    cargarAlertas()
  }, [])

  // Cierra el panel si se hace clic fuera
  useEffect(() => {
    function handleClickFuera(e) {
      if (contenedorRef.current && !contenedorRef.current.contains(e.target)) {
        setAbierto(false)
      }
    }
    document.addEventListener('mousedown', handleClickFuera)
    return () => document.removeEventListener('mousedown', handleClickFuera)
  }, [])

  async function cargarAlertas() {
    try {
      const data = await getAlertas()
      setAlertas(data)
    } catch {
      // Silencioso: el panel de alertas no debe romper la pantalla si falla
    } finally {
      setCargando(false)
    }
  }

  async function handleAtender(e, alerta) {
    e.stopPropagation()
    try {
      await marcarAtendida(alerta.expedienteId, alerta.etapaHistorialId)
      setAlertas(prev => prev.filter(a => a.etapaHistorialId !== alerta.etapaHistorialId))
    } catch {
      // si falla, simplemente no se quita de la lista
    }
  }

  function irAlExpediente(expedienteId) {
    setAbierto(false)
    navigate(`/expedientes/${expedienteId}`)
  }

  return (
    <div className="relative" ref={contenedorRef}>
      <button
        onClick={() => setAbierto(prev => !prev)}
        className="relative text-gray-500 hover:text-gray-700 transition-colors"
        aria-label="Alertas"
      >
        <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
        </svg>
        {alertas.length > 0 && (
          <span className="absolute -top-1 -right-1 bg-red-500 text-white text-xs font-bold rounded-full w-5 h-5 flex items-center justify-center">
            {alertas.length > 9 ? '9+' : alertas.length}
          </span>
        )}
      </button>

      {abierto && (
        <div className="absolute right-0 mt-2 w-80 bg-white rounded-lg shadow-lg border border-gray-200 z-50 max-h-96 overflow-y-auto">
          <div className="px-4 py-3 border-b border-gray-100">
            <h3 className="text-sm font-semibold text-gray-700">Alertas</h3>
          </div>

          {cargando ? (
            <p className="text-sm text-gray-400 text-center py-6">Cargando...</p>
          ) : alertas.length === 0 ? (
            <p className="text-sm text-gray-400 text-center py-6">Sin alertas pendientes</p>
          ) : (
            <div className="divide-y divide-gray-100">
              {alertas.map(alerta => (
                <div
                  key={alerta.etapaHistorialId}
                  onClick={() => irAlExpediente(alerta.expedienteId)}
                  className="px-4 py-3 hover:bg-gray-50 cursor-pointer"
                >
                  <div className="flex justify-between items-start mb-1">
                    <span className="text-sm font-medium text-blue-600">{alerta.numeroExpediente}</span>
                    {alerta.vencida ? (
                      <span className="text-xs font-medium text-red-600">
                        Vencida hace {Math.abs(alerta.diasRestantes)} día{Math.abs(alerta.diasRestantes) !== 1 ? 's' : ''}
                      </span>
                    ) : (
                      <span className="text-xs font-medium text-yellow-600">
                        Vence en {alerta.diasRestantes} día{alerta.diasRestantes !== 1 ? 's' : ''}
                      </span>
                    )}
                  </div>
                  <p className="text-sm text-gray-600">{alerta.etapaNombre ?? 'Etapa'}</p>
                  <div className="flex justify-between items-center mt-1">
                    <span className="text-xs text-gray-400">Límite: {formatearFecha(alerta.fechaLimite)}</span>
                    <button
                      onClick={(e) => handleAtender(e, alerta)}
                      className="text-xs text-gray-500 hover:text-gray-700 underline"
                    >
                      Marcar atendida
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
}

export default PanelAlertas