import api from './api'

export async function getCitas(mes, anio) {
  const response = await api.get('/citas', { params: { mes, anio } })
  return response.data
}

export async function crearCita(datos) {
  const response = await api.post('/citas', datos)
  return response.data
}

export async function editarCita(id, datos) {
  const response = await api.put(`/citas/${id}`, datos)
  return response.data
}

export async function eliminarCita(id) {
  await api.delete(`/citas/${id}`)
}
