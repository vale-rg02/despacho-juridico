import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { FileText, HelpCircle, X } from 'lucide-react'
import { useAcuerdosNoVistos } from '../context/AcuerdosNoVistosContext'

const DURACION_AUTOCIERRE_MS = 8000

function FilaAcuerdo({ acuerdo, onClick }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="w-full text-left px-3 py-2 rounded-md hover:bg-secondary/40 transition"
    >
      <div className="flex items-center gap-1.5">
        <span className="text-xs font-medium text-foreground truncate" style={{ fontFamily: "'DM Mono', monospace" }}>
          {acuerdo.numeroExpediente}
        </span>
        {acuerdo.confianza === 'Media' && (
          <span
            className="flex items-center gap-1 bg-amber-50 text-amber-800 text-[9px] font-semibold rounded-full px-1.5 py-0.5 shrink-0"
            title="El juzgado y número coinciden, pero el texto no nombra al demandado por su nombre — confírmalo si es tu caso"
          >
            <HelpCircle size={8} />
            ¿Es tu caso?
          </span>
        )}
      </div>
      {acuerdo.nombreJuzgado && (
        <p className="text-xs text-muted-foreground mt-0.5 truncate">{acuerdo.nombreJuzgado}</p>
      )}
    </button>
  )
}

function PopupCard({ popup, onIrAlAcuerdo, onCerrar }) {
  useEffect(() => {
    const timeoutId = setTimeout(() => onCerrar(popup.id), DURACION_AUTOCIERRE_MS)
    return () => clearTimeout(timeoutId)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [popup.id])

  const titulo = popup.acuerdos.length === 1 ? 'Nuevo acuerdo' : `${popup.acuerdos.length} acuerdos nuevos`

  return (
    <div
      role="status"
      aria-live="polite"
      className="w-80 bg-card border border-border rounded-lg shadow-xl overflow-hidden"
    >
      <div className="flex items-center justify-between gap-2 px-3 py-2 border-b border-border bg-secondary/30">
        <span className="flex items-center gap-1.5 text-xs font-medium uppercase tracking-widest text-foreground"
          style={{ fontFamily: "'DM Mono', monospace" }}>
          <FileText size={12} className="text-accent" />
          {titulo}
        </span>
        <button
          type="button"
          onClick={() => onCerrar(popup.id)}
          className="text-muted-foreground hover:text-foreground transition"
          aria-label="Cerrar aviso"
        >
          <X size={14} />
        </button>
      </div>
      <div className="py-1">
        {popup.acuerdos.map(acuerdo => (
          <FilaAcuerdo key={acuerdo.id} acuerdo={acuerdo} onClick={() => onIrAlAcuerdo(acuerdo)} />
        ))}
      </div>
    </div>
  )
}

// DJ-127: aviso activo, no intrusivo (esquina de la pantalla, no modal) de que
// llegó un acuerdo nuevo mientras el litigante tiene la sesión abierta. Se
// agrupa por ráfaga (una sola tarjeta por cada corrida del polling que trajo
// algo nuevo, nunca una tarjeta apilada por acuerdo). No roba el foco al
// aparecer -- nada de autoFocus/.focus() aquí, se deja al litigante seguir
// navegando con teclado sin interrupción.
function AcuerdoPopup() {
  const { popups, marcarVistoLocal, cerrarPopup } = useAcuerdosNoVistos()
  const navigate = useNavigate()

  function irAlAcuerdo(acuerdo) {
    marcarVistoLocal(acuerdo.id)
    navigate(`/expedientes/${acuerdo.expedienteId}#acuerdos`)
  }

  if (popups.length === 0) return null

  return (
    <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2 pointer-events-none">
      {popups.map(popup => (
        <div key={popup.id} className="pointer-events-auto">
          <PopupCard popup={popup} onIrAlAcuerdo={irAlAcuerdo} onCerrar={cerrarPopup} />
        </div>
      ))}
    </div>
  )
}

export default AcuerdoPopup
