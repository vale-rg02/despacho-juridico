import api from './api'

export async function getAcuerdos(expedienteId) {
  const response = await api.get(`/acuerdos/${expedienteId}`)
  return response.data
}
