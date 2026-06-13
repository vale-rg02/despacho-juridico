// Réplica del CalculadorFechasService del backend (mismas reglas oficiales)

function primerLunesDelMes(año, mes) {
  const fecha = new Date(Date.UTC(año, mes - 1, 1))
  while (fecha.getUTCDay() !== 1) {
    fecha.setUTCDate(fecha.getUTCDate() + 1)
  }
  return fecha
}

function tercerLunesDelMes(año, mes) {
  const primerLunes = primerLunesDelMes(año, mes)
  const tercer = new Date(primerLunes)
  tercer.setUTCDate(tercer.getUTCDate() + 14)
  return tercer
}

function obtenerFestivos(año) {
  const festivos = [
    new Date(Date.UTC(año, 0, 1)),   // Año Nuevo
    new Date(Date.UTC(año, 4, 1)),   // Día del Trabajo
    new Date(Date.UTC(año, 8, 16)),  // Independencia
    new Date(Date.UTC(año, 11, 25)), // Navidad
    primerLunesDelMes(año, 2),       // Constitución
    tercerLunesDelMes(año, 3),       // Natalicio de Juárez
    tercerLunesDelMes(año, 11),      // Revolución
  ]
  return festivos.map(f => f.toISOString().slice(0, 10))
}

export function esDiaHabil(fecha) {
  const dia = fecha.getUTCDay()
  if (dia === 0 || dia === 6) return false // domingo o sábado

  const festivos = obtenerFestivos(fecha.getUTCFullYear())
  const fechaStr = fecha.toISOString().slice(0, 10)
  return !festivos.includes(fechaStr)
}

export function sumarDiasHabiles(fechaInicio, dias) {
  const fecha = new Date(fechaInicio)
  let sumados = 0
  while (sumados < dias) {
    fecha.setUTCDate(fecha.getUTCDate() + 1)
    if (esDiaHabil(fecha)) sumados++
  }
  return fecha
}

export function sumarDiasNaturales(fechaInicio, dias) {
  const fecha = new Date(fechaInicio)
  fecha.setUTCDate(fecha.getUTCDate() + dias)
  return fecha
}

// Devuelve un string "yyyy-MM-dd" o null si terminoDias es null
export function calcularFechaLimite(fechaInicioStr, terminoDias, esDiasHabiles) {
  if (terminoDias == null) return null

  const fechaInicio = new Date(fechaInicioStr + 'T00:00:00Z')
  const resultado = esDiasHabiles
    ? sumarDiasHabiles(fechaInicio, terminoDias)
    : sumarDiasNaturales(fechaInicio, terminoDias)

  return resultado.toISOString().slice(0, 10)
}