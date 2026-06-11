import { Navigate } from 'react-router-dom'
import { isAuthenticated } from '../services/auth'

function RutaProtegida({ children }) {
  if (!isAuthenticated()) {
    return <Navigate to="/login" replace />
  }
  return children
}

export default RutaProtegida