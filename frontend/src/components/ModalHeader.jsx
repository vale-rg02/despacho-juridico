// Encabezado de modal con chip de ícono a color + regla de acento —
// 'accent' (dorado) para modales de etapas, 'primary' (azul marino) para citas
const TONOS = {
  accent: { bg: 'bg-accent/10', text: 'text-accent', regla: 'border-accent/30' },
  primary: { bg: 'bg-primary/10', text: 'text-primary', regla: 'border-primary/30' },
}

function ModalHeader({ icon: Icon, tono = 'accent', titulo, subtitulo }) {
  const c = TONOS[tono] ?? TONOS.accent
  return (
    <div className={`flex items-center gap-3 pb-4 border-b ${c.regla}`}>
      <div className={`w-9 h-9 rounded-full ${c.bg} ${c.text} flex items-center justify-center shrink-0`}>
        <Icon size={16} />
      </div>
      <div className="min-w-0">
        <h3 className="text-base font-medium text-foreground truncate" style={{ fontFamily: "'Playfair Display', serif" }}>
          {titulo}
        </h3>
        {subtitulo && (
          <p className="text-xs text-muted-foreground mt-0.5 truncate" style={{ fontFamily: "'DM Mono', monospace" }}>
            {subtitulo}
          </p>
        )}
      </div>
    </div>
  )
}

export default ModalHeader
