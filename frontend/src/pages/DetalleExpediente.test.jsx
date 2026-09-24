import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import DetalleExpediente from './DetalleExpediente'
import { getExpedienteById, getBitacora, cambiarEstado, cambiarPrioridad, eliminarExpediente } from '../services/expedientes'
import { getHistorialEtapas } from '../services/etapas'
import { getAccesos } from '../services/accesos'
import { getUsuarios } from '../services/catalogos'
import { getAcuerdos, marcarAcuerdoVisto, registrarAcuerdoManual } from '../services/acuerdos'
import { getUsuario } from '../services/auth'

vi.mock('../services/expedientes')
vi.mock('../services/etapas')
vi.mock('../services/accesos')
vi.mock('../services/catalogos')
vi.mock('../services/acuerdos')
vi.mock('../services/auth')
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return { ...actual, useNavigate: () => vi.fn(), useParams: () => ({ id: '1' }) }
})

// DJ-108: el registro manual de acuerdos (antes solo exhortos) generaliza a
// cualquier acuerdo, con un selector de tipo, y queda acotado a expedientes
// propios (titular o colaborador) -- el botón se oculta para el resto.
describe('DetalleExpediente - registro manual de acuerdos (DJ-108)', () => {
  const expedienteBase = {
    id: 1,
    numeroExpediente: '673/2019',
    parteDemandada: 'Juan Pérez',
    estado: 'Abierto',
    prioridad: 'Normal',
    usuarioAsignadoId: 2,
    usuarioAsignadoNombre: 'Mario Acedo',
    esColaborador: false,
    creadoEn: '2026-01-01T00:00:00Z',
    actualizadoEn: '2026-01-01T00:00:00Z',
  }

  beforeEach(() => {
    vi.clearAllMocks()
    getHistorialEtapas.mockResolvedValue([])
    getAccesos.mockResolvedValue([])
    getUsuarios.mockResolvedValue([])
    getAcuerdos.mockResolvedValue([])
    getBitacora.mockResolvedValue([])
    marcarAcuerdoVisto.mockResolvedValue({})
    cambiarEstado.mockResolvedValue({})
    cambiarPrioridad.mockResolvedValue({})
    eliminarExpediente.mockResolvedValue({})
  })

  function renderPagina() {
    return render(
      <MemoryRouter>
        <DetalleExpediente />
      </MemoryRouter>
    )
  }

  it('titular: ve el botón "+ Registrar acuerdo manualmente"', async () => {
    getUsuario.mockReturnValue({ id: 2, nivelAcceso: 'Estandar', rol: 'Litigante' })
    getExpedienteById.mockResolvedValue(expedienteBase)
    renderPagina()

    expect(await screen.findByText('+ Registrar acuerdo manualmente')).toBeInTheDocument()
  })

  it('colaborador: ve el botón "+ Registrar acuerdo manualmente"', async () => {
    getUsuario.mockReturnValue({ id: 9, nivelAcceso: 'Estandar', rol: 'Litigante' })
    getExpedienteById.mockResolvedValue({ ...expedienteBase, esColaborador: true })
    renderPagina()

    expect(await screen.findByText('+ Registrar acuerdo manualmente')).toBeInTheDocument()
  })

  it('litigante sin relación con el expediente: NO ve el botón', async () => {
    getUsuario.mockReturnValue({ id: 9, nivelAcceso: 'Estandar', rol: 'Litigante' })
    getExpedienteById.mockResolvedValue({ ...expedienteBase, esColaborador: false })
    renderPagina()

    await screen.findAllByText('673/2019')
    expect(screen.queryByText('+ Registrar acuerdo manualmente')).not.toBeInTheDocument()
  })

  it('modo "Acuerdo normal": envía esExhorto=false y el tipoAsunto capturado', async () => {
    getUsuario.mockReturnValue({ id: 2, nivelAcceso: 'Estandar', rol: 'Litigante' })
    getExpedienteById.mockResolvedValue(expedienteBase)
    registrarAcuerdoManual.mockResolvedValue({})
    renderPagina()

    fireEvent.click(await screen.findByText('+ Registrar acuerdo manualmente'))

    // "Acuerdo normal" es el default -- no hace falta tocar el selector.
    fireEvent.change(screen.getByPlaceholderText('Ej. Contestación, Notificación...'), { target: { value: 'Contestación' } })
    fireEvent.change(screen.getByPlaceholderText('Descripción del acuerdo...'), { target: { value: 'Se contesta demanda' } })
    const fecha = document.querySelector('input[type="date"]')
    fireEvent.change(fecha, { target: { value: '2026-09-24' } })

    fireEvent.click(screen.getByRole('button', { name: /guardar acuerdo/i }))

    await waitFor(() => expect(registrarAcuerdoManual).toHaveBeenCalledWith('1', expect.objectContaining({
      esExhorto: false,
      tipoAsunto: 'Contestación',
      sintesis: 'Se contesta demanda',
      fechaAcuerdo: '2026-09-24',
    })))
  })

  it('modo "Exhorto": sigue enviando esExhorto=true con Juzgado/Ciudad destino, sin regresión', async () => {
    getUsuario.mockReturnValue({ id: 2, nivelAcceso: 'Estandar', rol: 'Litigante' })
    getExpedienteById.mockResolvedValue(expedienteBase)
    registrarAcuerdoManual.mockResolvedValue({})
    renderPagina()

    fireEvent.click(await screen.findByText('+ Registrar acuerdo manualmente'))
    fireEvent.click(screen.getByLabelText('Exhorto'))

    fireEvent.change(screen.getByPlaceholderText('Ej. 1ro Civil Hermosillo'), { target: { value: '1ro Civil Guadalajara' } })
    fireEvent.change(screen.getByPlaceholderText('Ej. Guadalajara, Jalisco'), { target: { value: 'Guadalajara, Jalisco' } })
    fireEvent.change(screen.getByPlaceholderText('Descripción del acuerdo...'), { target: { value: 'Se recibe exhorto' } })
    const fecha = document.querySelector('input[type="date"]')
    fireEvent.change(fecha, { target: { value: '2026-09-24' } })

    fireEvent.click(screen.getByRole('button', { name: /guardar exhorto/i }))

    await waitFor(() => expect(registrarAcuerdoManual).toHaveBeenCalledWith('1', expect.objectContaining({
      esExhorto: true,
      nombreJuzgado: '1ro Civil Guadalajara',
      ciudadDestino: 'Guadalajara, Jalisco',
      tipoAsunto: null,
    })))
  })
})
