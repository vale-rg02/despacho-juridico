import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import Select from './Select'

// Reemplazo del <select> nativo (cuyo panel abierto lo dibuja el sistema
// operativo): patrón ARIA "select-only combobox", mismo aspecto que
// ComboboxCatalogo. Lo crítico: que conserve el contrato del <select> (onChange
// recibe string), que el teclado funcione completo y que un Escape que solo
// cierra la lista no cierre también el modal de afuera.
describe('Select', () => {
  const opciones = [
    { valor: '', etiqueta: '— Selecciona —' },
    { valor: 'Civil', etiqueta: 'Civil' },
    { valor: 'Mercantil', etiqueta: 'Mercantil' },
    { valor: 'Familiar', etiqueta: 'Familiar' },
  ]
  const campo = () => screen.getByRole('combobox')

  it('muestra la etiqueta de la opción elegida y la lista cerrada', () => {
    render(<Select value="Mercantil" onChange={() => {}} opciones={opciones} />)

    expect(campo()).toHaveTextContent('Mercantil')
    expect(campo()).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('sin valor (o con valor vacío) muestra el texto de la opción vacía', () => {
    render(<Select value="" onChange={() => {}} opciones={opciones} />)

    expect(campo()).toHaveTextContent('— Selecciona —')
  })

  it('un valor sin opción correspondiente muestra el placeholder', () => {
    render(<Select value="Inexistente" onChange={() => {}} opciones={opciones.slice(1)} placeholder="Elige…" />)

    expect(campo()).toHaveTextContent('Elige…')
  })

  it('al hacer clic abre la lista con todas las opciones y marca la elegida', () => {
    render(<Select value="Civil" onChange={() => {}} opciones={opciones} />)

    fireEvent.click(campo())

    expect(campo()).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getAllByRole('option')).toHaveLength(4)
    expect(screen.getByRole('option', { name: 'Civil' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('option', { name: 'Familiar' })).toHaveAttribute('aria-selected', 'false')
  })

  it('al elegir una opción llama onChange con su valor y cierra la lista', () => {
    const onChange = vi.fn()
    render(<Select value="" onChange={onChange} opciones={opciones} />)

    fireEvent.click(campo())
    fireEvent.click(screen.getByRole('option', { name: 'Familiar' }))

    expect(onChange).toHaveBeenCalledWith('Familiar')
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('igual que un <select>, onChange siempre recibe string aunque el valor de la opción sea número', () => {
    const onChange = vi.fn()
    const prioridades = [{ valor: 0, etiqueta: 'Normal' }, { valor: 2, etiqueta: 'Urgente' }]
    render(<Select value={0} onChange={onChange} opciones={prioridades} />)

    expect(campo()).toHaveTextContent('Normal')
    fireEvent.click(campo())
    fireEvent.click(screen.getByRole('option', { name: 'Urgente' }))

    expect(onChange).toHaveBeenCalledWith('2')
  })

  it('acepta opciones como strings simples', () => {
    const onChange = vi.fn()
    render(<Select value="Socio" onChange={onChange} opciones={['Litigante', 'Socio']} />)

    fireEvent.click(campo())
    fireEvent.click(screen.getByRole('option', { name: 'Litigante' }))

    expect(onChange).toHaveBeenCalledWith('Litigante')
  })

  it('teclado: ↓ abre en la opción actual, ↓ avanza y Enter elige', () => {
    const onChange = vi.fn()
    render(<Select value="Civil" onChange={onChange} opciones={opciones} />)

    fireEvent.keyDown(campo(), { key: 'ArrowDown' })
    expect(campo()).toHaveAttribute('aria-expanded', 'true')
    expect(campo()).toHaveAttribute('aria-activedescendant', screen.getByRole('option', { name: 'Civil' }).id)

    fireEvent.keyDown(campo(), { key: 'ArrowDown' })
    fireEvent.keyDown(campo(), { key: 'Enter' })

    expect(onChange).toHaveBeenCalledWith('Mercantil')
  })

  it('teclado: ↑ no pasa de la primera opción, Fin salta a la última y Inicio a la primera', () => {
    const onChange = vi.fn()
    render(<Select value="Civil" onChange={onChange} opciones={opciones} />)

    fireEvent.keyDown(campo(), { key: 'ArrowDown' })
    fireEvent.keyDown(campo(), { key: 'End' })
    fireEvent.keyDown(campo(), { key: 'Enter' })
    expect(onChange).toHaveBeenLastCalledWith('Familiar')

    fireEvent.keyDown(campo(), { key: 'ArrowDown' })
    fireEvent.keyDown(campo(), { key: 'Home' })
    fireEvent.keyDown(campo(), { key: 'ArrowUp' })
    fireEvent.keyDown(campo(), { key: 'Enter' })
    expect(onChange).toHaveBeenLastCalledWith('')
  })

  it('teclado: Espacio abre y luego elige', () => {
    const onChange = vi.fn()
    render(<Select value="" onChange={onChange} opciones={opciones} />)

    fireEvent.keyDown(campo(), { key: ' ' })
    fireEvent.keyDown(campo(), { key: 'ArrowDown' })
    fireEvent.keyDown(campo(), { key: ' ' })

    expect(onChange).toHaveBeenCalledWith('Civil')
  })

  it('teclado: escribir una letra salta a la opción que empieza con ella (sin importar acentos ni mayúsculas)', () => {
    const onChange = vi.fn()
    const conAcentos = [{ valor: 'a', etiqueta: 'Álamos' }, { valor: 'c', etiqueta: 'Cajeme' }]
    render(<Select value="" onChange={onChange} opciones={conAcentos} />)

    fireEvent.keyDown(campo(), { key: 'c' })
    fireEvent.keyDown(campo(), { key: 'Enter' })

    expect(onChange).toHaveBeenCalledWith('c')
  })

  it('Escape cierra la lista sin elegir y NO deja que el Escape llegue al modal de afuera', () => {
    const onChange = vi.fn()
    const escapeEnDocumento = vi.fn()
    document.addEventListener('keydown', escapeEnDocumento)
    render(<Select value="Civil" onChange={onChange} opciones={opciones} />)

    fireEvent.keyDown(campo(), { key: 'ArrowDown' })
    fireEvent.keyDown(campo(), { key: 'Escape' })

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
    expect(onChange).not.toHaveBeenCalled()
    expect(escapeEnDocumento).not.toHaveBeenCalledWith(expect.objectContaining({ key: 'Escape' }))
    document.removeEventListener('keydown', escapeEnDocumento)
  })

  it('con la lista cerrada, Escape sí llega al modal de afuera (para que pueda cerrarse)', () => {
    const escapeEnDocumento = vi.fn()
    document.addEventListener('keydown', escapeEnDocumento)
    render(<Select value="Civil" onChange={() => {}} opciones={opciones} />)

    fireEvent.keyDown(campo(), { key: 'Escape' })

    expect(escapeEnDocumento).toHaveBeenCalled()
    document.removeEventListener('keydown', escapeEnDocumento)
  })

  it('Tab cierra la lista sin elegir nada', () => {
    const onChange = vi.fn()
    render(<Select value="Civil" onChange={onChange} opciones={opciones} />)

    fireEvent.keyDown(campo(), { key: 'ArrowDown' })
    fireEvent.keyDown(campo(), { key: 'Tab' })

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
    expect(onChange).not.toHaveBeenCalled()
  })

  it('un clic fuera cierra la lista', () => {
    render(
      <div>
        <Select value="Civil" onChange={() => {}} opciones={opciones} />
        <button>afuera</button>
      </div>
    )

    fireEvent.click(campo())
    fireEvent.mouseDown(screen.getByText('afuera'))

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('deshabilitado: no abre ni con clic ni con teclado, y no entra en la secuencia de Tab', () => {
    render(<Select value="Civil" onChange={() => {}} opciones={opciones} disabled />)

    fireEvent.click(campo())
    fireEvent.keyDown(campo(), { key: 'ArrowDown' })

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
    expect(campo()).toHaveAttribute('aria-disabled', 'true')
    expect(campo()).toHaveAttribute('tabindex', '-1')
  })

  it('usa aria-label cuando se le pasa, para que un lector de pantalla anuncie el campo', () => {
    render(<Select value="" onChange={() => {}} opciones={opciones} aria-label="Materia" />)

    expect(screen.getByRole('combobox', { name: 'Materia' })).toBeInTheDocument()
  })
})
