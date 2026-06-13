import api from './api'

export async function getEtapasCatalogo(tipoJuicio) {
  const response = await api.get('/etapas-catalogo', {
    params: tipoJuicio ? { tipoJuicio } : {}
  })
  return response.data
}

export async function getHistorialEtapas(expedienteId) {
  const response = await api.get(`/expedientes/${expedienteId}/etapas`)
  return response.data
}

export async function registrarEtapa(expedienteId, datos) {
  const response = await api.post(`/expedientes/${expedienteId}/etapas`, datos)
  return response.data
}

export async function completarEtapa(expedienteId, etapaId, datos = {}) {
  const response = await api.put(`/expedientes/${expedienteId}/etapas/${etapaId}`, datos)
  return response.data
}