import api from './api'

export async function getAlertas() {
  const response = await api.get('/notificaciones')
  return response.data
}

export async function marcarAtendida(expedienteId, etapaHistorialId) {
  const response = await api.patch(`/expedientes/${expedienteId}/etapas/${etapaHistorialId}/atendido`)
  return response.data
}