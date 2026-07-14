import api from './api'

export async function getAlertas() {
  const response = await api.get('/notificaciones')
  return response.data
}

export async function marcarAtendida(expedienteId, etapaHistorialId) {
  const response = await api.patch(`/expedientes/${expedienteId}/etapas/${etapaHistorialId}/atendido`)
  return response.data
}

// usuarioId=0 o ausente = todos los usuarios (solo aplica para Socio Principal)
export async function getPanelNotificaciones(usuarioId) {
  const params = {}
  if (usuarioId !== undefined && usuarioId !== null) params.usuarioId = usuarioId
  const response = await api.get('/notificaciones/panel', { params })
  return response.data
}

export async function getUsuariosDisponibles() {
  const response = await api.get('/notificaciones/usuarios-disponibles')
  return response.data
}