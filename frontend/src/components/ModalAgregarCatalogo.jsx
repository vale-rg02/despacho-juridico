import { useState } from 'react'
import { useCerrarConEscape } from '../hooks/useCerrarConEscape'

// DJ-112/DJ-87: modal simple para que un admin agregue una Sede o un Juzgado
// nuevo al catálogo -- mismo look que ModalConfirmacion.jsx.
function ModalAgregarCatalogo({ titulo, label, onGuardar, onCancelar }) {
  const [nombre, setNombre] = useState('')
  const [guardando, setGuardando] = useState(false)
  const [error, setError] = useState('')

  useCerrarConEscape(onCancelar)

  async function handleGuardar() {
    if (!nombre.trim()) {
      setError('Este campo es obligatorio')
      return
    }
    setGuardando(true)
    setError('')
    try {
      await onGuardar(nombre.trim())
    } catch (err) {
      setError(err.response?.data?.mensaje ?? 'No se pudo agregar')
      setGuardando(false)
    }
  }

  return (
    <div
      className="fixed inset-0 bg-black/40 z-[60] flex items-center justify-center"
      onClick={e => { e.stopPropagation(); onCancelar() }}
    >
      <div
        className="bg-card border border-border rounded-lg p-6 w-full max-w-sm shadow-xl"
        onClick={e => e.stopPropagation()}
      >
        <h3 className="text-base font-medium text-foreground mb-4" style={{ fontFamily: "'Playfair Display', serif" }}>
          {titulo}
        </h3>

        <label className="block text-xs font-medium uppercase tracking-widest text-muted-foreground mb-1.5" style={{ fontFamily: "'DM Mono', monospace" }}>
          {label}
        </label>
        <input
          type="text"
          value={nombre}
          onChange={e => setNombre(e.target.value)}
          autoFocus
          className="w-full bg-input-background text-foreground text-sm px-3 py-2 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"
        />
        {error && <p className="text-xs text-red-500 mt-2">{error}</p>}

        <div className="flex justify-end gap-2 mt-5">
          <button
            type="button"
            onClick={onCancelar}
            className="text-sm text-muted-foreground hover:text-foreground transition px-3 py-1.5"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={handleGuardar}
            disabled={guardando}
            className="bg-accent text-accent-foreground px-4 py-1.5 rounded text-sm font-medium hover:opacity-90 transition disabled:opacity-50"
          >
            {guardando ? 'Guardando...' : 'Guardar'}
          </button>
        </div>
      </div>
    </div>
  )
}

export default ModalAgregarCatalogo
