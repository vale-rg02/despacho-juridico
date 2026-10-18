import { useEffect, useId, useMemo, useRef, useState } from 'react'
import { Check, ChevronDown } from 'lucide-react'
import { CAJA_CON_FLECHA, FLECHA, PANEL, claseOpcion, normalizarParaBusqueda } from './desplegable'

// Desplegable de lista cerrada (reemplaza al <select> nativo, cuyo panel abierto
// lo dibuja el sistema operativo y no se puede igualar al de ComboboxCatalogo).
// Patrón ARIA "select-only combobox": el foco se queda en el campo, la lista
// se navega con aria-activedescendant.
//
// Teclado: ↓/↑ abren y mueven, Inicio/Fin saltan, Enter/Espacio abren o
// eligen, Escape cierra (sin cerrar el modal que lo contenga), Tab cierra y
// sigue de largo, y escribir una letra salta a la opción que empieza con ella.
//
// `opciones`: [{ valor, etiqueta }] o strings. Igual que un <select>, `onChange`
// siempre recibe el valor como string -- así los formularios que ya hacían
// Number(...)/parseInt(...) sobre e.target.value no cambian.
function Select({
  id,
  value,
  onChange,
  opciones,
  placeholder = '— Selecciona —',
  disabled = false,
  className = '',
  'aria-label': ariaLabel,
  'aria-labelledby': ariaLabelledby,
}) {
  const items = useMemo(
    () => opciones.map(o => (typeof o === 'object' ? { valor: String(o.valor), etiqueta: o.etiqueta } : { valor: o, etiqueta: o })),
    [opciones]
  )
  const idBase = useId()
  const listaId = `${idBase}-lista`
  const opcionId = i => `${idBase}-op-${i}`

  const [abierto, setAbierto] = useState(false)
  const [activo, setActivo] = useState(-1)
  const contenedorRef = useRef(null)
  const opcionesRefs = useRef([])
  const bufferRef = useRef({ texto: '', timeout: null })

  const valorActual = value === undefined || value === null ? '' : String(value)
  const seleccionado = items.findIndex(o => o.valor === valorActual)
  const etiquetaActual = seleccionado >= 0 ? items[seleccionado].etiqueta : placeholder
  const esVacio = seleccionado < 0 || items[seleccionado].valor === ''

  function abrir() {
    if (disabled) return
    setAbierto(true)
    setActivo(seleccionado >= 0 ? seleccionado : 0)
  }

  function cerrar() {
    setAbierto(false)
    setActivo(-1)
  }

  function elegir(i) {
    const item = items[i]
    if (!item) return
    onChange(item.valor)
    cerrar()
  }

  useEffect(() => {
    if (!abierto) return
    function handleClickAfuera(e) {
      if (contenedorRef.current && !contenedorRef.current.contains(e.target)) cerrar()
    }
    document.addEventListener('mousedown', handleClickAfuera)
    return () => document.removeEventListener('mousedown', handleClickAfuera)
  }, [abierto])

  useEffect(() => {
    const el = activo >= 0 ? opcionesRefs.current[activo] : null
    if (typeof el?.scrollIntoView === 'function') el.scrollIntoView({ block: 'nearest' })
  }, [activo])

  function buscarPorLetra(tecla) {
    const buffer = bufferRef.current
    clearTimeout(buffer.timeout)
    buffer.texto += tecla
    buffer.timeout = setTimeout(() => { buffer.texto = '' }, 600)

    const prefijo = normalizarParaBusqueda(buffer.texto)
    // Misma letra repetida (aaa) recorre las opciones que empiezan con ella.
    const repetida = buffer.texto.split('').every(c => c === buffer.texto[0])
    const termino = repetida ? normalizarParaBusqueda(buffer.texto[0]) : prefijo
    const desde = (abierto ? activo : seleccionado) + 1
    for (let k = 0; k < items.length; k++) {
      const i = (desde + k) % items.length
      if (normalizarParaBusqueda(items[i].etiqueta).startsWith(termino)) {
        if (!abierto) setAbierto(true)
        setActivo(i)
        return
      }
    }
  }

  function handleKeyDown(e) {
    if (disabled) return
    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault()
        if (!abierto) abrir()
        else setActivo(i => Math.min(i + 1, items.length - 1))
        break
      case 'ArrowUp':
        e.preventDefault()
        if (!abierto) abrir()
        else setActivo(i => Math.max(i - 1, 0))
        break
      case 'Home':
        if (abierto) { e.preventDefault(); setActivo(0) }
        break
      case 'End':
        if (abierto) { e.preventDefault(); setActivo(items.length - 1) }
        break
      case 'Enter':
      case ' ':
        e.preventDefault()
        if (!abierto) abrir()
        else elegir(activo)
        break
      case 'Escape':
        if (abierto) {
          // stopPropagation: que un Escape que solo cierra la lista no cierre
          // también el modal de afuera (useCerrarConEscape escucha en document).
          e.preventDefault()
          e.stopPropagation()
          cerrar()
        }
        break
      case 'Tab':
        cerrar()
        break
      default:
        if (e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) buscarPorLetra(e.key)
    }
  }

  return (
    <div ref={contenedorRef} className={`relative ${className}`}>
      <div
        id={id}
        role="combobox"
        tabIndex={disabled ? -1 : 0}
        aria-haspopup="listbox"
        aria-expanded={abierto}
        aria-controls={listaId}
        aria-activedescendant={abierto && activo >= 0 ? opcionId(activo) : undefined}
        aria-disabled={disabled || undefined}
        aria-label={ariaLabel}
        aria-labelledby={ariaLabelledby}
        onClick={() => (abierto ? cerrar() : abrir())}
        onKeyDown={handleKeyDown}
        onBlur={cerrar}
        className={`${CAJA_CON_FLECHA} flex items-center ${disabled ? 'opacity-60 cursor-not-allowed' : 'cursor-pointer'}`}
      >
        <span className={`truncate ${esVacio ? 'text-muted-foreground' : ''}`}>{etiquetaActual}</span>
      </div>
      <ChevronDown size={14} aria-hidden="true" className={`${FLECHA} ${abierto ? 'rotate-180' : ''}`} />

      {abierto && !disabled && (
        <div
          id={listaId}
          role="listbox"
          // El foco debe quedarse en el campo: sin esto, un clic en la barra de
          // scroll o en una opción dispara onBlur y cierra la lista antes del clic.
          onMouseDown={e => e.preventDefault()}
          className={PANEL}
        >
          {items.map((item, i) => (
            <div
              key={item.valor}
              id={opcionId(i)}
              ref={el => { opcionesRefs.current[i] = el }}
              role="option"
              aria-selected={i === seleccionado}
              onMouseEnter={() => setActivo(i)}
              onClick={() => elegir(i)}
              className={`${claseOpcion({ activa: i === activo, seleccionada: i === seleccionado })} cursor-pointer`}
            >
              <span className={item.valor === '' ? 'text-muted-foreground' : ''}>{item.etiqueta}</span>
              {i === seleccionado && <Check size={14} aria-hidden="true" className="text-accent shrink-0" />}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

export default Select
