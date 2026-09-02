import { useEffect } from 'react'

// Cierra un modal/dropdown al presionar Escape. Uso: useCerrarConEscape(onCerrar)
export function useCerrarConEscape(onCerrar) {
  useEffect(() => {
    function handleKeyDown(e) {
      if (e.key === 'Escape') onCerrar()
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [onCerrar])
}
