import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import Badge from '../components/Badge'
import Navbar from '../components/Navbar'
import { getExpedientes } from '../services/expedientes'

function Expedientes() {
  const navigate = useNavigate()
  const [busqueda, setBusqueda] = useState('')
  const [filtroEstado, setFiltroEstado] = useState('Todos')
  const [expedientes, setExpedientes] = useState([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    cargarExpedientes()
  }, [filtroEstado])

  // Búsqueda con debounce: espera 400ms después de dejar de escribir
  useEffect(() => {
    const timeout = setTimeout(() => {
      cargarExpedientes()
    }, 400)
    return () => clearTimeout(timeout)
  }, [busqueda])

  async function cargarExpedientes() {
    setCargando(true)
    setError('')
    try {
      const data = await getExpedientes({ estado: filtroEstado, busqueda })
      setExpedientes(data)
    } catch (err) {
      setError('No se pudieron cargar los expedientes')
    } finally {
      setCargando(false)
    }
  }

  return (
    <div className="min-h-screen bg-gray-100">
      <Navbar />

      <div className="max-w-6xl mx-auto px-6 py-8">
        {/* Encabezado */}
        <div className="flex justify-between items-center mb-6">
          <h2 className="text-2xl font-bold text-gray-800">Expedientes</h2>
          <button
            onClick={() => navigate('/expedientes/nuevo')}
            className="bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700 transition-colors text-sm font-medium"
          >
            + Nuevo expediente
          </button>
        </div>

        {/* Filtros */}
        <div className="flex gap-4 mb-4">
          <input
            type="text"
            placeholder="Buscar por número o parte..."
            value={busqueda}
            onChange={e => setBusqueda(e.target.value)}
            className="border border-gray-300 rounded-md px-3 py-2 text-sm w-72 focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <select
            value={filtroEstado}
            onChange={e => setFiltroEstado(e.target.value)}
            className="border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="Todos">Todos los estados</option>
            <option value="Abierto">Abierto</option>
            <option value="Cerrado">Cerrado</option>
            <option value="Pausado">Pausado</option>
          </select>
        </div>

        {/* Error */}
        {error && (
          <div className="mb-4 bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
            {error}
          </div>
        )}

        {/* Tabla */}
        <div className="bg-white rounded-lg shadow-sm overflow-hidden">
          <table className="w-full">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="text-left px-6 py-3 text-xs font-medium text-gray-500 uppercase">Número</th>
                <th className="text-left px-6 py-3 text-xs font-medium text-gray-500 uppercase">Parte demandada</th>
                <th className="text-left px-6 py-3 text-xs font-medium text-gray-500 uppercase">Juzgado</th>
                <th className="text-left px-6 py-3 text-xs font-medium text-gray-500 uppercase">Materia</th>
                <th className="text-left px-6 py-3 text-xs font-medium text-gray-500 uppercase">Estado</th>
                <th className="text-left px-6 py-3 text-xs font-medium text-gray-500 uppercase">Prioridad</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {cargando ? (
                <tr>
                  <td colSpan={6} className="px-6 py-8 text-center text-sm text-gray-400">
                    Cargando expedientes...
                  </td>
                </tr>
              ) : expedientes.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-6 py-8 text-center text-sm text-gray-400">
                    No se encontraron expedientes
                  </td>
                </tr>
              ) : (
                expedientes.map(exp => (
                  <tr
                    key={exp.id}
                    onClick={() => navigate(`/expedientes/${exp.id}`)}
                    className="hover:bg-gray-50 cursor-pointer"
                  >
                    <td className="px-6 py-4 text-sm font-medium text-blue-600">{exp.numeroExpediente}</td>
                    <td className="px-6 py-4 text-sm text-gray-800">{exp.parteDemandada}</td>
                    <td className="px-6 py-4 text-sm text-gray-600">{exp.juzgado ?? '—'}</td>
                    <td className="px-6 py-4 text-sm text-gray-600">{exp.materia ?? '—'}</td>
                    <td className="px-6 py-4"><Badge texto={exp.estado} /></td>
                    <td className="px-6 py-4"><Badge texto={exp.prioridad} /></td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Contador */}
        {!cargando && (
          <p className="text-sm text-gray-400 mt-3">
            {expedientes.length} expediente{expedientes.length !== 1 ? 's' : ''} encontrado{expedientes.length !== 1 ? 's' : ''}
          </p>
        )}
      </div>
    </div>
  )
}

export default Expedientes