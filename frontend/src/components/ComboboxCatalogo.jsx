import { useEffect, useRef, useState } from 'react'
import { useCerrarConEscape } from '../hooks/useCerrarConEscape'

const inputBase = "w-full bg-input-background text-foreground text-sm px-3 py-2 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"

// Insensible a mayúsculas Y a acentos, mismo criterio que unaccent() del
// backend (DJ-110, AplicarFiltroBusqueda en ExpedientesController) -- solo
// para COMPARAR durante la búsqueda; el valor mostrado/guardado siempre es el
// texto original de `opciones`, con sus acentos correctos (ej. "Álamos").
// ̀-ͯ es el bloque Unicode de marcas diacríticas combinantes (lo que
// normalize('NFD') separa de la letra base, ej. "Á" -> "A" + acento combinante).
function normalizarParaBusqueda(texto) {
  return texto
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
}

// DJ-112/DJ-87: combobox genérico con autocompletado, reusado para Sede y
// Juzgado. Solo acepta un valor que exista en `opciones` (nombres) -- a
// diferencia de un <input> libre, si el usuario escribe algo que no coincide
// con ninguna opción y quita el foco, el valor se revierte (Juzgado ya no
// acepta texto libre, DJ-87).
//
// `opciones`: array de strings (nombres ya listos para mostrar/guardar).
// `onAgregarNuevo`: opcional -- si se pasa, muestra un link "+ Agregar nueva"
// al final de la lista filtrada (solo admin, ver NuevoExpediente.jsx).
function ComboboxCatalogo({
  label,
  value,
  onChange,
  opciones,
  placeholder = 'Escribe para buscar...',
  disabled = false,
  onAgregarNuevo,
  textoAgregarNuevo = '+ Agregar nueva',
}) {
  const [abierto, setAbierto] = useState(false)
  const [texto, setTexto] = useState(value || '')
  const contenedorRef = useRef(null)

  // Si el valor cambia desde afuera (ej. se resetea Juzgado al cambiar Sede),
  // el texto visible se sincroniza.
  useEffect(() => {
    setTexto(value || '')
  }, [value])

  useCerrarConEscape(() => setAbierto(false))

  function cerrarYRevertirSiInvalido() {
    setAbierto(false)
    if (texto !== (value || '') && !opciones.includes(texto)) {
      setTexto(value || '') // revierte -- no se acepta texto libre
    }
  }

  useEffect(() => {
    function handleClickAfuera(e) {
      if (contenedorRef.current && !contenedorRef.current.contains(e.target)) {
        cerrarYRevertirSiInvalido()
      }
    }
    document.addEventListener('mousedown', handleClickAfuera)
    return () => document.removeEventListener('mousedown', handleClickAfuera)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [texto, opciones])

  function seleccionar(opcion) {
    setTexto(opcion)
    onChange(opcion)
    setAbierto(false)
  }

  const textoNormalizado = normalizarParaBusqueda(texto)
  const filtradas = opciones.filter(o => normalizarParaBusqueda(o).includes(textoNormalizado))

  return (
    <div ref={contenedorRef} className="relative">
      {label && (
        <label className="block text-xs font-medium uppercase tracking-widest text-muted-foreground mb-1.5" style={{ fontFamily: "'DM Mono', monospace" }}>
          {label}
        </label>
      )}
      <input
        type="text"
        value={texto}
        onChange={e => { setTexto(e.target.value); setAbierto(true) }}
        onFocus={() => setAbierto(true)}
        placeholder={placeholder}
        disabled={disabled}
        className={inputBase}
        autoComplete="off"
      />
      {abierto && !disabled && (
        <div className="absolute z-10 mt-1 w-full max-h-56 overflow-y-auto bg-card border border-border rounded-md shadow-lg">
          {filtradas.length === 0 && (
            <p className="px-3 py-2 text-sm text-muted-foreground">Sin resultados</p>
          )}
          {filtradas.map(opcion => (
            <button
              key={opcion}
              type="button"
              onClick={() => seleccionar(opcion)}
              className="w-full text-left px-3 py-2 text-sm hover:bg-accent/10 transition"
            >
              {opcion}
            </button>
          ))}
          {onAgregarNuevo && (
            <button
              type="button"
              onClick={() => { setAbierto(false); onAgregarNuevo() }}
              className="w-full text-left px-3 py-2 text-sm text-accent font-medium hover:bg-accent/10 transition border-t border-border"
            >
              {textoAgregarNuevo}
            </button>
          )}
        </div>
      )}
    </div>
  )
}

export default ComboboxCatalogo
