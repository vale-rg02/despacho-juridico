import { useEffect, useId, useRef, useState } from 'react'
import { Check } from 'lucide-react'
import { PANEL_MENU, claseOpcion } from './desplegable'

// Menú de botón (patrón ARIA "menu button"): un botón que abre una lista de
// acciones o filtros. Reemplaza a los cinco menús que se habían armado a mano
// (estado, prioridad y colaborador en el detalle; filtro de usuario en
// Expedientes y Notificaciones), que no tenían roles ARIA, ni teclado más allá
// de Tab, ni el mismo aspecto entre sí.
//
// Teclado: ↓/↑/Enter/Espacio en el botón abren y enfocan la primera/última
// opción; dentro del menú ↓/↑ navegan (circular), Inicio/Fin saltan, Escape
// cierra y devuelve el foco al botón (sin cerrar un modal que lo contenga), y
// Tab cierra y sigue de largo.
//
// `items`: [{ clave, contenido, onSelect, seleccionado?, disabled? } | { separador: true }].
//   - `seleccionado` definido (true/false) => la opción es de selección única
//     (role="menuitemradio" + check); sin definir => acción simple ("menuitem").
//   - `onSelect` puede ser async: si devuelve `false`, el menú se queda abierto
//     (ej. el servidor rechazó agregar al colaborador).
// `boton`: función ({ abierto }) => nodo con el contenido del botón.
function MenuDesplegable({
  etiqueta,
  boton,
  botonClassName = '',
  botonStyle,
  items,
  encabezado,
  vacio,
  alinear = 'left',
  panelClassName = '',
  className = '',
}) {
  const idBase = useId()
  const menuId = `${idBase}-menu`
  const [abierto, setAbierto] = useState(false)
  const contenedorRef = useRef(null)
  const botonRef = useRef(null)
  const itemsRef = useRef([])
  const enfocarAlAbrir = useRef('primero')

  const accionables = items.filter(i => !i.separador)

  function abrir(cual = 'primero') {
    enfocarAlAbrir.current = cual
    setAbierto(true)
  }

  function cerrar(devolverFoco = false) {
    setAbierto(false)
    if (devolverFoco) botonRef.current?.focus()
  }

  useEffect(() => {
    if (!abierto) return
    function handleClickAfuera(e) {
      if (contenedorRef.current && !contenedorRef.current.contains(e.target)) setAbierto(false)
    }
    document.addEventListener('mousedown', handleClickAfuera)
    return () => document.removeEventListener('mousedown', handleClickAfuera)
  }, [abierto])

  useEffect(() => {
    if (!abierto) return
    const botones = itemsRef.current.filter(Boolean)
    if (botones.length === 0) return
    const marcado = botones.find(b => b.getAttribute('aria-checked') === 'true')
    const destino = enfocarAlAbrir.current === 'ultimo' ? botones[botones.length - 1] : (marcado ?? botones[0])
    destino.focus()
  }, [abierto])

  function moverFoco(delta, absoluto) {
    const botones = itemsRef.current.filter(b => b && !b.disabled)
    if (botones.length === 0) return
    const actual = botones.indexOf(document.activeElement)
    let siguiente
    if (absoluto === 'inicio') siguiente = 0
    else if (absoluto === 'fin') siguiente = botones.length - 1
    else siguiente = (actual + delta + botones.length) % botones.length
    botones[siguiente].focus()
  }

  function handleKeyDownBoton(e) {
    if (e.key === 'ArrowDown' || e.key === 'Enter' || e.key === ' ') {
      e.preventDefault()
      abierto ? cerrar() : abrir('primero')
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      abrir('ultimo')
    } else if (e.key === 'Escape' && abierto) {
      e.preventDefault()
      e.stopPropagation()
      cerrar()
    }
  }

  function handleKeyDownMenu(e) {
    if (e.key === 'ArrowDown') { e.preventDefault(); moverFoco(1) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); moverFoco(-1) }
    else if (e.key === 'Home') { e.preventDefault(); moverFoco(0, 'inicio') }
    else if (e.key === 'End') { e.preventDefault(); moverFoco(0, 'fin') }
    else if (e.key === 'Escape') {
      // stopPropagation: este Escape solo cierra el menú, no el modal de afuera.
      e.preventDefault()
      e.stopPropagation()
      cerrar(true)
    } else if (e.key === 'Tab') {
      cerrar()
    }
  }

  async function elegir(item) {
    const resultado = await item.onSelect?.()
    if (resultado !== false) cerrar(true)
  }

  return (
    <div ref={contenedorRef} className={`relative ${className}`}>
      <button
        ref={botonRef}
        type="button"
        aria-haspopup="menu"
        aria-expanded={abierto}
        aria-controls={abierto ? menuId : undefined}
        onClick={() => (abierto ? cerrar() : abrir('primero'))}
        onKeyDown={handleKeyDownBoton}
        className={botonClassName}
        style={botonStyle}
      >
        {boton({ abierto })}
      </button>

      {abierto && (
        <div
          id={menuId}
          role="menu"
          aria-label={etiqueta}
          onKeyDown={handleKeyDownMenu}
          className={`${PANEL_MENU} ${alinear === 'right' ? 'right-0' : 'left-0'} ${panelClassName}`}
        >
          {encabezado}
          {accionables.length === 0 && vacio}
          {items.map((item, i) => {
            if (item.separador) return <div key={`sep-${i}`} role="separator" className="border-t border-border" />
            const indice = items.slice(0, i).filter(x => !x.separador).length
            const esRadio = item.seleccionado !== undefined
            return (
              <button
                key={item.clave}
                ref={el => { itemsRef.current[indice] = el }}
                type="button"
                role={esRadio ? 'menuitemradio' : 'menuitem'}
                aria-checked={esRadio ? item.seleccionado : undefined}
                tabIndex={-1}
                disabled={item.disabled}
                onClick={() => elegir(item)}
                className={`${claseOpcion({ activa: false, seleccionada: item.seleccionado })} disabled:opacity-50 focus-visible:bg-accent/10`}
              >
                <span className="flex items-center gap-2 min-w-0">{item.contenido}</span>
                {item.seleccionado && <Check size={14} aria-hidden="true" className="text-accent-strong shrink-0" />}
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}

export default MenuDesplegable
