import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { login } from '../services/auth'

function Login() {
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [cargando, setCargando] = useState(false)

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')

    if (!email || !password) {
      setError('Completa ambos campos')
      return
    }

    setCargando(true)
    try {
      await login(email, password)
      navigate('/expedientes')
    } catch (err) {
      if (err.response?.status === 401) {
        setError('Correo o contraseña incorrectos')
      } else {
        setError('Error de conexión con el servidor')
      }
    } finally {
      setCargando(false)
    }
  }

  return (
    <div className="min-h-screen bg-gray-100 flex items-center justify-center">
      <form onSubmit={handleSubmit} className="bg-white p-8 rounded-lg shadow-md w-full max-w-md">
        <h1 className="text-2xl font-bold text-gray-800 mb-6 text-center">
          Despacho Jurídico
        </h1>

        {error && (
          <div className="mb-4 bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
            {error}
          </div>
        )}

        <div className="mb-4">
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Correo electrónico
          </label>
          <input
            type="email"
            value={email}
            onChange={e => setEmail(e.target.value)}
            className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
            placeholder="correo@despacho.com"
          />
        </div>
        <div className="mb-6">
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Contraseña
          </label>
          <input
            type="password"
            value={password}
            onChange={e => setPassword(e.target.value)}
            className="w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
        <button
          type="submit"
          disabled={cargando}
          className="w-full bg-blue-600 text-white py-2 rounded-md hover:bg-blue-700 transition-colors font-medium disabled:opacity-50"
        >
          {cargando ? 'Ingresando...' : 'Iniciar sesión'}
        </button>
      </form>
    </div>
  )
}

export default Login