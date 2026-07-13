import { useState, useEffect } from 'react'
import { getEtapasCatalogo, editarEtapa } from '../services/etapas'
import { calcularFechaLimite } from '../utils/diasHabiles'

function ModalEditarEtapa({ expedienteId, etapa, tipoJuicio, onGuardado, onCerrar }) {
  const [catalogo, setCatalogo] = useState([])
  const [cargandoCatalogo, setCargandoCatalogo] = useState(true)

  const [etapaCatalogoId, setEtapaCatalogoId] = useState(etapa.etapaCatalogoId ?? '')
  const [fechaInicio, setFechaInicio] = useState(etapa.fechaInicio.slice(0, 10))
  const [fechaLimite, setFechaLimite] = useState(etapa.fechaLimite ? etapa.fechaLimite.slice(0, 10) : '')
  const [notas, setNotas] = useState(etapa.notas ?? '')

  const [error, setError] = useState('')
  const [guardando, setGuardando] = useState(false)

  useEffect(() => {
    cargarCatalogo()
  }, [])

  async function cargarCatalogo() {
    try {
      const data = await getEtapasCatalogo(tipoJuicio)
      setCatalogo(data)
    } catch {
      setError('No se pudo cargar el catálogo de etapas')
    } finally {
      setCargandoCatalogo(false)
    }
  }

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')

    if (!etapaCatalogoId) {
      setError('Selecciona una etapa')
      return
    }

    setGuardando(true)
    try {
      await editarEtapa(expedienteId, etapa.id, {
        etapaCatalogoId: Number(etapaCatalogoId),
        fechaInicio,
        fechaLimite: fechaLimite || null,
        notas: notas || null,
      })
      onGuardado()
    } catch {
      setError('No se pudo guardar la etapa')
    } finally {
      setGuardando(false)
    }
  }

  const etapaSeleccionada = catalogo.find(e => e.id === Number(etapaCatalogoId))

  const labelClass = "block text-xs font-medium uppercase tracking-widest text-muted-foreground mb-1.5"
  const inputClass = "w-full bg-input-background text-foreground text-sm px-3 py-1.5 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"

  return (
    <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center">
      <form
        onSubmit={handleSubmit}
        className="bg-card border border-border rounded-lg p-6 w-full max-w-md shadow-xl space-y-4"
      >
        <h3 className="text-base font-medium text-foreground" style={{ fontFamily: "'Playfair Display', serif" }}>
          Editar etapa
        </h3>

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
            {error}
          </div>
        )}

        <div>
          <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Etapa *</label>
          <select
            value={etapaCatalogoId}
            onChange={e => setEtapaCatalogoId(e.target.value)}
            disabled={cargandoCatalogo}
            className={`${inputClass} cursor-pointer`}
          >
            <option value="">— Selecciona —</option>
            {catalogo.map(e => (
              <option key={e.id} value={e.id}>{e.nombre}</option>
            ))}
          </select>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Fecha de inicio *</label>
            <input
              type="date"
              value={fechaInicio}
              onChange={e => setFechaInicio(e.target.value)}
              className={inputClass}
            />
          </div>

          <div>
            <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Fecha límite</label>
            <input
              type="date"
              value={fechaLimite}
              onChange={e => setFechaLimite(e.target.value)}
              className={inputClass}
            />
          </div>
        </div>

        {etapaSeleccionada?.terminoDias != null && (
          <button
            type="button"
            onClick={() => {
              const sugerida = calcularFechaLimite(fechaInicio, etapaSeleccionada.terminoDias, etapaSeleccionada.esDiasHabiles)
              if (sugerida) setFechaLimite(sugerida)
            }}
            className="text-xs text-accent hover:underline"
          >
            Recalcular fecha límite sugerida
          </button>
        )}

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

export default ModalEditarEtapa
