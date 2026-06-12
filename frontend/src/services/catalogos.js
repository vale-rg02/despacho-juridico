import api from './api'

export async function getBancos() {
  const response = await api.get('/bancos')
  return response.data
}

export async function getUsuarios() {
  const response = await api.get('/usuarios')
  return response.data
}