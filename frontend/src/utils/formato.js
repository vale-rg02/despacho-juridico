export function formatearFecha(fechaISO) {
  if (!fechaISO) return '—'
  const fecha = new Date(fechaISO)
  return fecha.toLocaleString('es-MX', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

// Mapeo entre el string que regresa el backend y el número que espera al enviar
export const ESTADOS = [
  { valor: 0, etiqueta: 'Abierto' },
  { valor: 1, etiqueta: 'Cerrado' },
  { valor: 2, etiqueta: 'Pausado' },
]

export const PRIORIDADES = [
  { valor: 0, etiqueta: 'Normal' },
  { valor: 1, etiqueta: 'Prioritario' },
  { valor: 2, etiqueta: 'Urgente' },
]

export function estadoANumero(texto) {
  return ESTADOS.find(e => e.etiqueta === texto)?.valor ?? 0
}

export function prioridadANumero(texto) {
  return PRIORIDADES.find(p => p.etiqueta === texto)?.valor ?? 0
}