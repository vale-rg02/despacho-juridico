import api from './api'

export async function getBancos() {
  const response = await api.get('/bancos')
  return response.data
}

// DJ-105
export async function crearBanco(nombre) {
  const response = await api.post('/bancos', { nombre })
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

// DJ-87 — sin sedeId, el backend regresa lista vacía (nada que elegir todavía).
// DJ-122: materia es opcional -- filtra también por Materia (el backend hace
// fallback a la sede completa si ninguna coincide, ver JuzgadosController).
export async function getJuzgados(sedeId, materia) {
  if (!sedeId) return []
  const params = { sedeId }
  if (materia) params.materia = materia
  const response = await api.get('/juzgados', { params })
  return response.data
}

export async function crearJuzgado(nombre, sedeId) {
  const response = await api.post('/juzgados', { nombre, sedeId })
  return response.data
}