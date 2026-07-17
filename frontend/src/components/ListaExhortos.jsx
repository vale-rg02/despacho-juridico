import { Send } from 'lucide-react'
import { formatearFecha } from '../utils/formato'
import EstadoVacio from './EstadoVacio'

function ListaExhortos({ exhortos, onEditar, onEliminar }) {
  if (exhortos.length === 0) {
    return (
      <EstadoVacio
        icon={Send}
        titulo="Sin exhortos registrados"
        subtitulo="Registra el número de exhorto y la ciudad cuando se envíe uno a otro juzgado."
      />
    )
  }

  return (
    <div className="space-y-3">
      {exhortos.map(exhorto => (
        <div
          key={exhorto.id}
          className="rounded-md p-3 border border-border transition hover:shadow-md hover:-translate-y-0.5"
        >
          <div className="flex justify-between items-start mb-1.5">
            <span className="text-sm font-medium text-accent" style={{ fontFamily: "'DM Mono', monospace" }}>
              {exhorto.numeroExhorto}
            </span>
            <span className="text-xs text-muted-foreground">{exhorto.ciudad}</span>
          </div>
          {exhorto.notas && (
            <p className="text-sm text-foreground/80 mb-1">{exhorto.notas}</p>
          )}
          <p className="text-xs text-muted-foreground/70 mb-2" style={{ fontFamily: "'DM Mono', monospace" }}>
            Registrado por {exhorto.registradoPorNombre} · {formatearFecha(exhorto.creadoEn)}
          </p>
          <div className="flex flex-wrap items-center gap-3">
            <button
              onClick={() => onEditar(exhorto)}
              className="text-xs px-3 py-1 rounded-md border border-border text-muted-foreground hover:bg-secondary hover:text-foreground transition font-medium"
            >
              Editar
            </button>
            <button
              onClick={() => {
                if (window.confirm('¿Estás seguro de que deseas eliminar este exhorto? Esta acción no se puede deshacer.')) {
                  onEliminar(exhorto.id)
                }
              }}
              className="text-xs px-3 py-1 rounded-md border border-red-300 text-red-500 hover:bg-red-500 hover:text-white transition font-medium"
            >
              Eliminar
            </button>
          </div>
        </div>
      ))}
    </div>
  )
}

export default ListaExhortos
