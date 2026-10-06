import { useEffect, useState } from 'react'

// DJ-127: para pausar el polling de acuerdos nuevos mientras la pestaña está
// oculta (no tiene sentido consultar el servidor si el litigante no la está
// viendo) y reanudar con una consulta inmediata al volver.
export function usePestanaVisible() {
  const [visible, setVisible] = useState(!document.hidden)

  useEffect(() => {
    function handler() {
      setVisible(!document.hidden)
    }
    document.addEventListener('visibilitychange', handler)
    return () => document.removeEventListener('visibilitychange', handler)
  }, [])

  return visible
}
