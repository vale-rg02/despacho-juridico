import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import HistorialEtapas from './HistorialEtapas'
import { agregarNotaEtapa } from '../services/etapas'

vi.mock('../services/etapas')

// Historial de notas por etapa: "+ Agregar nota" es un flujo aparte de
// Editar etapa -- cubre que la lista se muestra, que agregar una nota nueva
// no borra las anteriores, y que el formulario inline funciona sin recargar
// todo el historial.
describe('HistorialEtapas - historial de notas', () => {
  const etapaBase = {
    id: 1,
    etapaCatalogoId: 1,
    etapaNombre: 'Contestación',
    fechaInicio: '2026-09-01T00:00:00',
    fechaLimite: null,
    fechaCompletada: null,
    notas: [
      { id: 10, texto: 'Primera nota', creadoEn: '2026-09-01T10:00:00', creadoPorNombre: 'Mario Acedo' },
    ],
  }

  const props = {
    expedienteId: '5',
    onCompletar: vi.fn(),
    onRevertir: vi.fn(),
    onEditar: vi.fn(),
    onEliminar: vi.fn(),
    onNotaAgregada: vi.fn(),
  }

  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('muestra la lista de notas existentes con texto, fecha y autor', () => {
    render(<HistorialEtapas etapas={[etapaBase]} {...props} />)

    expect(screen.getByText('Primera nota')).toBeInTheDocument()
    expect(screen.getByText(/Mario Acedo/)).toBeInTheDocument()
  })

  it('"+ Agregar nota" abre un textarea inline', () => {
    render(<HistorialEtapas etapas={[etapaBase]} {...props} />)

    fireEvent.click(screen.getByText('+ Agregar nota'))

    expect(screen.getByPlaceholderText('Escribe la nota...')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /guardar nota/i })).toBeInTheDocument()
  })

  it('agregar una nota nueva no borra la anterior y llama onNotaAgregada', async () => {
    agregarNotaEtapa.mockResolvedValue({ id: 20, texto: 'Nota nueva', creadoEn: '2026-09-25T09:00:00', creadoPorNombre: 'Carlos' })
    render(<HistorialEtapas etapas={[etapaBase]} {...props} />)

    fireEvent.click(screen.getByText('+ Agregar nota'))
    fireEvent.change(screen.getByPlaceholderText('Escribe la nota...'), { target: { value: 'Nota nueva' } })
    fireEvent.click(screen.getByRole('button', { name: /guardar nota/i }))

    await waitFor(() => expect(agregarNotaEtapa).toHaveBeenCalledWith('5', 1, 'Nota nueva'))
    await waitFor(() => expect(props.onNotaAgregada).toHaveBeenCalledWith(1, expect.objectContaining({ texto: 'Nota nueva' })))

    // La nota original sigue en el DOM -- guardar una nota nueva no la reemplaza
    expect(screen.getByText('Primera nota')).toBeInTheDocument()
    // El formulario se cierra tras guardar
    expect(screen.queryByPlaceholderText('Escribe la nota...')).not.toBeInTheDocument()
  })

  it('guardar está deshabilitado con el textarea vacío', () => {
    render(<HistorialEtapas etapas={[etapaBase]} {...props} />)

    fireEvent.click(screen.getByText('+ Agregar nota'))

    expect(screen.getByRole('button', { name: /guardar nota/i })).toBeDisabled()
  })

  it('cancelar cierra el formulario sin llamar al servicio', () => {
    render(<HistorialEtapas etapas={[etapaBase]} {...props} />)

    fireEvent.click(screen.getByText('+ Agregar nota'))
    fireEvent.change(screen.getByPlaceholderText('Escribe la nota...'), { target: { value: 'Algo' } })
    fireEvent.click(screen.getByRole('button', { name: /cancelar/i }))

    expect(screen.queryByPlaceholderText('Escribe la nota...')).not.toBeInTheDocument()
    expect(agregarNotaEtapa).not.toHaveBeenCalled()
  })

  it('una etapa sin notas no muestra la lista, solo el botón de agregar', () => {
    render(<HistorialEtapas etapas={[{ ...etapaBase, notas: [] }]} {...props} />)

    expect(screen.queryByText('Primera nota')).not.toBeInTheDocument()
    expect(screen.getByText('+ Agregar nota')).toBeInTheDocument()
  })

  describe('colapsado por defecto (varias notas)', () => {
    const etapaConHistorial = {
      ...etapaBase,
      notas: [
        { id: 10, texto: 'Nota vieja', creadoEn: '2026-09-01T10:00:00', creadoPorNombre: 'Mario Acedo' },
        { id: 11, texto: 'Nota más reciente', creadoEn: '2026-09-20T10:00:00', creadoPorNombre: 'Carlos' },
      ],
    }

    it('por defecto solo muestra la nota más reciente, no las anteriores', () => {
      render(<HistorialEtapas etapas={[etapaConHistorial]} {...props} />)

      expect(screen.getByText('Nota más reciente')).toBeInTheDocument()
      expect(screen.queryByText('Nota vieja')).not.toBeInTheDocument()
      expect(screen.getByText(/Ver 1 nota anterior/)).toBeInTheDocument()
    })

    it('expandir el historial muestra la nota anterior sin ocultar la más reciente', () => {
      render(<HistorialEtapas etapas={[etapaConHistorial]} {...props} />)

      fireEvent.click(screen.getByText(/Ver 1 nota anterior/))

      expect(screen.getByText('Nota vieja')).toBeInTheDocument()
      expect(screen.getByText('Nota más reciente')).toBeInTheDocument()
      expect(screen.getByText('Ocultar historial')).toBeInTheDocument()
    })

    it('colapsar de nuevo vuelve a ocultar la nota anterior', () => {
      render(<HistorialEtapas etapas={[etapaConHistorial]} {...props} />)

      fireEvent.click(screen.getByText(/Ver 1 nota anterior/))
      fireEvent.click(screen.getByText('Ocultar historial'))

      expect(screen.queryByText('Nota vieja')).not.toBeInTheDocument()
      expect(screen.getByText(/Ver 1 nota anterior/)).toBeInTheDocument()
    })

    it('con una sola nota no muestra ningún control de expandir', () => {
      render(<HistorialEtapas etapas={[etapaBase]} {...props} />)

      expect(screen.queryByText(/Ver.*nota/)).not.toBeInTheDocument()
      expect(screen.queryByText('Ocultar historial')).not.toBeInTheDocument()
    })
  })

  it('"+ Agregar nota" vive en la misma fila que Editar y Eliminar', () => {
    render(<HistorialEtapas etapas={[etapaBase]} {...props} />)

    const eliminar = screen.getByText('Eliminar')
    const editar = screen.getByText('Editar')
    const agregarNota = screen.getByText('+ Agregar nota')

    // Mismo contenedor padre directo (la fila de acciones)
    expect(agregarNota.parentElement).toBe(eliminar.parentElement)
    expect(agregarNota.parentElement).toBe(editar.parentElement)
  })
})
