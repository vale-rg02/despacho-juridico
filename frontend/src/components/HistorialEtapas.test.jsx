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
})
