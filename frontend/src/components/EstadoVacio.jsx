function EstadoVacio({ icon: Icon, titulo, subtitulo }) {
  return (
    <div className="flex flex-col items-center justify-center text-center py-10 px-4">
      {Icon && (
        <div className="w-10 h-10 rounded-full bg-accent/10 text-accent flex items-center justify-center mb-3">
          <Icon size={18} />
        </div>
      )}
      <p className="text-sm font-medium text-foreground">{titulo}</p>
      {subtitulo && <p className="text-xs text-muted-foreground mt-1 max-w-xs">{subtitulo}</p>}
    </div>
  )
}

export default EstadoVacio
