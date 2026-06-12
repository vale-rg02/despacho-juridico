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