import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import BotonActualizarExpedientes from './BotonActualizarExpedientes'
import { actualizarMisExpedientes, getMiEstadoActualizacion } from '../services/scraper'

vi.mock('../services/scraper')

// DJ-102: botón "Actualizar expedientes" -- cooldown de 15 min, indicador de
// progreso, y que el resto de la interfaz (representado aquí por un botón
// hermano) nunca se deshabilite mientras la actualización corre en segundo
// plano.
describe('BotonActualizarExpedientes', () => {
  beforeEach(() => {
    // shouldAdvanceTime: deja que el tiempo real siga corriendo (a paso rápido)
    // para que waitFor/findBy de Testing Library (que internamente usan
    // setTimeout para reintentar) no se cuelguen esperando un timer que nunca
    // avanza -- vi.advanceTimersByTimeAsync sigue disponible para saltos
    // controlados (el countdown).
    vi.useFakeTimers({ shouldAdvanceTime: true })
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.clearAllMocks()
  })

  it('al cargar, muestra el botón habilitado si no hay cooldown ni progreso', async () => {
    getMiEstadoActualizacion.mockResolvedValue({
      enProgreso: false, ultimaActualizacionEn: null, cooldownRestanteSegundos: 0, puedeActualizar: true,
    })

    render(<BotonActualizarExpedientes />)

    const boton = await screen.findByRole('button', { name: /actualizar expedientes/i })
    expect(boton).not.toBeDisabled()
  })

  it('cooldown activo: botón deshabilitado mostrando el tiempo restante, y cuenta regresivamente', async () => {
    getMiEstadoActualizacion.mockResolvedValue({
      enProgreso: false, ultimaActualizacionEn: null, cooldownRestanteSegundos: 125, puedeActualizar: false,
    })

    render(<BotonActualizarExpedientes />)

    const boton = await screen.findByRole('button', { name: /disponible en 2:05/i })
    expect(boton).toBeDisabled()

    await vi.advanceTimersByTimeAsync(3000)

    expect(screen.getByRole('button', { name: /disponible en 2:02/i })).toBeDisabled()
  })

  it('el countdown llega a habilitar el botón cuando el cooldown termina', async () => {
    getMiEstadoActualizacion.mockResolvedValue({
      enProgreso: false, ultimaActualizacionEn: null, cooldownRestanteSegundos: 2, puedeActualizar: false,
    })

    render(<BotonActualizarExpedientes />)
    await screen.findByRole('button', { name: /disponible en 0:02/i })

    await vi.advanceTimersByTimeAsync(2000)

    expect(screen.getByRole('button', { name: /^actualizar expedientes$/i })).not.toBeDisabled()
  })

  it('al hacer click, muestra indicador de progreso y deshabilita el botón mientras corre', async () => {
    getMiEstadoActualizacion.mockResolvedValue({
      enProgreso: false, ultimaActualizacionEn: null, cooldownRestanteSegundos: 0, puedeActualizar: true,
    })
    actualizarMisExpedientes.mockResolvedValue({ mensaje: 'Actualización iniciada' })

    render(<BotonActualizarExpedientes />)
    const boton = await screen.findByRole('button', { name: /actualizar expedientes/i })

    fireEvent.click(boton)

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /actualizando/i })).toBeDisabled()
    })
  })

  it('doble click mientras la primera petición está en curso solo dispara una llamada', async () => {
    getMiEstadoActualizacion.mockResolvedValue({
      enProgreso: false, ultimaActualizacionEn: null, cooldownRestanteSegundos: 0, puedeActualizar: true,
    })
    let resolverPost
    actualizarMisExpedientes.mockReturnValue(new Promise(resolve => { resolverPost = resolve }))

    render(<BotonActualizarExpedientes />)
    const boton = await screen.findByRole('button', { name: /actualizar expedientes/i })

    fireEvent.click(boton)
    fireEvent.click(boton)
    fireEvent.click(boton)

    resolverPost({ mensaje: 'Actualización iniciada' })
    await waitFor(() => expect(actualizarMisExpedientes).toHaveBeenCalledTimes(1))
  })

  it('el resto de la interfaz (un botón hermano) sigue interactivo mientras la actualización corre', async () => {
    getMiEstadoActualizacion.mockResolvedValue({
      enProgreso: false, ultimaActualizacionEn: null, cooldownRestanteSegundos: 0, puedeActualizar: true,
    })
    actualizarMisExpedientes.mockResolvedValue({ mensaje: 'Actualización iniciada' })
    const onClickHermano = vi.fn()

    render(
      <div>
        <BotonActualizarExpedientes />
        <button onClick={onClickHermano}>Otro control de la página</button>
      </div>
    )

    const boton = await screen.findByRole('button', { name: /actualizar expedientes/i })
    fireEvent.click(boton)
    await waitFor(() => expect(screen.getByRole('button', { name: /actualizando/i })).toBeDisabled())

    const hermano = screen.getByRole('button', { name: /otro control de la página/i })
    expect(hermano).not.toBeDisabled()
    fireEvent.click(hermano)
    expect(onClickHermano).toHaveBeenCalledTimes(1)
  })

  it('409 por cooldown: muestra el mensaje y el tiempo restante que trae el backend', async () => {
    getMiEstadoActualizacion.mockResolvedValue({
      enProgreso: false, ultimaActualizacionEn: null, cooldownRestanteSegundos: 0, puedeActualizar: true,
    })
    actualizarMisExpedientes.mockRejectedValue({
      response: { status: 409, data: { mensaje: 'Disponible de nuevo en 300 segundos.', cooldownRestanteSegundos: 300 } },
    })

    render(<BotonActualizarExpedientes />)
    const boton = await screen.findByRole('button', { name: /actualizar expedientes/i })
    fireEvent.click(boton)

    await screen.findByRole('button', { name: /disponible en 5:00/i })
  })

  it('el timestamp mostrado se refresca tras terminar una actualización manual (vía polling)', async () => {
    getMiEstadoActualizacion
      .mockResolvedValueOnce({ enProgreso: false, ultimaActualizacionEn: null, cooldownRestanteSegundos: 0, puedeActualizar: true })
      .mockResolvedValueOnce({ enProgreso: true, ultimaActualizacionEn: null, cooldownRestanteSegundos: 0, puedeActualizar: false })
      .mockResolvedValueOnce({ enProgreso: false, ultimaActualizacionEn: '2026-09-23T12:00:00Z', cooldownRestanteSegundos: 900, puedeActualizar: false })
    actualizarMisExpedientes.mockResolvedValue({ mensaje: 'Actualización iniciada' })

    render(<BotonActualizarExpedientes />)
    const boton = await screen.findByRole('button', { name: /actualizar expedientes/i })
    fireEvent.click(boton)
    await waitFor(() => expect(screen.getByRole('button', { name: /actualizando/i })).toBeInTheDocument())

    await vi.advanceTimersByTimeAsync(3000) // 1er tick del polling -- todavía enProgreso
    await vi.advanceTimersByTimeAsync(3000) // 2do tick -- ya terminó, trae el nuevo timestamp

    await waitFor(() => expect(getMiEstadoActualizacion).toHaveBeenCalledTimes(3))
    expect(screen.getByRole('button', { name: /disponible en/i })).toBeInTheDocument()
  })
})
