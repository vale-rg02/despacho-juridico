import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import Login from './Login'

vi.mock('../services/auth', () => ({
  login: vi.fn(),
}))
import { login } from '../services/auth'

function PaginaDestino({ label }) {
  return <div>{label}</div>
}

function completarYEnviarFormulario(container) {
  const emailInput = container.querySelector('input[type="email"]')
  const passwordInput = container.querySelector('input[type="password"]')
  fireEvent.change(emailInput, { target: { value: 'litigante@despacho.com' } })
  fireEvent.change(passwordInput, { target: { value: 'clave123' } })
  fireEvent.click(screen.getByRole('button', { name: /iniciar sesión/i }))
}

// Link directo del correo a /expedientes/:id#acuerdos: tras login exitoso, si
// RutaProtegida.jsx guardó un "from" (litigante llegó desde una ruta protegida
// sin sesión), Login.jsx debe regresarlo ahí -- no siempre a /expedientes.
describe('Login', () => {
  beforeEach(() => {
    login.mockReset()
  })

  it('con "from" guardado en el state, navega ahí tras login exitoso (no a /expedientes)', async () => {
    login.mockResolvedValue({ nombre: 'Mario Acedo' })

    const { container } = render(
      <MemoryRouter initialEntries={[{ pathname: '/login', state: { from: '/expedientes/159#acuerdos' } }]}>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/expedientes/:id" element={<PaginaDestino label="Detalle expediente" />} />
          <Route path="/expedientes" element={<PaginaDestino label="Lista expedientes" />} />
        </Routes>
      </MemoryRouter>
    )

    completarYEnviarFormulario(container)

    await waitFor(() => {
      expect(screen.getByText('Detalle expediente')).toBeInTheDocument()
    })
  })

  it('sin "from" (entrada directa a /login), navega a /expedientes -- sin regresión', async () => {
    login.mockResolvedValue({ nombre: 'Mario Acedo' })

    const { container } = render(
      <MemoryRouter initialEntries={['/login']}>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/expedientes" element={<PaginaDestino label="Lista expedientes" />} />
        </Routes>
      </MemoryRouter>
    )

    completarYEnviarFormulario(container)

    await waitFor(() => {
      expect(screen.getByText('Lista expedientes')).toBeInTheDocument()
    })
  })
})
