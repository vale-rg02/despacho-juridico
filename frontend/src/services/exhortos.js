import api from './api'

export async function getExhortos(expedienteId) {
  const response = await api.get(`/expedientes/${expedienteId}/exhortos`)
  return response.data
}

export async function registrarExhorto(expedienteId, datos) {
  const response = await api.post(`/expedientes/${expedienteId}/exhortos`, datos)
  return response.data
}

export async function editarExhorto(expedienteId, exhortoId, datos) {
  const response = await api.patch(`/expedientes/${expedienteId}/exhortos/${exhortoId}`, datos)
  return response.data
}

export async function eliminarExhorto(expedienteId, exhortoId) {
  const response = await api.delete(`/expedientes/${expedienteId}/exhortos/${exhortoId}`)
  return response.data
}
