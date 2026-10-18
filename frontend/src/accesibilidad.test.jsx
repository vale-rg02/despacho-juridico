import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Login from './pages/Login'
import ModalCita from './components/ModalCita'
import ModalAgregarCatalogo from './components/ModalAgregarCatalogo'
import ComboboxCatalogo from './components/ComboboxCatalogo'
import SelectorBancoOParticular from './components/SelectorBancoOParticular'
import { useTituloPagina } from './hooks/useTituloPagina'

vi.mock('./services/auth', () => ({ login: vi.fn() }))
vi.mock('./services/citas', () => ({ crearCita: vi.fn(), editarCita: vi.fn(), eliminarCita: vi.fn() }))

// Revisión UX/UI (WCAG 1.3.1 / 4.1.2 / 2.4.2): cada campo debe poder
// encontrarse por su etiqueta (lo que hace un lector de pantalla, y lo que
// permite que un clic en la etiqueta enfoque el campo).
describe('etiquetas asociadas a sus campos', () => {
  it('Login: correo y contraseña se encuentran por su etiqueta', () => {
    render(<MemoryRouter><Login /></MemoryRouter>)

    expect(screen.getByLabelText('Correo electrónico')).toHaveAttribute('type', 'email')
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('type', 'password')
  })

  it('ModalCita: título, expediente (Select), fecha, hora y descripción tienen nombre accesible', () => {
    render(<ModalCita modalCita={{ fecha: '2026-10-20' }} setModalCita={() => {}} expedientesActivos={[]} onGuardado={() => {}} />)

    expect(screen.getByLabelText('Título *')).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Expediente' })).toBeInTheDocument()
    expect(screen.getByLabelText('Fecha *')).toBeInTheDocument()
    expect(screen.getByLabelText('Hora *')).toBeInTheDocument()
    expect(screen.getByLabelText('Descripción')).toBeInTheDocument()
  })

  it('ModalAgregarCatalogo: la etiqueta dinámica enfoca el campo', () => {
    render(<ModalAgregarCatalogo titulo="Agregar sede" label="Municipio" onGuardar={() => {}} onCancelar={() => {}} />)

    expect(screen.getByLabelText('Municipio')).toBeInTheDocument()
  })

  it('ComboboxCatalogo: con label, el campo se encuentra por esa etiqueta', () => {
    render(<ComboboxCatalogo label="Sede" value="" onChange={() => {}} opciones={['Hermosillo']} />)

    expect(screen.getByLabelText('Sede')).toHaveAttribute('role', 'combobox')
  })

  it('ComboboxCatalogo: sin label visible, usa aria-label', () => {
    render(<ComboboxCatalogo aria-label="Banco" value="" onChange={() => {}} opciones={['HSBC']} />)

    expect(screen.getByRole('combobox', { name: 'Banco' })).toBeInTheDocument()
  })

  it('Parte actora: es un grupo con nombre, y las pestañas anuncian cuál está activa', () => {
    render(
      <SelectorBancoOParticular
        modo="banco" onCambiarModo={() => {}} banco="" onCambiarBanco={() => {}} bancosOpciones={['HSBC']}
        onAgregarBanco={() => {}} parteActoraParticular="" onCambiarParteActoraParticular={() => {}}
      />
    )

    expect(screen.getByRole('group', { name: 'Parte actora' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Banco' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: 'Asunto particular' })).toHaveAttribute('aria-pressed', 'false')
  })
})

describe('useTituloPagina', () => {
  beforeEach(() => { document.title = 'Acedo e Hijos' })

  function Pagina({ titulo }) {
    useTituloPagina(titulo)
    return null
  }

  it('pone el título de la pantalla seguido del nombre de la app', () => {
    render(<Pagina titulo="Calendario" />)

    expect(document.title).toBe('Calendario · Acedo e Hijos')
  })

  it('se actualiza cuando cambia el título (ej. al cargar el número de expediente)', () => {
    const { rerender } = render(<Pagina titulo="Expediente" />)
    rerender(<Pagina titulo="Exp. 159/2025" />)

    expect(document.title).toBe('Exp. 159/2025 · Acedo e Hijos')
  })

  it('sin título, deja solo el nombre de la app', () => {
    render(<Pagina titulo="" />)

    expect(document.title).toBe('Acedo e Hijos')
  })
})
