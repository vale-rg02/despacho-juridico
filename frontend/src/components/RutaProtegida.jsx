import { Navigate, useLocation } from 'react-router-dom'
import { isAuthenticated } from '../services/auth'

function RutaProtegida({ children }) {
  const location = useLocation()

  if (!isAuthenticated()) {
    // Guarda a dónde iba el usuario (path+query+hash) para que Login.jsx
    // pueda regresarlo ahí después de autenticarse, en vez de mandarlo
    // siempre a /expedientes -- necesario para que un link de correo a
    // /expedientes/:id#acuerdos no se pierda si no hay sesión iniciada.
    const from = location.pathname + location.search + location.hash
    return <Navigate to="/login" state={{ from }} replace />
  }
  return children
}

export default RutaProtegida
