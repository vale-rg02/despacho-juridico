// Compartido entre NuevoExpediente.jsx y EditarExpediente.jsx para que no se
// desincronicen entre sí (ya pasó antes con estas mismas listas).

export const MATERIAS = ['Civil', 'Mercantil', 'Familiar', 'Arrendamiento']

export const TIPOS_JUICIO = [
  { valor: 'Hipotecario', etiqueta: 'Hipotecario' },
  { valor: 'Jurisdiccion Voluntaria', etiqueta: 'Jurisdicción Voluntaria' },
  { valor: 'Oral Mercantil', etiqueta: 'Oral Mercantil' },
  // Pedidos por un litigante del despacho (1 de septiembre de 2026) — sin
  // catálogo de etapas propio todavía (ver docs/pendientes-reunion-despacho.md).
  // Un expediente con cualquiera de estos dos no va a poder llevar seguimiento
  // de etapas hasta que se defina su catálogo en DbSeeder.cs.
  { valor: 'Especial Mercantil', etiqueta: 'Especial Mercantil' },
  { valor: 'Ordinario Mercantil', etiqueta: 'Ordinario Mercantil' },
  { valor: 'Familiar', etiqueta: 'Familiar' },
  { valor: 'Arrendamiento', etiqueta: 'Arrendamiento' },
]

// Tipos de juicio válidos para cada materia (DJ-82): al elegir una Materia, el
// dropdown de Tipo de juicio se filtra a solo estas opciones.
const TIPOS_JUICIO_POR_MATERIA = {
  Civil: ['Hipotecario', 'Jurisdiccion Voluntaria'],
  Mercantil: ['Oral Mercantil', 'Especial Mercantil', 'Ordinario Mercantil'],
  Familiar: ['Familiar'],
  Arrendamiento: ['Arrendamiento'],
}

// Sin materia seleccionada se muestran todas las opciones (no se fuerza un orden
// de captura); con materia seleccionada, solo las que le correspondan.
export function tiposJuicioDisponibles(materia) {
  if (!materia) return TIPOS_JUICIO
  const permitidos = TIPOS_JUICIO_POR_MATERIA[materia] ?? []
  return TIPOS_JUICIO.filter(t => permitidos.includes(t.valor))
}
