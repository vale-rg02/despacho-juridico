import { useState, useEffect } from 'react'
import { Pencil } from 'lucide-react'
import { getEtapasCatalogo, editarEtapa } from '../services/etapas'
import { calcularFechaLimite } from '../utils/diasHabiles'
import ModalHeader from './ModalHeader'

// Extrae "HH:MM" directo del string ISO (sin pasar por new Date(), que convertiría
// a la zona horaria del navegador y desfasaría la hora) si trae una hora real, no medianoche
function horaDe(fechaISO) {
  if (!fechaISO) return ''
  const match = fechaISO.match(/T(\d{2}):(\d{2})/)
  if (!match) return ''
  const [, horas, minutos] = match
  if (horas === '00' && minutos === '00') return ''
  return `${horas}:${minutos}`
}

function ModalEditarEtapa({ expedienteId, etapa, tipoJuicio, onGuardado, onCerrar }) {
  const [catalogo, setCatalogo] = useState([])
  const [cargandoCatalogo, setCargandoCatalogo] = useState(true)

  const [etapaCatalogoId, setEtapaCatalogoId] = useState(etapa.etapaCatalogoId ?? '')
  const [fechaInicio, setFechaInicio] = useState(etapa.fechaInicio.slice(0, 10))
  const [horaInicio, setHoraInicio] = useState(horaDe(etapa.fechaInicio))
  const [fechaLimite, setFechaLimite] = useState(etapa.fechaLimite ? etapa.fechaLimite.slice(0, 10) : '')
  const [horaLimite, setHoraLimite] = useState(horaDe(etapa.fechaLimite))
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
        horaInicio: horaInicio || null,
        fechaLimite: fechaLimite || null,
        horaLimite: horaLimite || null,
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
  const inputBase = "bg-input-background text-foreground text-sm px-3 py-1.5 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"
  const inputClass = `w-full ${inputBase}`

  return (
    <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center">
      <form
        onSubmit={handleSubmit}
        className="bg-card border border-border rounded-lg p-6 w-full max-w-md shadow-xl space-y-4"
      >
        <ModalHeader icon={Pencil} tono="accent" titulo="Editar etapa" />

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

        <div className="space-y-4">
          <div>
            <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Fecha de inicio *</label>
            <div className="flex gap-2">
              <input
                type="date"
                value={fechaInicio}
                onChange={e => setFechaInicio(e.target.value)}
                onFocus={e => { try { e.target.showPicker() } catch { /* sin soporte, se ignora */ } }}
                min="1900-01-01"
                max="2100-12-31"
                className={`${inputBase} flex-1 min-w-0`}
              />
              <input
                type="time"
                value={horaInicio}
                onChange={e => setHoraInicio(e.target.value)}
                title="Hora (opcional)"
                className={`${inputBase} w-32 shrink-0`}
              />
            </div>
          </div>

          <div>
            <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Fecha límite</label>
            <div className="flex gap-2">
              <input
                type="date"
                value={fechaLimite}
                onChange={e => setFechaLimite(e.target.value)}
                onFocus={e => { try { e.target.showPicker() } catch { /* sin soporte, se ignora */ } }}
                min="1900-01-01"
                max="2100-12-31"
                className={`${inputBase} flex-1 min-w-0`}
              />
              <input
                type="time"
                value={horaLimite}
                onChange={e => setHoraLimite(e.target.value)}
                title="Hora (opcional)"
                className={`${inputBase} w-32 shrink-0`}
              />
            </div>
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
