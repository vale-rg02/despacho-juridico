import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, act, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import AcuerdoPopup from './AcuerdoPopup'
import Topbar from './Topbar'
import { AcuerdosNoVistosProvider } from '../context/AcuerdosNoVistosContext'
import { getAcuerdosNoVistos } from '../services/acuerdos'
import { getAlertas } from '../services/notificaciones'
import { isAuthenticated, getUsuario } from '../services/auth'

vi.mock('../services/acuerdos')
vi.mock('../services/notificaciones')
vi.mock('../services/auth')

const navegarMock = vi.fn()
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return { ...actual, useNavigate: () => navegarMock }
})

function acuerdo(id, expedienteId = 10, confianza = 'Alta') {
  return { id, expedienteId, numeroExpediente: `${id}/2026`, nombreJuzgado: '1ro Civil Hermosillo', sintesis: 'x', fechaAcuerdo: '2026-10-05', confianza }
}

// DJ-127: Topbar (campana) y AcuerdoPopup comparten el mismo
// AcuerdosNoVistosProvider -- esta prueba confirma que están de verdad
// sincronizados, no solo que cada uno "funciona" por separado.
function renderPagina() {
  return render(
    <MemoryRouter initialEntries={['/expedientes']}>
      <AcuerdosNoVistosProvider>
        <Topbar />
        <AcuerdoPopup />
      </AcuerdosNoVistosProvider>
    </MemoryRouter>
  )
}

describe('AcuerdoPopup + campana (sincronización)', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    isAuthenticated.mockReturnValue(true)
    getUsuario.mockReturnValue({ id: 2, nivelAcceso: 'Estandar', rol: 'Litigante' })
    getAlertas.mockResolvedValue([])
    navegarMock.mockClear()
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.clearAllMocks()
  })

  it('al llegar un acuerdo nuevo se ve el popup y el badge de la campana sube', async () => {
    getAcuerdosNoVistos
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([acuerdo(1)])
    renderPagina()
    await act(async () => { await Promise.resolve() })

    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })

    expect(screen.getByText('Nuevo acuerdo')).toBeInTheDocument()
    expect(screen.getByText('1')).toBeInTheDocument() // badge de la campana
  })

  it('clic en "Ver acuerdo" navega al expediente correcto, marca como visto y el badge de la campana baja a la vez', async () => {
    getAcuerdosNoVistos
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([acuerdo(1, 77)])
    renderPagina()
    await act(async () => { await Promise.resolve() })
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })

    fireEvent.click(screen.getByText('Ver acuerdo'))

    expect(navegarMock).toHaveBeenCalledWith('/expedientes/77#acuerdos')
    // El popup se cierra y la campana ya no cuenta ese acuerdo, en el mismo clic.
    expect(screen.queryByText('Nuevo acuerdo')).not.toBeInTheDocument()
    expect(screen.queryByText('1')).not.toBeInTheDocument()
  })

  it('clic en "Cerrar" descarta el popup sin navegar ni marcar como visto', async () => {
    getAcuerdosNoVistos
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([acuerdo(1, 77)])
    renderPagina()
    await act(async () => { await Promise.resolve() })
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })

    fireEvent.click(screen.getByText('Cerrar'))

    expect(navegarMock).not.toHaveBeenCalled()
    expect(screen.queryByText('Nuevo acuerdo')).not.toBeInTheDocument()
    // Sigue contando como no visto -- la campana no baja solo por cerrar con "Cerrar".
    expect(screen.getByText('1')).toBeInTheDocument()
  })

  it('un acuerdo de confianza Media se diferencia visualmente y usa "Ver y confirmar"', async () => {
    getAcuerdosNoVistos
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([acuerdo(1, 77, 'Media')])
    renderPagina()
    await act(async () => { await Promise.resolve() })
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })

    expect(screen.getByText('¿Es tu caso?')).toBeInTheDocument()
    expect(screen.getByText('Posible acuerdo')).toBeInTheDocument()

    fireEvent.click(screen.getByText('Ver y confirmar'))
    expect(navegarMock).toHaveBeenCalledWith('/expedientes/77#acuerdos')
  })

  it('un acuerdo de confianza Media dentro de una ráfaga agrupada usa el pill corto "confirma"', async () => {
    getAcuerdosNoVistos
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([acuerdo(1, 77, 'Alta'), acuerdo(2, 78, 'Media')])
    renderPagina()
    await act(async () => { await Promise.resolve() })
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })

    expect(screen.getByText('2 acuerdos nuevos')).toBeInTheDocument()
    expect(screen.getByText('confirma')).toBeInTheDocument()
    expect(screen.queryByText('¿Es tu caso?')).not.toBeInTheDocument()
  })

  it('no se autocierra a los 8s (duración anterior) -- dura 12s', async () => {
    getAcuerdosNoVistos
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([acuerdo(1)])
    renderPagina()
    await act(async () => { await Promise.resolve() })
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })

    await act(async () => { await vi.advanceTimersByTimeAsync(8_000) })
    expect(screen.getByText('Nuevo acuerdo')).toBeInTheDocument() // todavía visible

    await act(async () => { await vi.advanceTimersByTimeAsync(4_000) }) // completa 12s
    expect(screen.queryByText('Nuevo acuerdo')).not.toBeInTheDocument()
  })

  it('el autocierre se pausa mientras el mouse está encima y se reanuda al quitarlo', async () => {
    getAcuerdosNoVistos
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([acuerdo(1)])
    renderPagina()
    await act(async () => { await Promise.resolve() })
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })

    fireEvent.mouseEnter(screen.getByRole('status'))
    await act(async () => { await vi.advanceTimersByTimeAsync(20_000) }) // de sobra, pero está en pausa
    expect(screen.getByText('Nuevo acuerdo')).toBeInTheDocument()

    fireEvent.mouseLeave(screen.getByRole('status'))
    await act(async () => { await vi.advanceTimersByTimeAsync(12_000) })
    expect(screen.queryByText('Nuevo acuerdo')).not.toBeInTheDocument()
  })

  it('el autocierre se pausa mientras el foco de teclado sigue dentro de la tarjeta', async () => {
    getAcuerdosNoVistos
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([acuerdo(1)])
    renderPagina()
    await act(async () => { await Promise.resolve() })
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })

    const botonCerrar = screen.getByLabelText('Cerrar aviso')
    fireEvent.focus(botonCerrar)
    await act(async () => { await vi.advanceTimersByTimeAsync(20_000) })
    expect(screen.getByText('Nuevo acuerdo')).toBeInTheDocument()

    fireEvent.blur(botonCerrar, { relatedTarget: document.body })
    await act(async () => { await vi.advanceTimersByTimeAsync(12_000) })
    expect(screen.queryByText('Nuevo acuerdo')).not.toBeInTheDocument()
  })

  it('clic en un acuerdo de la campana también navega con el hash de acuerdos y queda sincronizado con el popup', async () => {
    getAcuerdosNoVistos
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([acuerdo(5, 99)])
    renderPagina()
    await act(async () => { await Promise.resolve() })
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })
    // Deja que el popup se autocierre (12s) para interactuar solo con la
    // campana aquí -- si no, "5/2026" aparecería duplicado (popup + campana).
    await act(async () => { await vi.advanceTimersByTimeAsync(12_000) })
    expect(screen.queryByText('Nuevo acuerdo')).not.toBeInTheDocument()

    fireEvent.click(screen.getByLabelText('Ver notificaciones'))
    fireEvent.click(screen.getByText('5/2026'))

    expect(navegarMock).toHaveBeenCalledWith('/expedientes/99#acuerdos')
  })
})
