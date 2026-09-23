import api from './api'

// DJ-102: dispara la actualización manual ("Actualizar expedientes") acotada a
// los expedientes propios del usuario autenticado. 202 = se encoló y corre en
// segundo plano; 409 = cooldown o ya en progreso (el caller lee
// err.response.data.cooldownRestanteSegundos / mensaje).
export async function actualizarMisExpedientes() {
  const response = await api.post('/scraper/actualizar-mis-expedientes')
  return response.data
}

export async function getMiEstadoActualizacion() {
  const response = await api.get('/scraper/mi-estado-actualizacion')
  return response.data
}
