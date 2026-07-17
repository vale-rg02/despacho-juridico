import { useState } from 'react'
import { registrarExhorto } from '../services/exhortos'

function FormularioExhorto({ expedienteId, onGuardado, onCancelar }) {
  const [numeroExhorto, setNumeroExhorto] = useState('')
  const [ciudad, setCiudad] = useState('')
  const [notas, setNotas] = useState('')

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
      await registrarExhorto(expedienteId, {
        numeroExhorto: numeroExhorto.trim(),
        ciudad: ciudad.trim(),
        notas: notas.trim() || null,
      })
      onGuardado()
    } catch {
      setError('No se pudo registrar el exhorto')
    } finally {
      setGuardando(false)
    }
  }

  const labelClass = "block text-xs font-medium uppercase tracking-widest text-muted-foreground mb-1.5"
  const inputClass = "w-full bg-input-background text-foreground text-sm px-3 py-1.5 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"

  return (
    <form onSubmit={handleSubmit} className="bg-secondary/40 border border-border rounded-lg p-4">
      {error && (
        <div className="mb-3 bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
          {error}
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-3">
        <div>
          <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Número de exhorto *</label>
          <input
            type="text"
            value={numeroExhorto}
            onChange={e => setNumeroExhorto(e.target.value)}
            placeholder="Ej. 123/2026"
            className={inputClass}
          />
        </div>
        <div>
          <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Ciudad *</label>
          <input
            type="text"
            value={ciudad}
            onChange={e => setCiudad(e.target.value)}
            placeholder="Ej. Guadalajara, Jalisco"
            className={inputClass}
          />
        </div>
      </div>

      <div className="mb-3">
        <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Notas</label>
        <input
          type="text"
          value={notas}
          onChange={e => setNotas(e.target.value)}
          placeholder="Información adicional (opcional)"
          className={inputClass}
        />
      </div>

      <div className="flex justify-end gap-2">
        <button
          type="button"
          onClick={onCancelar}
          className="px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground transition"
        >
          Cancelar
        </button>
        <button
          type="submit"
          disabled={guardando}
          className="bg-accent text-accent-foreground px-4 py-1.5 rounded text-sm font-medium hover:opacity-90 transition disabled:opacity-50"
        >
          {guardando ? 'Guardando...' : 'Registrar exhorto'}
        </button>
      </div>
    </form>
  )
}

export default FormularioExhorto
