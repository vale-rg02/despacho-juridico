import { useState } from 'react'
import { Calendar } from 'lucide-react'
import { editarEtapa } from '../services/etapas'
import ModalHeader from './ModalHeader'

// Extrae "HH:MM" directo del string ISO, sin pasar por new Date() (desfasaría
// la hora según la zona horaria del navegador); '' si es medianoche o no hay fecha
function horaDe(fechaISO) {
  if (!fechaISO) return ''
  const match = fechaISO.match(/T(\d{2}):(\d{2})/)
  if (!match) return ''
  const [, horas, minutos] = match
  return horas === '00' && minutos === '00' ? '' : `${horas}:${minutos}`
}

function ModalEtapaCalendario({ modalEtapa, setModalEtapa, onGuardado, onVerExpediente }) {
  const [fecha, setFecha] = useState(modalEtapa.fechaLimite)
  const [hora, setHora] = useState(modalEtapa.horaLimite)
  const [guardando, setGuardando] = useState(false)
  const [error, setError] = useState('')

  async function handleGuardar() {
    if (!fecha) return
    setGuardando(true)
    setError('')
    try {
      // PATCH sobrescribe el registro completo (no fusiona), así que se reenvían
      // etapaCatalogoId/fechaInicio/notas sin cambios — solo fechaLimite/horaLimite son nuevos
      await editarEtapa(modalEtapa.expedienteId, modalEtapa.etapaId, {
        etapaCatalogoId: modalEtapa.etapaCatalogoId,
        fechaInicio: modalEtapa.fechaInicio.slice(0, 10),
        horaInicio: horaDe(modalEtapa.fechaInicio) || null,
        fechaLimite: fecha,
        horaLimite: hora || null,
        notas: modalEtapa.notas ?? null,
      })
      onGuardado()
      setModalEtapa(null)
    } catch {
      setError('No se pudo actualizar la fecha')
    } finally {
      setGuardando(false)
    }
  }

  const labelClass = "text-xs text-muted-foreground uppercase tracking-widest block mb-1"
  const inputClass = "w-full border border-border rounded-md px-3 py-2 text-sm bg-background focus:outline-none focus:ring-1 focus:ring-accent/50"

  return (
    <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center">
      <div className="bg-card border border-border rounded-lg p-6 w-full max-w-sm shadow-xl space-y-4">
        <ModalHeader
          icon={Calendar}
          tono="accent"
          titulo={modalEtapa.etapaNombre}
          subtitulo={`Exp. ${modalEtapa.numeroExpediente}`}
        />

        <div className="space-y-3">
          <div>
            <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Nueva fecha límite</label>
            <input
              type="date"
              value={fecha}
              onChange={e => setFecha(e.target.value)}
              min="1900-01-01"
              max="2100-12-31"
              className={inputClass}
            />
          </div>
          <div>
            <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Hora (opcional)</label>
            <input
              type="time"
              value={hora}
              onChange={e => setHora(e.target.value)}
              className={inputClass}
            />
          </div>
          {error && <p className="text-red-500 text-xs">{error}</p>}
        </div>

        <div className="flex justify-between items-center pt-2">
          <button
            onClick={() => {
              setModalEtapa(null)
              onVerExpediente(modalEtapa.expedienteId)
            }}
            className="text-xs text-accent hover:underline"
          >
            Ver expediente →
          </button>
          <div className="flex gap-2">
            <button
              onClick={() => setModalEtapa(null)}
              className="text-sm text-muted-foreground hover:text-foreground px-3 py-1.5 rounded-md hover:bg-secondary/50 transition"
            >
              Cancelar
            </button>
            <button
              onClick={handleGuardar}
              disabled={guardando || !fecha}
              className="bg-accent text-accent-foreground text-sm px-4 py-1.5 rounded-md hover:opacity-90 transition disabled:opacity-50"
            >
              {guardando ? 'Guardando...' : 'Guardar'}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}

export default ModalEtapaCalendario
