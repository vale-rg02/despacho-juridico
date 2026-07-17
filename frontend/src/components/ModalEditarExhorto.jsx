import { useState } from 'react'
import { Send } from 'lucide-react'
import { editarExhorto } from '../services/exhortos'
import ModalHeader from './ModalHeader'

function ModalEditarExhorto({ expedienteId, exhorto, onGuardado, onCerrar }) {
  const [numeroExhorto, setNumeroExhorto] = useState(exhorto.numeroExhorto)
  const [ciudad, setCiudad] = useState(exhorto.ciudad)
  const [notas, setNotas] = useState(exhorto.notas ?? '')

  const [error, setError] = useState('')
  const [guardando, setGuardando] = useState(false)

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')

    if (!numeroExhorto.trim() || !ciudad.trim()) {
      setError('El número de exhorto y la ciudad son obligatorios')
      return
    }

    setGuardando(true)
    try {
      await editarExhorto(expedienteId, exhorto.id, {
        numeroExhorto: numeroExhorto.trim(),
        ciudad: ciudad.trim(),
        notas: notas.trim() || null,
      })
      onGuardado()
    } catch {
      setError('No se pudo guardar el exhorto')
    } finally {
      setGuardando(false)
    }
  }

  const labelClass = "block text-xs font-medium uppercase tracking-widest text-muted-foreground mb-1.5"
  const inputClass = "w-full bg-input-background text-foreground text-sm px-3 py-1.5 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"

  return (
    <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center">
      <form
        onSubmit={handleSubmit}
        className="bg-card border border-border rounded-lg p-6 w-full max-w-md shadow-xl space-y-4"
      >
        <ModalHeader icon={Send} tono="accent" titulo="Editar exhorto" />

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
            {error}
          </div>
        )}

        <div>
          <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Número de exhorto *</label>
          <input
            type="text"
            value={numeroExhorto}
            onChange={e => setNumeroExhorto(e.target.value)}
            className={inputClass}
          />
        </div>

        <div>
          <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Ciudad *</label>
          <input
            type="text"
            value={ciudad}
            onChange={e => setCiudad(e.target.value)}
            className={inputClass}
          />
        </div>

        <div>
          <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Notas</label>
          <input
            type="text"
            value={notas}
            onChange={e => setNotas(e.target.value)}
            placeholder="Información adicional (opcional)"
            className={inputClass}
          />
        </div>

        <div className="flex justify-end gap-2 pt-2">
          <button
            type="button"
            onClick={onCerrar}
            className="text-sm text-muted-foreground hover:text-foreground transition px-3 py-1.5"
          >
            Cancelar
          </button>
          <button
            type="submit"
            disabled={guardando}
            className="bg-accent text-accent-foreground px-4 py-1.5 rounded text-sm font-medium hover:opacity-90 transition disabled:opacity-50"
          >
            {guardando ? 'Guardando...' : 'Guardar'}
          </button>
        </div>
      </form>
    </div>
  )
}

export default ModalEditarExhorto
