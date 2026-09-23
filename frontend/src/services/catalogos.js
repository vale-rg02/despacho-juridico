import api from './api'

export async function getBancos() {
  const response = await api.get('/bancos')
  return response.data
}

export async function getUsuarios() {
  const response = await api.get('/usuarios?excluirSoporte=true')
  return response.data
}

// DJ-112
export async function getSedes() {
  const response = await api.get('/sedes')
  return response.data
}

export async function crearSede(nombre) {
  const response = await api.post('/sedes', { nombre })
  return response.data
}

// DJ-87 — sin sedeId, el backend regresa lista vacía (nada que elegir todavía)
export async function getJuzgados(sedeId) {
  if (!sedeId) return []
  const response = await api.get(`/juzgados?sedeId=${sedeId}`)
  return response.data
}

export async function crearJuzgado(nombre, sedeId) {
  const response = await api.post('/juzgados', { nombre, sedeId })
  return response.data
}