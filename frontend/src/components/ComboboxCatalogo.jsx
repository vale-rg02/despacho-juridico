import { useEffect, useRef, useState } from 'react'
import { ChevronDown } from 'lucide-react'
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

// DJ-112/DJ-87: combobox genérico con autocompletado, reusado para Sede,
// Juzgado y Banco. Solo acepta un valor que exista en `opciones` (nombres) --
// a diferencia de un <input> libre, si el usuario escribe algo que no
// coincide con ninguna opción y quita el foco, el valor se revierte (Juzgado
// ya no acepta texto libre, DJ-87).
//
// DJ-123: navegación completa por teclado (ArrowDown/Up navega opciones,
// Enter selecciona, Escape cierra y revierte si quedó texto inválido, Tab
// mueve el foco al siguiente campo sin quedar atrapado -- las opciones usan
// tabIndex={-1}, no son parte de la secuencia de Tab, se navegan con flechas).
// También corrige que al abrir con un valor ya elegido (ej. Sede="Hermosillo")
// se mostraban solo las opciones que ya calzaban con ese texto -- ahora, hasta
// que el usuario escribe algo nuevo, se muestra el catálogo completo.
//
// `opciones`: array de strings (nombres ya listos para mostrar/guardar).
// `onAgregarNuevo`: opcional -- si se pasa, muestra un link "+ Agregar nueva"
// al final de la lista filtrada (solo admin, ver NuevoExpediente.jsx), y
// participa en la navegación por flechas como un elemento más de la lista.
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
  // Solo true después de que el usuario escribe algo desde que se abrió --
  // mientras sea false, se ignora `texto` para filtrar y se muestra todo.
  const [filtroActivo, setFiltroActivo] = useState(false)
  const [activo, setActivo] = useState(-1)
  const contenedorRef = useRef(null)
  const opcionesRefs = useRef([])

  // Si el valor cambia desde afuera (ej. se resetea Juzgado al cambiar Sede),
  // el texto visible se sincroniza.
  useEffect(() => {
    setTexto(value || '')
  }, [value])

  function cerrar() {
    setAbierto(false)
    setFiltroActivo(false)
    setActivo(-1)
  }

  useCerrarConEscape(cerrar)

  function cerrarYRevertirSiInvalido() {
    cerrar()
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
    cerrar()
  }

  const textoParaFiltrar = filtroActivo ? texto : ''
  const textoNormalizado = normalizarParaBusqueda(textoParaFiltrar)
  const filtradas = opciones.filter(o => normalizarParaBusqueda(o).includes(textoNormalizado))
  // Lista unificada para navegar con flechas: las opciones + "+ Agregar nueva"
  // al final (si aplica), para que Enter también la pueda disparar.
  const items = onAgregarNuevo ? [...filtradas, { esAgregar: true }] : filtradas

  useEffect(() => {
    const elementoActivo = activo >= 0 ? opcionesRefs.current[activo] : null
    if (typeof elementoActivo?.scrollIntoView === 'function') {
      elementoActivo.scrollIntoView({ block: 'nearest' })
    }
  }, [activo])

  function handleKeyDown(e) {
    if (disabled) return
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      if (!abierto) { setAbierto(true); setActivo(0); return }
      setActivo(i => Math.min(i + 1, items.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      if (!abierto) { setAbierto(true); return }
      setActivo(i => Math.max(i - 1, 0))
    } else if (e.key === 'Enter') {
      if (!abierto) return
      e.preventDefault()
      let indice = activo
      if (indice === -1) {
        if (items.length !== 1) return // ambiguo -- obliga a elegir con flechas
        indice = 0
      }
      const item = items[indice]
      if (!item) return
      if (item.esAgregar) {
        cerrar()
        onAgregarNuevo()
      } else {
        seleccionar(item)
      }
    } else if (e.key === 'Escape') {
      if (abierto) {
        e.preventDefault()
        e.stopPropagation()
        cerrarYRevertirSiInvalido()
      }
    }
  }

  // Cierra y revierte igual que un clic afuera -- pero vía Tab/Shift+Tab, que
  // no dispara mousedown. relatedTarget evita cerrar de más cuando el foco se
  // mueve HACIA una opción de la propia lista (click con mouse en una opción
  // también dispara blur antes que el onClick).
  function handleBlur(e) {
    if (contenedorRef.current && e.relatedTarget && contenedorRef.current.contains(e.relatedTarget)) {
      return
    }
    cerrarYRevertirSiInvalido()
  }

  return (
    <div ref={contenedorRef} className="relative">
      {label && (
        <label className="block text-xs font-medium uppercase tracking-widest text-muted-foreground mb-1.5" style={{ fontFamily: "'DM Mono', monospace" }}>
          {label}
        </label>
      )}
      <div className="relative">
        <input
          type="text"
          role="combobox"
          aria-expanded={abierto}
          aria-autocomplete="list"
          value={texto}
          onChange={e => { setTexto(e.target.value); setFiltroActivo(true); setActivo(-1); setAbierto(true) }}
          onFocus={() => setAbierto(true)}
          onKeyDown={handleKeyDown}
          onBlur={handleBlur}
          placeholder={placeholder}
          disabled={disabled}
          className={inputBase}
          style={{ paddingRight: '2.25rem' }}
          autoComplete="off"
        />
        <ChevronDown
          size={14}
          aria-hidden="true"
          className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground"
        />
      </div>
      {abierto && !disabled && (
        <div role="listbox" tabIndex={-1} className="absolute z-10 mt-1 w-full max-h-56 overflow-y-auto bg-card border border-border rounded-md shadow-lg">
          {filtradas.length === 0 && (
            <p className="px-3 py-2 text-sm text-muted-foreground">Sin resultados</p>
          )}
          {filtradas.map((opcion, i) => (
            <button
              key={opcion}
              ref={el => { opcionesRefs.current[i] = el }}
              type="button"
              tabIndex={-1}
              role="option"
              aria-selected={i === activo}
              onMouseEnter={() => setActivo(i)}
              onClick={() => seleccionar(opcion)}
              className={`w-full text-left px-3 py-2 text-sm transition ${i === activo ? 'bg-accent/10' : 'hover:bg-accent/10'}`}
            >
              {opcion}
            </button>
          ))}
          {onAgregarNuevo && (
            <button
              type="button"
              tabIndex={-1}
              onMouseEnter={() => setActivo(items.length - 1)}
              onClick={() => { cerrar(); onAgregarNuevo() }}
              className={`w-full text-left px-3 py-2 text-sm text-accent font-medium transition border-t border-border ${items.length - 1 === activo ? 'bg-accent/10' : 'hover:bg-accent/10'}`}
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
