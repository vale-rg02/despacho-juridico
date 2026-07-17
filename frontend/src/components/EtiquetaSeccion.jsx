// Etiqueta de sección con un pequeño trazo dorado como acento — para encabezados
// de sección genéricos que no traen ya un ícono propio (ver DetalleExpediente/Topbar)
function EtiquetaSeccion({ children, className = '' }) {
  return (
    <h2
      className={`text-xs font-medium uppercase tracking-widest text-foreground flex items-center gap-2 ${className}`}
      style={{ fontFamily: "'DM Mono', monospace" }}
    >
      <span className="w-3 h-0.5 bg-accent shrink-0" />
      {children}
    </h2>
  )
}

export default EtiquetaSeccion
