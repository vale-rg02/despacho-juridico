import api from './api'

export async function getBancos() {
  const response = await api.get('/bancos')
  return response.data
}

export async function getUsuarios() {
  const response = await api.get('/usuarios?excluirSoporte=true')
  return response.data
}

export async function getJuzgados() {
  const response = await api.get('/expedientes/juzgados')
  return response.data
}