import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import NuevoExpediente from './NuevoExpediente'
import { createExpediente } from '../services/expedientes'
import { getBancos, getUsuarios, getSedes, getJuzgados, crearBanco } from '../services/catalogos'
import { getUsuario } from '../services/auth'

vi.mock('../services/expedientes')
vi.mock('../services/catalogos')
vi.mock('../services/auth')
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return { ...actual, useNavigate: () => vi.fn() }
})

// DJ-105: el campo Banco pasó de <select> a ComboboxCatalogo (mismo patrón ya
// probado de Sede/Juzgado) -- lo que importa cubrir aquí es lo NUEVO: que
// "+ Agregar banco nuevo" solo aparece para admin, que Banco sigue siendo
// opcional (no bloquea guardar sin seleccionar uno), y que al guardar se
// resuelve el bancoId correcto a partir del nombre elegido. El filtrado/
// selección del combobox en sí ya está cubierto en ComboboxCatalogo.test.jsx.
describe('NuevoExpediente - campo Banco (DJ-105)', () => {
  const bancosDisponibles = [
    { id: 1, nombre: 'BBVA México' },
    { id: 2, nombre: 'HSBC' },
  ]

  beforeEach(() => {
    vi.clearAllMocks()
    getBancos.mockResolvedValue(bancosDisponibles)
    getUsuarios.mockResolvedValue([])
    getSedes.mockResolvedValue([])
    getJuzgados.mockResolvedValue([])
    createExpediente.mockResolvedValue({ id: 42 })
  })

  function renderPagina() {
    return render(
      <MemoryRouter>
        <NuevoExpediente />
      </MemoryRouter>
    )
  }

  function llenarCamposObligatorios() {
    fireEvent.change(screen.getByPlaceholderText('Ej. 673/2019'), { target: { value: '123/2026' } })
    fireEvent.change(screen.getByPlaceholderText('Nombre completo'), { target: { value: 'Juan Pérez' } })
  }

  it('admin: ve "+ Agregar banco nuevo" al enfocar el combobox de Banco', async () => {
    getUsuario.mockReturnValue({ id: 1, nivelAcceso: 'Administrativo' })
    renderPagina()

    const inputBanco = await screen.findByPlaceholderText(/sin banco/i)
    fireEvent.focus(inputBanco)

    expect(screen.getByText('+ Agregar banco nuevo')).toBeInTheDocument()
  })

  it('litigante estándar: NO ve "+ Agregar banco nuevo"', async () => {
    getUsuario.mockReturnValue({ id: 2, nivelAcceso: 'Estandar' })
    renderPagina()

    const inputBanco = await screen.findByPlaceholderText(/sin banco/i)
    fireEvent.focus(inputBanco)

    expect(screen.queryByText('+ Agregar banco nuevo')).not.toBeInTheDocument()
  })

  it('guardar sin elegir Banco no falla -- sigue siendo opcional', async () => {
    getUsuario.mockReturnValue({ id: 2, nivelAcceso: 'Estandar' })
    renderPagina()
    await screen.findByPlaceholderText(/sin banco/i)

    llenarCamposObligatorios()
    fireEvent.click(screen.getByRole('button', { name: /guardar expediente/i }))

    await waitFor(() => expect(createExpediente).toHaveBeenCalledWith(
      expect.objectContaining({ bancoId: null })
    ))
  })

  it('al elegir un banco del catálogo y guardar, se manda el bancoId correcto', async () => {
    getUsuario.mockReturnValue({ id: 2, nivelAcceso: 'Estandar' })
    renderPagina()
    const inputBanco = await screen.findByPlaceholderText(/sin banco/i)

    llenarCamposObligatorios()
    fireEvent.focus(inputBanco)
    fireEvent.change(inputBanco, { target: { value: 'HSBC' } })
    fireEvent.click(screen.getByText('HSBC'))

    fireEvent.click(screen.getByRole('button', { name: /guardar expediente/i }))

    await waitFor(() => expect(createExpediente).toHaveBeenCalledWith(
      expect.objectContaining({ bancoId: 2 })
    ))
  })

  it('agregar un banco nuevo desde el modal lo agrega al catálogo y lo selecciona', async () => {
    getUsuario.mockReturnValue({ id: 1, nivelAcceso: 'Administrativo' })
    crearBanco.mockResolvedValue({ id: 3, nombre: 'Banorte' })
    renderPagina()

    const inputBanco = await screen.findByPlaceholderText(/sin banco/i)
    fireEvent.focus(inputBanco)
    fireEvent.click(screen.getByText('+ Agregar banco nuevo'))

    // ModalAgregarCatalogo no asocia el <label> con el <input> por atributo
    // (ver ModalAgregarCatalogo.jsx) -- es el último textbox agregado al DOM.
    const textboxes = screen.getAllByRole('textbox')
    const inputModal = textboxes[textboxes.length - 1]
    fireEvent.change(inputModal, { target: { value: 'Banorte' } })
    fireEvent.click(screen.getByRole('button', { name: /^guardar$/i }))

    await waitFor(() => expect(crearBanco).toHaveBeenCalledWith('Banorte'))
    await waitFor(() => expect(screen.getByDisplayValue('Banorte')).toBeInTheDocument())
  })
})
