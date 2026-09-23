import { useEffect, useRef, useState } from 'react'
import { RefreshCw, Clock } from 'lucide-react'
import { actualizarMisExpedientes, getMiEstadoActualizacion } from '../services/scraper'
import { formatearFecha } from '../utils/formato'

function formatearMMSS(segundos) {
  const m = Math.floor(segundos / 60)
  const s = segundos % 60
  return `${m}:${s.toString().padStart(2, '0')}`
}

// DJ-102: botón self-service para que el litigante fuerce una consulta al
// scraper acotada a sus propios expedientes. Aislado de Expedientes.jsx (que ya
// es grande) para poder testearlo solo. Nunca bloquea el resto de la página --
// solo el botón mismo cambia de estado mientras corre o está en cooldown.
function BotonActualizarExpedientes() {
  const [estado, setEstado] = useState(null) // null mientras carga el estado inicial
  const [segundosRestantes, setSegundosRestantes] = useState(0)
  const [disparando, setDisparando] = useState(false)
  const [mensajeError, setMensajeError] = useState('')
  const pollingRef = useRef(null)

  useEffect(() => {
    cargarEstado()
    return () => detenerPolling()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (segundosRestantes <= 0) return
    const intervalo = setInterval(() => {
      setSegundosRestantes(s => Math.max(0, s - 1))
    }, 1000)
    return () => clearInterval(intervalo)
  }, [segundosRestantes > 0])

  function detenerPolling() {
    if (pollingRef.current) {
      clearInterval(pollingRef.current)
      pollingRef.current = null
    }
  }

  function iniciarPolling() {
    detenerPolling()
    pollingRef.current = setInterval(async () => {
      const nuevoEstado = await cargarEstado()
      if (nuevoEstado && !nuevoEstado.enProgreso) detenerPolling()
    }, 3000)
  }

  async function cargarEstado() {
    try {
      const data = await getMiEstadoActualizacion()
      setEstado(data)
      setSegundosRestantes(data.cooldownRestanteSegundos ?? 0)
      return data
    } catch {
      return null
    }
  }

  async function handleClick() {
    if (disparando || estado?.enProgreso || segundosRestantes > 0) return

    setDisparando(true)
    setMensajeError('')
    try {
      await actualizarMisExpedientes()
      setEstado(prev => ({ ...prev, enProgreso: true }))
      iniciarPolling()
    } catch (err) {
      const datos = err.response?.data
      if (datos?.cooldownRestanteSegundos != null) {
        setSegundosRestantes(datos.cooldownRestanteSegundos)
      }
      setMensajeError(datos?.mensaje || 'No se pudo iniciar la actualización')
    } finally {
      setDisparando(false)
    }
  }

  const enProgreso = disparando || estado?.enProgreso
  const enCooldown = !enProgreso && segundosRestantes > 0
  const deshabilitado = estado === null || enProgreso || enCooldown

  let texto = 'Actualizar expedientes'
  if (enProgreso) texto = 'Actualizando…'
  else if (enCooldown) texto = `Disponible en ${formatearMMSS(segundosRestantes)}`

  return (
    <div className="flex items-center gap-2 text-sm">
      <button
        type="button"
        onClick={handleClick}
        disabled={deshabilitado}
        className="flex items-center gap-1.5 bg-input-background hover:bg-secondary disabled:opacity-60 disabled:cursor-not-allowed text-foreground text-sm px-3 py-1.5 rounded transition border border-border"
      >
        <RefreshCw size={12} className={enProgreso ? 'animate-spin text-accent' : 'text-muted-foreground'} />
        {texto}
      </button>
      {estado && (
        <span className="hidden sm:flex items-center gap-1 text-xs text-muted-foreground" title="Última actualización real de tus datos">
          <Clock size={11} />
          {estado.ultimaActualizacionEn ? formatearFecha(estado.ultimaActualizacionEn) : 'Nunca'}
        </span>
      )}
      {mensajeError && !enCooldown && (
        <span className="text-xs text-red-600">{mensajeError}</span>
      )}
    </div>
  )
}

export default BotonActualizarExpedientes
