import api from './api'

export async function getAcuerdos(expedienteId) {
  const response = await api.get(`/acuerdos/${expedienteId}`)
  return response.data
}

export async function marcarAcuerdoVisto(acuerdoId) {
  const response = await api.patch(`/acuerdos/${acuerdoId}/visto`)
  return response.data
}

export async function actualizarDestinoExhorto(acuerdoId, ciudadDestino) {
  const response = await api.patch(`/acuerdos/${acuerdoId}/destino`, { ciudadDestino })
  return response.data
}

export async function registrarExhortoManual(expedienteId, datos) {
  const response = await api.post(`/acuerdos/${expedienteId}/manual`, datos)
  return response.data
}

export async function eliminarAcuerdoManual(acuerdoId) {
  await api.delete(`/acuerdos/${acuerdoId}`)
}

export async function descartarAcuerdo(acuerdoId) {
  const response = await api.patch(`/acuerdos/${acuerdoId}/descartar`)
  return response.data
}

// GET /api/acuerdos/no-vistos regresa los acuerdos (no los expedientes) con
// visto=false asignados al usuario autenticado; se reduce a expedienteId único.
export async function getExpedientesConAcuerdosNoVistos() {
  try {
    const response = await api.get('/acuerdos/no-vistos')
    return [...new Set(response.data.map(a => a.expedienteId))]
  } catch {
    return []
  }
}
