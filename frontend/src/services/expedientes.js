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