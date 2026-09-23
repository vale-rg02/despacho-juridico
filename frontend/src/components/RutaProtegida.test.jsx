import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom'
import RutaProtegida from './RutaProtegida'

vi.mock('../services/auth', () => ({
  isAuthenticated: vi.fn(),
}))
import { isAuthenticated } from '../services/auth'

function PaginaLoginFalsa() {
  const location = useLocation()
  return <div>Login page, from: {location.state?.from ?? 'ninguno'}</div>
}

function PaginaProtegidaFalsa() {
  return <div>Contenido protegido</div>
}

// Link directo del correo a /expedientes/:id#acuerdos: sin sesión, RutaProtegida
// debe guardar path+query+hash para que Login.jsx pueda regresar al litigante
// exactamente ahí (no a /expedientes) después de autenticarse.
describe('RutaProtegida', () => {
  beforeEach(() => {
    isAuthenticated.mockReset()
  })

  it('sin sesión, redirige a /login guardando la ruta original con hash', () => {
    isAuthenticated.mockReturnValue(false)

    render(
      <MemoryRouter initialEntries={['/expedientes/159#acuerdos']}>
        <Routes>
          <Route path="/login" element={<PaginaLoginFalsa />} />
          <Route
            path="/expedientes/:id"
            element={<RutaProtegida><PaginaProtegidaFalsa /></RutaProtegida>}
          />
        </Routes>
      </MemoryRouter>
    )

    expect(screen.getByText('Login page, from: /expedientes/159#acuerdos')).toBeInTheDocument()
  })

  it('sin sesión, también preserva query params si los hay', () => {
    isAuthenticated.mockReturnValue(false)

    render(
      <MemoryRouter initialEntries={['/expedientes/159?foo=bar']}>
        <Routes>
          <Route path="/login" element={<PaginaLoginFalsa />} />
          <Route
            path="/expedientes/:id"
            element={<RutaProtegida><PaginaProtegidaFalsa /></RutaProtegida>}
          />
        </Routes>
      </MemoryRouter>
    )

    expect(screen.getByText('Login page, from: /expedientes/159?foo=bar')).toBeInTheDocument()
  })

  it('con sesión iniciada, muestra el contenido protegido sin redirigir', () => {
    isAuthenticated.mockReturnValue(true)

    render(
      <MemoryRouter initialEntries={['/expedientes/159#acuerdos']}>
        <Routes>
          <Route path="/login" element={<PaginaLoginFalsa />} />
          <Route
            path="/expedientes/:id"
            element={<RutaProtegida><PaginaProtegidaFalsa /></RutaProtegida>}
          />
        </Routes>
      </MemoryRouter>
    )

    expect(screen.getByText('Contenido protegido')).toBeInTheDocument()
  })
})
