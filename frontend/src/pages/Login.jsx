import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Scale } from 'lucide-react'
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
    <div className="min-h-screen bg-background flex items-center justify-center px-4">
      <div className="w-full max-w-sm">
        <div className="flex flex-col items-center mb-8">
          <div className="w-12 h-12 rounded-full bg-primary flex items-center justify-center mb-3">
            <Scale size={20} className="text-accent" />
          </div>
          <h1
            className="text-2xl text-foreground"
            style={{ fontFamily: "'Playfair Display', serif", fontWeight: 600 }}
          >
            Despacho Jurídico Acedo e Hijos
          </h1>
          <p className="text-xs text-muted-foreground mt-1" style={{ fontFamily: "'DM Mono', monospace" }}>
            Sistema de Gestión de Expedientes
          </p>
        </div>

        <form onSubmit={handleSubmit} className="bg-card border border-border rounded-lg shadow-sm p-6">
          {error && (
            <div className="mb-4 bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
              {error}
            </div>
          )}

          <div className="mb-4">
            <label className="block text-xs font-medium uppercase tracking-widest text-muted-foreground mb-1.5" style={{ fontFamily: "'DM Mono', monospace" }}>
              Correo electrónico
            </label>
            <input
              type="email"
              value={email}
              onChange={e => setEmail(e.target.value)}
              className="w-full bg-input-background text-foreground placeholder:text-muted-foreground text-sm px-3 py-2 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"
              placeholder="correo@despacho.com"
            />
          </div>

          <div className="mb-6">
            <label className="block text-xs font-medium uppercase tracking-widest text-muted-foreground mb-1.5" style={{ fontFamily: "'DM Mono', monospace" }}>
              Contraseña
            </label>
            <input
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              className="w-full bg-input-background text-foreground text-sm px-3 py-2 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"
            />
          </div>

          <button
            type="submit"
            disabled={cargando}
            className="w-full bg-primary text-primary-foreground py-2.5 rounded text-sm font-medium hover:opacity-90 transition disabled:opacity-50"
          >
            {cargando ? 'Ingresando...' : 'Iniciar sesión'}
          </button>
        </form>

        <p className="text-center text-xs text-muted-foreground mt-6" style={{ fontFamily: "'DM Mono', monospace" }}>
          Despacho Jurídico Acedo e Hijos · {new Date().getFullYear()}
        </p>
      </div>
    </div>
  )
}

export default Login