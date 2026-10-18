import { useEffect } from 'react'

const NOMBRE_APP = 'Acedo e Hijos'

// WCAG 2.4.2: cada pantalla debe tener un título que la distinga -- antes todas
// decían "Acedo e Hijos", así que la pestaña del navegador, el historial y el
// lector de pantalla no diferenciaban una pantalla de otra.
export function useTituloPagina(titulo) {
  useEffect(() => {
    document.title = titulo ? `${titulo} · ${NOMBRE_APP}` : NOMBRE_APP
  }, [titulo])
}
