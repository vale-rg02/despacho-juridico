import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import Badge from '../components/Badge'
import Navbar from '../components/Navbar'

const expedientesMock = [
  { id: 1, numero: "673/2019", parte: "Juan García López", juzgado: "1ro Civil", materia: "Hipotecario", estado: "Abierto", prioridad: "Urgente" },
  { id: 2, numero: "412/2021", parte: "BBVA México", juzgado: "1ro Oral Mercantil", materia: "Mercantil", estado: "Abierto", prioridad: "Normal" },
  { id: 3, numero: "891/2020", parte: "María Rodríguez", juzgado: "2do Civil", materia: "Hipotecario", estado: "Abierto", prioridad: "Prioritario" },
  { id: 4, numero: "234/2022", parte: "Banco Azteca", juzgado: "2do Oral Mercantil", materia: "Mercantil", estado: "Cerrado", prioridad: "Normal" },
]

function Expedientes() {
  const navigate = useNavigate()
  const [busqueda, setBusqueda] = useState('')
  const [filtroEstado, setFiltroEstado] = useState('Todos')

  const expedientesFiltrados = expedientesMock.filter(exp => {
    const coincideBusqueda =
      exp.numero.toLowerCase().includes(busqueda.toLowerCase()) ||
      exp.parte.toLowerCase().includes(busqueda.toLowerCase())
    const coincideEstado = filtroEstado === 'Todos' || exp.estado === filtroEstado
    return coincideBusqueda && coincideEstado
  })

  return (
    <div className="min-h-screen bg-gray-100">
      <Navbar usuario="Carlos López" />

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
              {expedientesFiltrados.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-6 py-8 text-center text-sm text-gray-400">
                    No se encontraron expedientes
                  </td>
                </tr>
              ) : (
                expedientesFiltrados.map(exp => (
                  <tr
                    key={exp.id}
                    onClick={() => navigate(`/expedientes/${exp.id}`)}
                    className="hover:bg-gray-50 cursor-pointer"
                  >
                    <td className="px-6 py-4 text-sm font-medium text-blue-600">{exp.numero}</td>
                    <td className="px-6 py-4 text-sm text-gray-800">{exp.parte}</td>
                    <td className="px-6 py-4 text-sm text-gray-600">{exp.juzgado}</td>
                    <td className="px-6 py-4 text-sm text-gray-600">{exp.materia}</td>
                    <td className="px-6 py-4"><Badge texto={exp.estado} /></td>
                    <td className="px-6 py-4"><Badge texto={exp.prioridad} /></td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Contador */}
        <p className="text-sm text-gray-400 mt-3">
          {expedientesFiltrados.length} expediente{expedientesFiltrados.length !== 1 ? 's' : ''} encontrado{expedientesFiltrados.length !== 1 ? 's' : ''}
        </p>
      </div>
    </div>
  )
}

export default Expedientes