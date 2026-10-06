import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, act } from '@testing-library/react'
import { MemoryRouter, Routes, Route, useNavigate } from 'react-router-dom'
import { AcuerdosNoVistosProvider, useAcuerdosNoVistos } from './AcuerdosNoVistosContext'
import { getAcuerdosNoVistos } from '../services/acuerdos'
import { isAuthenticated } from '../services/auth'

vi.mock('../services/acuerdos')
vi.mock('../services/auth')

// DJ-127: pequeño harness que expone el estado del contexto para poder
// aserirlo directamente, más un botón para navegar (prueba el corte de
// polling al llegar a /login y la supresión de popup al estar ya en el
// expediente del acuerdo).
function Harness() {
  const { acuerdosNoVistos, popups, marcarVistoLocal } = useAcuerdosNoVistos()
  const navigate = useNavigate()
  return (
    <div>
      <p data-testid="no-vistos-count">{acuerdosNoVistos.length}</p>
      <p data-testid="popups-count">{popups.length}</p>
      <p data-testid="popups-detalle">
        {popups.map(p => p.acuerdos.map(a => a.id).join(',')).join('|')}
      </p>
      <button onClick={() => navigate('/login')}>ir a login</button>
      <button onClick={() => marcarVistoLocal(popups[0]?.acuerdos[0]?.id)}>marcar primero visto</button>
    </div>
  )
}

function renderHarness(rutaInicial = '/expedientes') {
  return render(
    <MemoryRouter initialEntries={[rutaInicial]}>
      <AcuerdosNoVistosProvider>
        <Routes>
          <Route path="*" element={<Harness />} />
        </Routes>
      </AcuerdosNoVistosProvider>
    </MemoryRouter>
  )
}

function acuerdo(id, expedienteId = 1) {
  return { id, expedienteId, numeroExpediente: `${id}/2026`, nombreJuzgado: '1ro Civil Hermosillo', sintesis: 'x', fechaAcuerdo: '2026-10-05', confianza: 'Alta' }
}

describe('AcuerdosNoVistosContext', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    isAuthenticated.mockReturnValue(true)
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.clearAllMocks()
  })

  it('la línea base inicial no dispara ningún popup, aunque ya hubiera acuerdos pendientes', async () => {
    getAcuerdosNoVistos.mockResolvedValue([acuerdo(1), acuerdo(2)])
    renderHarness()

    await act(async () => { await Promise.resolve() })

    expect(screen.getByTestId('no-vistos-count')).toHaveTextContent('2')
    expect(screen.getByTestId('popups-count')).toHaveTextContent('0')
  })

  it('un acuerdo nuevo que llega después de la línea base sí dispara un popup', async () => {
    getAcuerdosNoVistos
      .mockResolvedValueOnce([acuerdo(1)]) // línea base
      .mockResolvedValueOnce([acuerdo(1), acuerdo(2)]) // llega el 2
    renderHarness()
    await act(async () => { await Promise.resolve() })

    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })

    expect(screen.getByTestId('popups-count')).toHaveTextContent('1')
    expect(screen.getByTestId('popups-detalle')).toHaveTextContent('2')
  })

  it('varios acuerdos que llegan en la misma ráfaga se agrupan en un solo popup', async () => {
    getAcuerdosNoVistos
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([acuerdo(1), acuerdo(2), acuerdo(3)])
    renderHarness()
    await act(async () => { await Promise.resolve() })

    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })

    expect(screen.getByTestId('popups-count')).toHaveTextContent('1')
    expect(screen.getByTestId('popups-detalle')).toHaveTextContent('1,2,3')
  })

  it('no repite el popup para el mismo acuerdo en consultas posteriores', async () => {
    getAcuerdosNoVistos
      .mockResolvedValueOnce([acuerdo(1)]) // línea base
      .mockResolvedValueOnce([acuerdo(1), acuerdo(2)]) // llega el 2 -- popup
      .mockResolvedValueOnce([acuerdo(1), acuerdo(2)]) // sin cambios -- no debe repetir
    renderHarness()
    await act(async () => { await Promise.resolve() })

    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })
    expect(screen.getByTestId('popups-count')).toHaveTextContent('1')

    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })
    expect(screen.getByTestId('popups-count')).toHaveTextContent('1') // sigue siendo el mismo, no se duplicó
  })

  it('no dispara popup si el litigante ya está viendo el expediente de ese acuerdo', async () => {
    getAcuerdosNoVistos
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([acuerdo(1, 55)])
    renderHarness('/expedientes/55')
    await act(async () => { await Promise.resolve() })

    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })

    // El badge/campana sí se entera (DJ-91 ya lo muestra ahí mismo)...
    expect(screen.getByTestId('no-vistos-count')).toHaveTextContent('1')
    // ...pero no hace falta además un popup encima.
    expect(screen.getByTestId('popups-count')).toHaveTextContent('0')
  })

  it('el polling se pausa cuando la pestaña está oculta y retoma con consulta inmediata al volver', async () => {
    getAcuerdosNoVistos.mockResolvedValue([])
    renderHarness()
    await act(async () => { await Promise.resolve() })
    expect(getAcuerdosNoVistos).toHaveBeenCalledTimes(1)

    Object.defineProperty(document, 'hidden', { configurable: true, get: () => true })
    await act(async () => { document.dispatchEvent(new Event('visibilitychange')) })

    await act(async () => { await vi.advanceTimersByTimeAsync(180_000) }) // 3 min ocultos
    expect(getAcuerdosNoVistos).toHaveBeenCalledTimes(1) // ni una consulta más mientras está oculta

    Object.defineProperty(document, 'hidden', { configurable: true, get: () => false })
    await act(async () => { document.dispatchEvent(new Event('visibilitychange')) })
    await act(async () => { await Promise.resolve() })

    expect(getAcuerdosNoVistos).toHaveBeenCalledTimes(2) // consulta inmediata al volver
  })

  it('el polling se detiene al llegar a /login (cierre de sesión)', async () => {
    getAcuerdosNoVistos.mockResolvedValue([])
    renderHarness()
    await act(async () => { await Promise.resolve() })
    expect(getAcuerdosNoVistos).toHaveBeenCalledTimes(1)

    await act(async () => { screen.getByText('ir a login').click() })

    await act(async () => { await vi.advanceTimersByTimeAsync(300_000) }) // 5 min, de sobra para varios ciclos
    expect(getAcuerdosNoVistos).toHaveBeenCalledTimes(1) // no se sumó ninguna consulta más
  })

  it('marcarVistoLocal quita el acuerdo de la lista de no-vistos y de cualquier popup que lo mostrara', async () => {
    getAcuerdosNoVistos
      .mockResolvedValueOnce([acuerdo(1)])
      .mockResolvedValueOnce([acuerdo(1), acuerdo(2)])
    renderHarness()
    await act(async () => { await Promise.resolve() })
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })

    expect(screen.getByTestId('no-vistos-count')).toHaveTextContent('2')
    expect(screen.getByTestId('popups-count')).toHaveTextContent('1')

    await act(async () => { screen.getByText('marcar primero visto').click() })

    expect(screen.getByTestId('no-vistos-count')).toHaveTextContent('1')
    expect(screen.getByTestId('popups-count')).toHaveTextContent('0')
  })
})
