import { useState } from 'react'
import { Users, X } from 'lucide-react'
import EstadoVacio from './EstadoVacio'

function Colaboradores({ accesos, usuariosDisponibles, puedeGestionar, onAgregar, onQuitar }) {
  const [usuarioSeleccionado, setUsuarioSeleccionado] = useState('')
  const [error, setError] = useState('')
  const [guardando, setGuardando] = useState(false)

  const inputClass = "bg-input-background text-foreground text-sm px-3 py-1.5 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"

  async function handleAgregar() {
    if (!usuarioSeleccionado) return
    setError('')
    setGuardando(true)
    try {
      await onAgregar(Number(usuarioSeleccionado))
      setUsuarioSeleccionado('')
    } catch (err) {
      setError(err.response?.data?.mensaje ?? 'No se pudo agregar al colaborador')
    } finally {
      setGuardando(false)
    }
  }

  return (
    <div className="space-y-3">
      {error && (
        <div className="bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
          {error}
        </div>
      )}

      {puedeGestionar && (
        <div className="flex flex-wrap items-center gap-2">
          <select
            value={usuarioSeleccionado}
            onChange={e => setUsuarioSeleccionado(e.target.value)}
            className={`${inputClass} flex-1 min-w-[200px]`}
          >
            <option value="">Selecciona un usuario…</option>
            {usuariosDisponibles.map(u => (
              <option key={u.id} value={u.id}>{u.nombre}</option>
            ))}
          </select>
          <button
            onClick={handleAgregar}
            disabled={!usuarioSeleccionado || guardando}
            className="bg-accent text-accent-foreground px-4 py-1.5 rounded text-sm font-medium hover:opacity-90 transition disabled:opacity-50"
          >
            {guardando ? 'Agregando...' : 'Agregar colaborador'}
          </button>
        </div>
      )}

      {accesos.length === 0 ? (
        <EstadoVacio
          icon={Users}
          titulo="Sin colaboradores"
          subtitulo="Este expediente aún no tiene colaboradores agregados."
        />
      ) : (
        <div className="space-y-2">
          {accesos.map(acceso => (
            <div
              key={acceso.id}
              className="flex items-center justify-between rounded-md p-3 border border-border"
            >
              <div>
                <p className="text-sm font-medium text-foreground">{acceso.usuarioNombre}</p>
                <p className="text-xs text-muted-foreground">{acceso.usuarioEmail}</p>
              </div>
              {puedeGestionar && (
                <button
                  onClick={() => {
                    if (window.confirm(`¿Quitar a ${acceso.usuarioNombre} como colaborador de este expediente?`)) {
                      onQuitar(acceso.id)
                    }
                  }}
                  className="flex items-center gap-1 text-xs px-2.5 py-1 rounded-md border border-red-300 text-red-500 hover:bg-red-500 hover:text-white transition font-medium"
                >
                  <X size={11} />
                  Quitar
                </button>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

export default Colaboradores
