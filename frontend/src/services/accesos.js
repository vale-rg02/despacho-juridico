import api from './api'

export async function getAccesos(expedienteId) {
  const response = await api.get(`/expedientes/${expedienteId}/accesos`)
  return response.data
}

export async function agregarAcceso(expedienteId, usuarioId) {
  const response = await api.post(`/expedientes/${expedienteId}/accesos`, { usuarioId })
  return response.data
}

export async function quitarAcceso(expedienteId, accesoId) {
  const response = await api.delete(`/expedientes/${expedienteId}/accesos/${accesoId}`)
  return response.data
}
