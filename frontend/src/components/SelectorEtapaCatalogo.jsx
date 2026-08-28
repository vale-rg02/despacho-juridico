// Selector de etapa con soporte para submenú (DJ-76: Remate → 1ra/2da/3ra
// Almoneda). El catálogo llega plano, con `etapaPadreId` marcando qué entradas
// son subetapas de cuáles. Este componente arma la jerarquía a partir de eso:
// el <select> principal solo lista etapas de primer nivel (sin padre); si la
// elegida tiene hijas, aparece un segundo <select> obligatorio para elegir una
// de ellas. El valor que se reporta hacia afuera (onCambiar) SIEMPRE es el id
// de una hoja — nunca el de un padre como "Remate" — porque es lo único que
// HistorialEtapa puede guardar.
import { useEffect, useState } from 'react'

function SelectorEtapaCatalogo({ catalogo, valorId, onCambiar, disabled, className }) {
  const [principalId, setPrincipalId] = useState('')
  const [subetapaId, setSubetapaId] = useState('')

  const etapasPrincipales = catalogo.filter(e => !e.etapaPadreId)
  const subetapas = catalogo.filter(e => String(e.etapaPadreId) === String(principalId))
  const tieneSubmenu = subetapas.length > 0

  // Si valorId llega desde afuera apuntando a una hoja con padre (ej. al editar
  // una etapa ya registrada), reconstruye la selección de dos niveles a partir
  // de ese único id.
  useEffect(() => {
    if (!valorId) {
      setPrincipalId('')
      setSubetapaId('')
      return
    }
    const entrada = catalogo.find(e => String(e.id) === String(valorId))
    if (!entrada) return

    if (entrada.etapaPadreId) {
      setPrincipalId(String(entrada.etapaPadreId))
      setSubetapaId(String(entrada.id))
    } else {
      setPrincipalId(String(entrada.id))
      setSubetapaId('')
    }
    // Solo debe recalcularse cuando cambia el catálogo o el valor que viene de
    // afuera — no cuando el usuario interactúa (esos casos ya están cubiertos
    // por handlePrincipalChange/handleSubetapaChange).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [valorId, catalogo])

  function handlePrincipalChange(nuevoId) {
    setPrincipalId(nuevoId)
    setSubetapaId('')

    const hijas = catalogo.filter(e => String(e.etapaPadreId) === String(nuevoId))
    // Si la nueva selección no tiene submenú, ya es una hoja válida por sí sola.
    // Si sí tiene, la selección queda incompleta hasta que se elija una hija.
    onCambiar(hijas.length > 0 ? '' : nuevoId)
  }

  function handleSubetapaChange(nuevoId) {
    setSubetapaId(nuevoId)
    onCambiar(nuevoId)
  }

  const inputBase = "bg-input-background text-foreground text-sm px-3 py-1.5 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"

  return (
    <div className={className}>
      <select
        value={principalId}
        onChange={e => handlePrincipalChange(e.target.value)}
        disabled={disabled}
        className={`w-full cursor-pointer ${inputBase}`}
      >
        <option value="">— Selecciona —</option>
        {etapasPrincipales.map(e => (
          <option key={e.id} value={e.id}>{e.nombre}</option>
        ))}
      </select>

      {tieneSubmenu && (
        <select
          value={subetapaId}
          onChange={e => handleSubetapaChange(e.target.value)}
          disabled={disabled}
          className={`w-full cursor-pointer mt-2 ${inputBase}`}
        >
          <option value="">— Elige cuál {etapasPrincipales.find(e => String(e.id) === principalId)?.nombre.toLowerCase()} —</option>
          {subetapas.map(e => (
            <option key={e.id} value={e.id}>{e.nombre}</option>
          ))}
        </select>
      )}
    </div>
  )
}

export default SelectorEtapaCatalogo
