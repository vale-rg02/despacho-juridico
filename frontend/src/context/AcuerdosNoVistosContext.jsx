import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { getAcuerdosNoVistos } from '../services/acuerdos'
import { isAuthenticated } from '../services/auth'
import { usePestanaVisible } from '../hooks/usePestanaVisible'

// DJ-127: el scraper corre cada 60 min (DJ-104, Paso 3) -- 60s de por medio es
// más que suficiente para notar un acuerdo nuevo casi al instante, y la carga
// que implica es despreciable (ver investigación del ticket: ~5 usuarios
// reales, unas decenas de miles de peticiones/día en el peor caso a una
// consulta trivial ya existente).
const INTERVALO_MS = 60_000

const AcuerdosNoVistosContext = createContext(null)

// DJ-127: notificación activa (popup) de acuerdos nuevos durante la sesión,
// además de la campana pasiva (DJ-126) y el punto en la lista (DJ-91) -- las
// tres comparten esta misma fuente de datos (un solo polling, no uno por
// componente) para no duplicar peticiones y para que marcar un acuerdo como
// visto por cualquier camino se refleje en los demás de inmediato.
export function AcuerdosNoVistosProvider({ children }) {
  const [acuerdosNoVistos, setAcuerdosNoVistos] = useState([])
  const [popups, setPopups] = useState([]) // [{ id, acuerdos: [...] }]
  // null = todavía no se estableció la línea base de esta sesión -- la
  // primera consulta nunca dispara popup, solo arma la línea base.
  const baselineRef = useRef(null)
  const visible = usePestanaVisible()
  const location = useLocation()
  const enLogin = location.pathname === '/login'
  const prevEnLoginRef = useRef(enLogin)

  // Si se acaba de iniciar sesión (transición real desde /login), es una
  // sesión nueva -- la línea base de la sesión anterior (si la hubo, ej. otro
  // usuario en el mismo equipo) ya no aplica.
  useEffect(() => {
    if (prevEnLoginRef.current && !enLogin) {
      baselineRef.current = null
      setAcuerdosNoVistos([])
      setPopups([])
    }
    prevEnLoginRef.current = enLogin
  }, [enLogin])

  const consultar = useCallback(async () => {
    if (!isAuthenticated()) return
    let data
    try {
      data = (await getAcuerdosNoVistos()) ?? []
    } catch {
      return // un fallo de red no debe tumbar el polling ni la sesión
    }

    setAcuerdosNoVistos(data)

    if (baselineRef.current === null) {
      baselineRef.current = new Set(data.map(a => a.id))
      return
    }

    const nuevos = data.filter(a => !baselineRef.current.has(a.id))
    if (nuevos.length === 0) return
    nuevos.forEach(a => baselineRef.current.add(a.id))

    // Se avisa sin importar en qué página esté el litigante, incluso si ya
    // está dentro de ese mismo expediente -- la página tiene varias secciones
    // (Notas, Historial de etapas, Acuerdos...) y nada garantiza que esté
    // viendo justo la de Acuerdos cuando llega uno nuevo. Se autocierra solo,
    // así que no estorba si de casualidad sí lo estaba viendo.
    setPopups(prev => [...prev, { id: `popup-${Date.now()}-${Math.random().toString(36).slice(2)}`, acuerdos: nuevos }])
  }, [])

  useEffect(() => {
    if (enLogin || !visible) return

    consultar() // inmediata al montar o al volver de una pestaña oculta
    const intervalId = setInterval(consultar, INTERVALO_MS)
    return () => clearInterval(intervalId)
  }, [enLogin, visible, consultar])

  // Quita un acuerdo de la lista y de cualquier popup que lo siga mostrando --
  // se llama antes de navegar, tanto desde la campana como desde el popup, así
  // ambos quedan sincronizados de inmediato sin esperar al siguiente polling.
  const marcarVistoLocal = useCallback((acuerdoId) => {
    setAcuerdosNoVistos(prev => prev.filter(a => a.id !== acuerdoId))
    setPopups(prev =>
      prev
        .map(p => ({ ...p, acuerdos: p.acuerdos.filter(a => a.id !== acuerdoId) }))
        .filter(p => p.acuerdos.length > 0)
    )
  }, [])

  const cerrarPopup = useCallback((popupId) => {
    setPopups(prev => prev.filter(p => p.id !== popupId))
  }, [])

  return (
    <AcuerdosNoVistosContext.Provider value={{ acuerdosNoVistos, popups, marcarVistoLocal, cerrarPopup }}>
      {children}
    </AcuerdosNoVistosContext.Provider>
  )
}

export function useAcuerdosNoVistos() {
  const ctx = useContext(AcuerdosNoVistosContext)
  if (!ctx) throw new Error('useAcuerdosNoVistos debe usarse dentro de AcuerdosNoVistosProvider')
  return ctx
}
