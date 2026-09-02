import { useCerrarConEscape } from '../hooks/useCerrarConEscape'

// Reemplaza window.confirm() nativo por un diálogo propio: mismo look que el
// resto de los modales, cerrable con Escape o clic afuera. z-[60] para quedar
// siempre por encima de cualquier otro modal ya abierto (ej. confirmar
// "Eliminar cita" dentro de ModalCita).
function ModalConfirmacion({
  titulo,
  mensaje,
  confirmLabel = 'Confirmar',
  cancelLabel = 'Cancelar',
  peligroso = false,
  onConfirmar,
  onCancelar,
}) {
  useCerrarConEscape(onCancelar)

  return (
    <div
      className="fixed inset-0 bg-black/40 z-[60] flex items-center justify-center"
      onClick={e => { e.stopPropagation(); onCancelar() }}
    >
      <div
        className="bg-card border border-border rounded-lg p-6 w-full max-w-sm shadow-xl"
        onClick={e => e.stopPropagation()}
      >
        {titulo && (
          <h3 className="text-base font-medium text-foreground mb-2" style={{ fontFamily: "'Playfair Display', serif" }}>
            {titulo}
          </h3>
        )}
        <p className="text-sm text-foreground/80 mb-5">{mensaje}</p>
        <div className="flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancelar}
            className="text-sm text-muted-foreground hover:text-foreground transition px-3 py-1.5"
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            onClick={onConfirmar}
            className={
              peligroso
                ? 'bg-destructive text-destructive-foreground px-4 py-1.5 rounded text-sm font-medium hover:opacity-90 transition'
                : 'bg-accent text-accent-foreground px-4 py-1.5 rounded text-sm font-medium hover:opacity-90 transition'
            }
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  )
}

export default ModalConfirmacion
