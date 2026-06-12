import api from './api'

export async function getExpedientes(filtros = {}) {
  const params = {}
  if (filtros.estado && filtros.estado !== 'Todos') params.estado = filtros.estado
  if (filtros.busqueda) params.busqueda = filtros.busqueda

  const response = await api.get('/expedientes', { params })
  return response.data
}

export async function getExpedienteById(id) {
  const response = await api.get(`/expedientes/${id}`)
  return response.data
}

export async function getBitacora(id) {
  const response = await api.get(`/expedientes/${id}/bitacora`)
  return response.data
}

export async function createExpediente(datos) {
  const response = await api.post('/expedientes', datos)
  return response.data
}

export async function updateExpediente(id, datos) {
  const response = await api.put(`/expedientes/${id}`, datos)
  return response.data
}

export async function cambiarEstado(id, estado) {
  const response = await api.patch(`/expedientes/${id}/estado`, { estado })
  return response.data
}

export async function cambiarPrioridad(id, prioridad) {
  const response = await api.patch(`/expedientes/${id}/prioridad`, { prioridad })
  return response.data
}