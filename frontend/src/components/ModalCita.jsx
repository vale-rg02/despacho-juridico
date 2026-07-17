import { useState } from 'react'
import { Calendar } from 'lucide-react'
import { crearCita, editarCita, eliminarCita } from '../services/citas'
import ModalHeader from './ModalHeader'

function fechaHoraALocal(fechaHoraISO) {
  const d = new Date(fechaHoraISO)
  const fecha = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
  const hora = `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
  return { fecha, hora }
}

function ModalCita({ modalCita, setModalCita, expedientesActivos, onGuardado }) {
  const esEdicion = modalCita.modo === 'editar'
  const prellenado = esEdicion
    ? fechaHoraALocal(modalCita.cita.fechaHora)
    : { fecha: modalCita.fecha, hora: modalCita.hora }

  const [titulo, setTitulo] = useState(esEdicion ? modalCita.cita.titulo : '')
  const [expedienteId, setExpedienteId] = useState(esEdicion ? (modalCita.cita.expedienteId ?? '') : '')
  const [fecha, setFecha] = useState(prellenado.fecha)
  const [hora, setHora] = useState(prellenado.hora)
  const [descripcion, setDescripcion] = useState(esEdicion ? (modalCita.cita.descripcion ?? '') : '')

  const [error, setError] = useState('')
  const [guardando, setGuardando] = useState(false)

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')

    if (!titulo.trim()) {
      setError('El título es obligatorio')
      return
    }

    setGuardando(true)
    try {
      const datos = {
        titulo: titulo.trim(),
        expedienteId: expedienteId || null,
        descripcion: descripcion || null,
        fechaHora: new Date(`${fecha}T${hora}:00`).toISOString(),
      }

      if (esEdicion) {
        await editarCita(modalCita.cita.id, datos)
      } else {
        await crearCita(datos)
      }
      onGuardado()
      setModalCita(null)
    } catch {
      setError('No se pudo guardar la cita')
    } finally {
      setGuardando(false)
    }
  }

  async function handleEliminar() {
    if (!window.confirm('¿Eliminar esta cita?')) return
    setGuardando(true)
    try {
      await eliminarCita(modalCita.cita.id)
      onGuardado()
      setModalCita(null)
    } catch {
      setError('No se pudo eliminar la cita')
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
        <ModalHeader
          icon={Calendar}
          tono="primary"
          titulo={esEdicion ? 'Editar cita' : 'Nueva cita'}
        />

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
            {error}
          </div>
        )}

        <div>
          <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Título *</label>
          <input
            type="text"
            value={titulo}
            onChange={e => setTitulo(e.target.value)}
            placeholder="Ej. Llamada con cliente"
            className={inputClass}
          />
        </div>

        <div>
          <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Expediente</label>
          <select
            value={expedienteId}
            onChange={e => setExpedienteId(e.target.value)}
            className={`${inputClass} cursor-pointer`}
          >
            <option value="">Sin expediente</option>
            {expedientesActivos.map(exp => (
              <option key={exp.id} value={exp.id}>{exp.numeroExpediente} — {exp.parteDemandada}</option>
            ))}
          </select>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Fecha *</label>
            <input
              type="date"
              value={fecha}
              onChange={e => setFecha(e.target.value)}
              className={inputClass}
            />
          </div>
          <div>
            <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Hora *</label>
            <input
              type="time"
              value={hora}
              onChange={e => setHora(e.target.value)}
              className={inputClass}
            />
          </div>
        </div>

        <div>
          <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Descripción</label>
          <textarea
            value={descripcion}
            onChange={e => setDescripcion(e.target.value)}
            placeholder="Detalles adicionales (opcional)"
            rows={3}
            className={`${inputClass} resize-none`}
          />
        </div>

        <div className="flex justify-between items-center pt-2">
          {esEdicion ? (
            <button
              type="button"
              onClick={handleEliminar}
              disabled={guardando}
              className="text-sm text-red-400 hover:text-red-600 transition disabled:opacity-50"
            >
              Eliminar cita
            </button>
          ) : <span />}

          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => setModalCita(null)}
              className="text-sm text-muted-foreground hover:text-foreground transition px-3 py-1.5"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={guardando}
              className="bg-accent text-accent-foreground px-4 py-1.5 rounded text-sm font-medium hover:opacity-90 transition disabled:opacity-50"
            >
              {guardando ? 'Guardando...' : esEdicion ? 'Guardar cambios' : 'Crear cita'}
            </button>
          </div>
        </div>
      </form>
    </div>
  )
}

export default ModalCita
