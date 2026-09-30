import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import SelectorBancoOParticular from './SelectorBancoOParticular'

// La parte actora de un expediente es un banco del catálogo o, en un "asunto
// particular", el nombre libre de una persona -- nunca ambos a la vez (la
// exclusividad la garantiza el backend al guardar, ver ParteActoraParticularTests
// en el backend). Aquí solo se cubre el comportamiento de la UI: cuál panel se
// muestra según la pestaña activa, y que cambiar de pestaña no descarta lo
// escrito en la otra mientras se sigue editando.
describe('SelectorBancoOParticular', () => {
  const propsBase = {
    modo: 'banco',
    onCambiarModo: vi.fn(),
    banco: '',
    onCambiarBanco: vi.fn(),
    bancosOpciones: ['BBVA México', 'HSBC'],
    onAgregarBanco: vi.fn(),
    parteActoraParticular: '',
    onCambiarParteActoraParticular: vi.fn(),
  }

  it('con modo "banco" muestra el combobox de banco, no el input de particular', () => {
    render(<SelectorBancoOParticular {...propsBase} />)

    expect(screen.getByPlaceholderText('— Sin banco — o escribe para buscar...')).toBeInTheDocument()
    expect(screen.queryByPlaceholderText('Nombre completo de la parte actora')).not.toBeInTheDocument()
  })

  it('con modo "particular" muestra el input de texto libre, no el combobox de banco', () => {
    render(<SelectorBancoOParticular {...propsBase} modo="particular" />)

    expect(screen.getByPlaceholderText('Nombre completo de la parte actora')).toBeInTheDocument()
    expect(screen.queryByPlaceholderText('— Sin banco — o escribe para buscar...')).not.toBeInTheDocument()
  })

  it('clic en la pestaña "Asunto particular" llama onCambiarModo("particular")', () => {
    const onCambiarModo = vi.fn()
    render(<SelectorBancoOParticular {...propsBase} onCambiarModo={onCambiarModo} />)

    fireEvent.click(screen.getByText('Asunto particular'))

    expect(onCambiarModo).toHaveBeenCalledWith('particular')
  })

  it('clic en la pestaña "Banco" llama onCambiarModo("banco")', () => {
    const onCambiarModo = vi.fn()
    render(<SelectorBancoOParticular {...propsBase} modo="particular" onCambiarModo={onCambiarModo} />)

    fireEvent.click(screen.getByText('Banco'))

    expect(onCambiarModo).toHaveBeenCalledWith('banco')
  })

  it('escribir en el input de particular llama onCambiarParteActoraParticular con el texto', () => {
    const onCambiarParteActoraParticular = vi.fn()
    render(<SelectorBancoOParticular {...propsBase} modo="particular" onCambiarParteActoraParticular={onCambiarParteActoraParticular} />)

    fireEvent.change(screen.getByPlaceholderText('Nombre completo de la parte actora'), { target: { value: 'María López' } })

    expect(onCambiarParteActoraParticular).toHaveBeenCalledWith('María López')
  })

  it('el valor de particular se mantiene visible aunque la pestaña activa sea "banco"', () => {
    // Cambiar de pestaña en la UI no borra lo escrito en la otra -- eso lo
    // decide el padre (form) al hacer submit, no este componente.
    render(<SelectorBancoOParticular {...propsBase} modo="particular" parteActoraParticular="María López" />)
    expect(screen.getByDisplayValue('María López')).toBeInTheDocument()
  })
})
