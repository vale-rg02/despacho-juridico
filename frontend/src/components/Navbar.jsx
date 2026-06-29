import { useNavigate } from 'react-router-dom'
import { logout, getUsuario } from '../services/auth'
import PanelAlertas from './PanelAlertas'

function Navbar() {
  const navigate = useNavigate()
  const usuario = getUsuario()

  const handleLogout = () => {
    logout()
    navigate('/login')
  }

  return (
    <div className="bg-white shadow-sm px-6 py-4 flex justify-between items-center">
      <h1
        className="text-xl font-bold text-gray-800 cursor-pointer"
        onClick={() => navigate('/expedientes')}
      >
        Despacho Jurídico Acedo e Hijos
      </h1>
      <div className="flex items-center gap-4">
        <PanelAlertas />
        <span className="text-sm text-gray-500">{usuario?.nombre ?? "Usuario"}</span>
        <button
          onClick={handleLogout}
          className="text-sm text-red-500 hover:text-red-700 transition-colors"
        >
          Cerrar sesión
        </button>
      </div>
    </div>
  )
}

export default Navbar