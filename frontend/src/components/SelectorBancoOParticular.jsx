import { useId } from 'react'
import ComboboxCatalogo from './ComboboxCatalogo'

const labelClass = "block text-xs font-medium uppercase tracking-widest text-muted-foreground mb-1.5"
const inputBase = "w-full bg-input-background text-foreground text-sm px-3 py-2 rounded transition"

// La parte actora de un expediente puede ser un banco del catálogo (caso más
// común, pestaña activa por default) o, en un "asunto particular", una
// persona que el despacho representa directamente -- sin banco institucional
// de por medio. Son mutuamente excluyentes al guardar (lo garantiza el
// backend, ver ResolverParteActora en ExpedientesController), pero cambiar de
// pestaña aquí no borra lo escrito en la otra -- solo se descarta al hacer
// submit, para no perder texto si el usuario duda antes de guardar.
function SelectorBancoOParticular({
  modo,
  onCambiarModo,
  banco,
  onCambiarBanco,
  bancosOpciones,
  onAgregarBanco,
  parteActoraParticular,
  onCambiarParteActoraParticular,
  disabled = false,
}) {
  const etiquetaId = useId()

  function tabClass(activo) {
    return `flex-1 text-xs px-3 py-1.5 rounded-md border font-medium transition ${
      activo
        ? 'border-accent bg-accent/10 text-accent-strong'
        : 'border-border text-muted-foreground hover:bg-secondary hover:text-foreground'
    }`
  }

  return (
    <div role="group" aria-labelledby={etiquetaId}>
      <span id={etiquetaId} className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Parte actora</span>
      <div className="flex gap-2 mb-2">
        <button type="button" aria-pressed={modo === 'banco'} onClick={() => onCambiarModo('banco')} className={tabClass(modo === 'banco')}>
          Banco
        </button>
        <button type="button" aria-pressed={modo === 'particular'} onClick={() => onCambiarModo('particular')} className={tabClass(modo === 'particular')}>
          Asunto particular
        </button>
      </div>

      {modo === 'banco' ? (
        <ComboboxCatalogo
          aria-label="Banco"
          value={banco}
          onChange={onCambiarBanco}
          opciones={bancosOpciones}
          placeholder="— Sin banco — o escribe para buscar..."
          disabled={disabled}
          onAgregarNuevo={onAgregarBanco}
          textoAgregarNuevo="+ Agregar banco nuevo"
        />
      ) : (
        <input
          type="text"
          aria-label="Nombre de la parte actora"
          value={parteActoraParticular}
          onChange={e => onCambiarParteActoraParticular(e.target.value)}
          placeholder="Nombre completo de la parte actora"
          disabled={disabled}
          className={inputBase}
        />
      )}
    </div>
  )
}

export default SelectorBancoOParticular
