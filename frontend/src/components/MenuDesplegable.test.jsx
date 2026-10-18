import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import MenuDesplegable from './MenuDesplegable'

// Menú de botón (patrón ARIA "menu button") que reemplazó a los cinco menús
// armados a mano. Lo crítico: roles ARIA correctos, teclado completo, que un
// Escape que solo cierra el menú no cierre el modal de afuera, y que un
// onSelect que devuelve false (error del servidor) deje el menú abierto.
function renderMenu(props = {}) {
  const items = props.items ?? [
    { clave: 'a', contenido: 'Abierto', seleccionado: true, onSelect: vi.fn() },
    { clave: 'p', contenido: 'Pausado', seleccionado: false, onSelect: vi.fn() },
    { clave: 'c', contenido: 'Cerrado', seleccionado: false, onSelect: vi.fn() },
  ]
  render(
    <MenuDesplegable
      etiqueta="Cambiar estado"
      boton={({ abierto }) => <span>Estado {abierto ? '(abierto)' : ''}</span>}
      items={items}
      {...props}
    />
  )
  return { items }
}

const boton = () => screen.getByRole('button', { name: /Estado/ })

describe('MenuDesplegable', () => {
  it('cerrado: el botón anuncia que abre un menú y que está colapsado', () => {
    renderMenu()

    expect(boton()).toHaveAttribute('aria-haspopup', 'menu')
    expect(boton()).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
  })

  it('al hacer clic abre el menú con nombre accesible y sus opciones', () => {
    renderMenu()

    fireEvent.click(boton())

    expect(boton()).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByRole('menu', { name: 'Cambiar estado' })).toBeInTheDocument()
    expect(screen.getAllByRole('menuitemradio')).toHaveLength(3)
  })

  it('las opciones de selección única indican cuál está marcada', () => {
    renderMenu()
    fireEvent.click(boton())

    expect(screen.getByRole('menuitemradio', { name: 'Abierto' })).toHaveAttribute('aria-checked', 'true')
    expect(screen.getByRole('menuitemradio', { name: 'Pausado' })).toHaveAttribute('aria-checked', 'false')
  })

  it('las opciones sin "seleccionado" son acciones simples (menuitem)', () => {
    renderMenu({ items: [{ clave: 'x', contenido: 'Carlos', onSelect: vi.fn() }] })
    fireEvent.click(boton())

    expect(screen.getByRole('menuitem', { name: 'Carlos' })).toBeInTheDocument()
    expect(screen.queryByRole('menuitemradio')).not.toBeInTheDocument()
  })

  it('al abrir, el foco pasa a la opción marcada', async () => {
    renderMenu()
    fireEvent.click(boton())

    await waitFor(() => expect(screen.getByRole('menuitemradio', { name: 'Abierto' })).toHaveFocus())
  })

  it('al elegir llama onSelect, cierra y devuelve el foco al botón', async () => {
    const { items } = renderMenu()
    fireEvent.click(boton())
    fireEvent.click(screen.getByRole('menuitemradio', { name: 'Pausado' }))

    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument())
    expect(items[1].onSelect).toHaveBeenCalledTimes(1)
    expect(boton()).toHaveFocus()
  })

  it('teclado: ↓ en el botón abre; ↓/↑ navegan en círculo; Inicio y Fin saltan', async () => {
    renderMenu()
    fireEvent.keyDown(boton(), { key: 'ArrowDown' })
    const [uno, dos, tres] = await screen.findAllByRole('menuitemradio')
    await waitFor(() => expect(uno).toHaveFocus())

    fireEvent.keyDown(screen.getByRole('menu'), { key: 'ArrowDown' })
    expect(dos).toHaveFocus()
    fireEvent.keyDown(screen.getByRole('menu'), { key: 'End' })
    expect(tres).toHaveFocus()
    fireEvent.keyDown(screen.getByRole('menu'), { key: 'ArrowDown' })
    expect(uno).toHaveFocus() // da la vuelta
    fireEvent.keyDown(screen.getByRole('menu'), { key: 'ArrowUp' })
    expect(tres).toHaveFocus()
    fireEvent.keyDown(screen.getByRole('menu'), { key: 'Home' })
    expect(uno).toHaveFocus()
  })

  it('teclado: ↑ en el botón abre enfocando la última opción', async () => {
    renderMenu()
    fireEvent.keyDown(boton(), { key: 'ArrowUp' })

    await waitFor(() => expect(screen.getByRole('menuitemradio', { name: 'Cerrado' })).toHaveFocus())
  })

  it('Escape cierra solo el menú, devuelve el foco al botón y NO llega al modal de afuera', async () => {
    const escapeEnDocumento = vi.fn()
    document.addEventListener('keydown', escapeEnDocumento)
    renderMenu()
    fireEvent.click(boton())
    await screen.findByRole('menu')

    fireEvent.keyDown(screen.getByRole('menuitemradio', { name: 'Abierto' }), { key: 'Escape' })

    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
    expect(boton()).toHaveFocus()
    expect(escapeEnDocumento).not.toHaveBeenCalled()
    document.removeEventListener('keydown', escapeEnDocumento)
  })

  it('clic fuera cierra el menú', () => {
    render(
      <div>
        <MenuDesplegable etiqueta="m" boton={() => 'Estado'} items={[{ clave: 'a', contenido: 'A', onSelect: vi.fn() }]} />
        <button>afuera</button>
      </div>
    )
    fireEvent.click(screen.getByRole('button', { name: 'Estado' }))
    expect(screen.getByRole('menu')).toBeInTheDocument()

    fireEvent.mouseDown(screen.getByText('afuera'))

    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
  })

  it('si onSelect devuelve false (error del servidor), el menú se queda abierto', async () => {
    const onSelect = vi.fn().mockResolvedValue(false)
    renderMenu({ items: [{ clave: 'u', contenido: 'Carlos', onSelect }] })
    fireEvent.click(boton())
    fireEvent.click(screen.getByRole('menuitem', { name: 'Carlos' }))

    await waitFor(() => expect(onSelect).toHaveBeenCalled())
    expect(screen.getByRole('menu')).toBeInTheDocument()
  })

  it('una opción deshabilitada no dispara onSelect', () => {
    const onSelect = vi.fn()
    renderMenu({ items: [{ clave: 'u', contenido: 'Carlos', onSelect, disabled: true }] })
    fireEvent.click(boton())

    fireEvent.click(screen.getByRole('menuitem', { name: 'Carlos' }))

    expect(onSelect).not.toHaveBeenCalled()
  })

  it('sin opciones muestra el contenido de "vacio" y el encabezado', () => {
    renderMenu({ items: [], vacio: <p>No hay usuarios disponibles</p>, encabezado: <p role="alert">Falló</p> })
    fireEvent.click(boton())

    expect(screen.getByText('No hay usuarios disponibles')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Falló')
  })

  it('los separadores se anuncian como tal y no cuentan como opciones', () => {
    renderMenu({
      items: [
        { clave: 'a', contenido: 'Todos', onSelect: vi.fn() },
        { separador: true },
        { clave: 'b', contenido: 'Carlos', onSelect: vi.fn() },
      ],
    })
    fireEvent.click(boton())

    expect(screen.getByRole('separator')).toBeInTheDocument()
    expect(screen.getAllByRole('menuitem')).toHaveLength(2)
  })
})
