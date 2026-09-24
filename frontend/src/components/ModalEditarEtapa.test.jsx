import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import ModalEditarEtapa from './ModalEditarEtapa'
import { getEtapasCatalogo, editarEtapa } from '../services/etapas'

vi.mock('../services/etapas')

// Historial de notas por etapa: editar una etapa ya no toca notas (flujo
// aparte, ver HistorialEtapas.jsx) -- el modal vuelve a ser solo fecha/tipo/límite.
describe('ModalEditarEtapa - ya no maneja notas', () => {
  const etapa = {
    id: 1,
    etapaCatalogoId: 5,
    fechaInicio: '2026-09-01T00:00:00',
    fechaLimite: null,
  }

  beforeEach(() => {
    vi.clearAllMocks()
    getEtapasCatalogo.mockResolvedValue([{ id: 5, nombre: 'Contestación', tipoJuicio: 'Hipotecario', terminoDias: null }])
    editarEtapa.mockResolvedValue({})
  })

  it('no muestra ningún campo de Notas', async () => {
    render(<ModalEditarEtapa expedienteId="1" etapa={etapa} tipoJuicio="Hipotecario" onGuardado={vi.fn()} onCerrar={vi.fn()} />)

    await screen.findByText('Editar etapa')
    expect(screen.queryByText('Notas')).not.toBeInTheDocument()
  })

  it('al guardar, no manda el campo notas en el payload', async () => {
    const onGuardado = vi.fn()
    render(<ModalEditarEtapa expedienteId="1" etapa={etapa} tipoJuicio="Hipotecario" onGuardado={onGuardado} onCerrar={vi.fn()} />)

    await screen.findByText('Editar etapa')
    fireEvent.click(screen.getByRole('button', { name: /^guardar$/i }))

    await waitFor(() => expect(editarEtapa).toHaveBeenCalled())
    const payload = editarEtapa.mock.calls[0][2]
    expect(payload).not.toHaveProperty('notas')
  })
})
