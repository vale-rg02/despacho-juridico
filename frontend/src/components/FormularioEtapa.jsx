import { useState, useEffect } from 'react'
import { getEtapasCatalogo, registrarEtapa } from '../services/etapas'
import { calcularFechaLimite } from '../utils/diasHabiles'

function hoyISO() {
  return new Date().toISOString().slice(0, 10)
}

function FormularioEtapa({ expedienteId, tipoJuicio, onGuardado, onCancelar }) {
  const [catalogo, setCatalogo] = useState([])
  const [cargandoCatalogo, setCargandoCatalogo] = useState(true)

  const [etapaCatalogoId, setEtapaCatalogoId] = useState('')
  const [fechaInicio, setFechaInicio] = useState(hoyISO())
  const [fechaLimite, setFechaLimite] = useState('')
  const [notas, setNotas] = useState('')

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

  useEffect(() => {
    if (!etapaCatalogoId) return
    const etapa = catalogo.find(e => e.id === Number(etapaCatalogoId))
    if (!etapa) return

    const sugerida = calcularFechaLimite(fechaInicio, etapa.terminoDias, etapa.esDiasHabiles)
    setFechaLimite(sugerida ?? '')
  }, [etapaCatalogoId, fechaInicio, catalogo])

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')

    if (!etapaCatalogoId) {
      setError('Selecciona una etapa')
      return
    }

    setGuardando(true)
    try {
      await registrarEtapa(expedienteId, {
        etapaCatalogoId: Number(etapaCatalogoId),
        fechaInicio,
        fechaLimite: fechaLimite || null,
        notas: notas || null,
      })
      onGuardado()
    } catch {
      setError('No se pudo registrar la etapa')
    } finally {
      setGuardando(false)
    }
  }

  const etapaSeleccionada = catalogo.find(e => e.id === Number(etapaCatalogoId))

  const labelClass = "block text-xs font-medium uppercase tracking-widest text-muted-foreground mb-1.5"
  const inputClass = "w-full bg-input-background text-foreground text-sm px-3 py-1.5 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"

  return (
    <form onSubmit={handleSubmit} className="bg-secondary/40 border border-border rounded-lg p-4">
      {error && (
        <div className="mb-3 bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
          {error}
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-3 mb-3">
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
          <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>
            Fecha límite
            {etapaSeleccionada?.terminoDias != null && (
              <span className="text-muted-foreground/60 font-normal"> (sugerida)</span>
            )}
          </label>
          <input
            type="date"
            value={fechaLimite}
            onChange={e => setFechaLimite(e.target.value)}
            className={inputClass}
          />
          {etapaSeleccionada?.terminoDias == null && etapaCatalogoId && (
            <p className="text-xs text-muted-foreground mt-1">Sin plazo definido aún. Captúralo manualmente si lo conoces.</p>
          )}
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
          {guardando ? 'Guardando...' : 'Registrar etapa'}
        </button>
      </div>
    </form>
  )
}

export default FormularioEtapa