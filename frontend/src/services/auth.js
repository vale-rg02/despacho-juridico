import api from './api'

export async function login(email, password) {
  const response = await api.post('/auth/login', { email, password })
  const { token, usuario } = response.data

  localStorage.setItem('token', token)
  localStorage.setItem('usuario', JSON.stringify(usuario))

  return usuario
}

export function logout() {
  localStorage.removeItem('token')
  localStorage.removeItem('usuario')
}

export function getUsuario() {
  const data = localStorage.getItem('usuario')
  return data ? JSON.parse(data) : null
}

export function isAuthenticated() {
  return !!localStorage.getItem('token')
}