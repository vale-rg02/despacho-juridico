import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import ComboboxCatalogo from './ComboboxCatalogo'

// DJ-112/DJ-87: el combobox genérico reusado para Sede y Juzgado -- lo que más
// importa probar es que Juzgado (DJ-87) ya no acepta texto libre y que el
// filtrado funciona, ya que es la base del combobox dependiente del formulario
// de expediente.
describe('ComboboxCatalogo', () => {
  const opcionesHermosillo = ['1ro Civil Hermosillo', '2do Civil Hermosillo', '1ro Oral Mercantil Hermosillo']

  it('muestra todas las opciones al enfocar, sin filtrar todavía', () => {
    render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesHermosillo} />)

    fireEvent.focus(screen.getByRole('textbox'))

    for (const opcion of opcionesHermosillo) {
      expect(screen.getByText(opcion)).toBeInTheDocument()
    }
  })

  it('filtra las opciones según el texto escrito', () => {
    render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesHermosillo} />)

    const input = screen.getByRole('textbox')
    fireEvent.focus(input)
    fireEvent.change(input, { target: { value: 'oral' } })

    expect(screen.getByText('1ro Oral Mercantil Hermosillo')).toBeInTheDocument()
    expect(screen.queryByText('1ro Civil Hermosillo')).not.toBeInTheDocument()
    expect(screen.queryByText('2do Civil Hermosillo')).not.toBeInTheDocument()
  })

  it('al hacer clic en una opción, llama onChange con ese valor', () => {
    const onChange = vi.fn()
    render(<ComboboxCatalogo value="" onChange={onChange} opciones={opcionesHermosillo} />)

    fireEvent.focus(screen.getByRole('textbox'))
    fireEvent.click(screen.getByText('2do Civil Hermosillo'))

    expect(onChange).toHaveBeenCalledWith('2do Civil Hermosillo')
  })

  it('DJ-87: si se escribe texto que no coincide con ninguna opción y se quita el foco, revierte (ya no acepta texto libre)', () => {
    const onChange = vi.fn()
    const { container } = render(
      <div>
        <ComboboxCatalogo value="1ro Civil Hermosillo" onChange={onChange} opciones={opcionesHermosillo} />
        <button>afuera</button>
      </div>
    )

    const input = screen.getByRole('textbox')
    fireEvent.focus(input)
    fireEvent.change(input, { target: { value: 'texto que no existe en el catalogo' } })

    // clic afuera del combobox -- simula perder el foco
    fireEvent.mouseDown(screen.getByText('afuera'))

    expect(input.value).toBe('1ro Civil Hermosillo') // revertido, no se quedó el texto libre
    expect(onChange).not.toHaveBeenCalled() // nunca se confirmó un valor inválido
  })

  it('no muestra "+ Agregar nueva" si no se pasa onAgregarNuevo (usuario no-admin)', () => {
    render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesHermosillo} />)

    fireEvent.focus(screen.getByRole('textbox'))

    expect(screen.queryByText(/Agregar nueva/i)).not.toBeInTheDocument()
  })

  it('muestra "+ Agregar nueva" cuando sí se pasa onAgregarNuevo (admin)', () => {
    render(
      <ComboboxCatalogo
        value=""
        onChange={() => {}}
        opciones={opcionesHermosillo}
        onAgregarNuevo={() => {}}
        textoAgregarNuevo="+ Agregar juzgado nuevo"
      />
    )

    fireEvent.focus(screen.getByRole('textbox'))

    expect(screen.getByText('+ Agregar juzgado nuevo')).toBeInTheDocument()
  })
})
