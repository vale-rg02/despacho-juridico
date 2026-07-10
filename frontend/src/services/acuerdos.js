import api from './api'

export async function getAcuerdos(expedienteId) {
  const response = await api.get(`/acuerdos/${expedienteId}`)
  return response.data
}

export async function marcarAcuerdoVisto(acuerdoId) {
  const response = await api.patch(`/acuerdos/${acuerdoId}/visto`)
  return response.data
}

// Pendiente del lado backend: GET /api/acuerdos/no-vistos debe regresar
// un arreglo de expedienteId con al menos un acuerdo con Visto=false.
// Mientras no exista, se resuelve como set vacío para no romper la lista.
export async function getExpedientesConAcuerdosNoVistos() {
  try {
    const response = await api.get('/acuerdos/no-vistos')
    return response.data
  } catch {
    return []
  }
}
