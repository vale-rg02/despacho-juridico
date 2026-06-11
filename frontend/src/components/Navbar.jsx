import { useNavigate } from 'react-router-dom'

function Navbar({ usuario }) {
  const navigate = useNavigate()

  return (
    <div className="bg-white shadow-sm px-6 py-4 flex justify-between items-center">
      <div className="flex items-center gap-3">
        <h1
          className="text-xl font-bold text-gray-800 cursor-pointer"
          onClick={() => navigate('/expedientes')}
        >
          Despacho Jurídico
        </h1>
      </div>
      <div className="flex items-center gap-4">
        <span className="text-sm text-gray-500">{usuario ?? "Usuario"}</span>
        <button
          onClick={() => navigate('/login')}
          className="text-sm text-red-500 hover:text-red-700 transition-colors"
        >
          Cerrar sesión
        </button>
      </div>
    </div>
  )
}

export default Navbar