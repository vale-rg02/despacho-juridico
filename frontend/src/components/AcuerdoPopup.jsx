import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { FileText, HelpCircle, X } from 'lucide-react'
import { useAcuerdosNoVistos } from '../context/AcuerdosNoVistosContext'

const DURACION_AUTOCIERRE_MS = 12000

function FilaAcuerdoAgrupado({ acuerdo, onClick }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="w-full text-left px-4 py-2.5 hover:bg-secondary/40 transition"
    >
      <div className="flex items-center gap-1.5">
        <span className="text-xs font-semibold text-foreground" style={{ fontFamily: "'DM Mono', monospace" }}>
          {acuerdo.numeroExpediente}
        </span>
        {acuerdo.confianza === 'Media' && (
          <span className="text-[9px] font-semibold text-amber-800 bg-amber-50 border border-amber-200 rounded-full px-1.5 py-0.5">
            confirma
          </span>
        )}
      </div>
      <p className="text-[12px] text-muted-foreground truncate">{acuerdo.sintesis || 'Sin síntesis disponible'}</p>
    </button>
  )
}

// Rediseño del popup (feedback tras DJ-127): encabezado de color sólido --
// navy de marca para confianza Alta, ámbar para Media -- así el nivel de
// confianza se reconoce antes de leer una sola palabra. Agrega 1-2 líneas de
// la síntesis del acuerdo y botones explícitos "Ver acuerdo"/"Cerrar" (antes
// solo existía la X). El autocierre ahora dura más (12s, antes 8s) y se
// pausa mientras el mouse está encima O el foco de teclado sigue dentro de
// la tarjeta -- se reanuda al soltar ambos.
function PopupCard({ popup, onIrAlAcuerdo, onCerrar }) {
  const [hover, setHover] = useState(false)
  const [focus, setFocus] = useState(false)
  const pausado = hover || focus
  const restanteRef = useRef(DURACION_AUTOCIERRE_MS)
  const inicioRef = useRef(null)

  useEffect(() => {
    if (pausado) return
    inicioRef.current = Date.now()
    const timeoutId = setTimeout(() => onCerrar(popup.id), restanteRef.current)
    return () => {
      clearTimeout(timeoutId)
      restanteRef.current -= Date.now() - inicioRef.current
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [popup.id, pausado])

  // onBlur se dispara también al mover el foco entre dos botones DENTRO de la
  // misma tarjeta -- sin este chequeo, el conteo se reanudaría a medio tabular.
  function handleBlur(e) {
    if (!e.currentTarget.contains(e.relatedTarget)) setFocus(false)
  }

  const esGrupo = popup.acuerdos.length > 1
  const unico = esGrupo ? null : popup.acuerdos[0]
  const esMedia = !esGrupo && unico.confianza === 'Media'
  const titulo = esGrupo ? `${popup.acuerdos.length} acuerdos nuevos` : (esMedia ? 'Posible acuerdo' : 'Nuevo acuerdo')

  return (
    <div
      role="status"
      aria-live="polite"
      className="w-[min(92vw,23rem)] bg-card rounded-lg shadow-2xl overflow-hidden"
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      onFocus={() => setFocus(true)}
      onBlur={handleBlur}
    >
      <div className={`px-4 py-3 flex items-center justify-between gap-2 ${esMedia ? 'bg-amber-50 border-b border-amber-200' : 'bg-primary'}`}>
        <span className={`flex items-center gap-2 min-w-0 ${esMedia ? 'text-amber-900' : 'text-primary-foreground'}`}>
          <span className={`w-6 h-6 rounded-full flex items-center justify-center shrink-0 ${esMedia ? 'bg-amber-200/60 border border-amber-300' : 'bg-accent/25 border border-accent/40'}`}>
            {esMedia ? (
              <HelpCircle size={12} className="text-amber-800" />
            ) : esGrupo ? (
              <span className="text-[10px] font-bold text-accent" style={{ fontFamily: "'DM Mono', monospace" }}>
                {popup.acuerdos.length}
              </span>
            ) : (
              <FileText size={12} className="text-accent" />
            )}
          </span>
          <span className="font-semibold text-[15px] truncate" style={{ fontFamily: "'Playfair Display', serif" }}>
            {titulo}
          </span>
        </span>
        <button
          type="button"
          onClick={() => onCerrar(popup.id)}
          className={`p-1 -m-1 rounded transition shrink-0 ${esMedia ? 'text-amber-700/60 hover:text-amber-900' : 'text-primary-foreground/60 hover:text-primary-foreground'}`}
          aria-label="Cerrar aviso"
        >
          <X size={15} />
        </button>
      </div>

      {esGrupo ? (
        <>
          <div className="divide-y divide-border max-h-56 overflow-y-auto">
            {popup.acuerdos.map(acuerdo => (
              <FilaAcuerdoAgrupado key={acuerdo.id} acuerdo={acuerdo} onClick={() => onIrAlAcuerdo(acuerdo)} />
            ))}
          </div>
          <div className="px-4 py-2.5 border-t border-border">
            <button
              type="button"
              onClick={() => onCerrar(popup.id)}
              className="w-full text-xs font-medium py-1.5 rounded-md text-muted-foreground hover:bg-secondary transition"
            >
              Cerrar
            </button>
          </div>
        </>
      ) : (
        <>
          <div className="px-4 py-3">
            <div className="flex items-center gap-1.5 flex-wrap">
              <span className="text-sm font-semibold text-foreground" style={{ fontFamily: "'DM Mono', monospace" }}>
                {unico.numeroExpediente}
              </span>
              {esMedia && (
                <span
                  className="flex items-center gap-1 bg-amber-50 text-amber-800 text-[10px] font-semibold rounded-full px-1.5 py-0.5"
                  title="El juzgado y número coinciden, pero el texto no nombra al demandado por su nombre — confírmalo si es tu caso"
                >
                  <HelpCircle size={9} />
                  ¿Es tu caso?
                </span>
              )}
            </div>
            {unico.nombreJuzgado && (
              <p className="text-xs text-muted-foreground mt-0.5">{unico.nombreJuzgado}</p>
            )}
            <p className="text-[13px] text-foreground leading-snug mt-1.5 line-clamp-2">
              {unico.sintesis || 'Sin síntesis disponible'}
            </p>
          </div>
          <div className="px-4 pb-3 flex items-center gap-2">
            <button
              type="button"
              onClick={() => onIrAlAcuerdo(unico)}
              className={`flex-1 text-xs font-medium py-2 rounded-md text-white transition hover:opacity-90 ${esMedia ? 'bg-amber-600' : 'bg-accent'}`}
            >
              {esMedia ? 'Ver y confirmar' : 'Ver acuerdo'}
            </button>
            <button
              type="button"
              onClick={() => onCerrar(popup.id)}
              className="text-xs font-medium py-2 px-3 rounded-md text-muted-foreground hover:bg-secondary transition"
            >
              Cerrar
            </button>
          </div>
        </>
      )}
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
