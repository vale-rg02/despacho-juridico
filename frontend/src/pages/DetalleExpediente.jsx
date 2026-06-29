import { useParams, useNavigate } from 'react-router-dom'
import Badge from '../components/Badge'
import Navbar from '../components/Navbar'

const expedientesMock = [
  { id: 1, numero: "673/2019", parte: "Juan García López", juzgado: "1ro Civil", materia: "Hipotecario", estado: "Abierto", prioridad: "Urgente", banco: "HSBC", notas: "Cliente con antecedentes de pagos tardíos." },
  { id: 2, numero: "412/2021", parte: "BBVA México", juzgado: "1ro Oral Mercantil", materia: "Mercantil", estado: "Abierto", prioridad: "Normal", banco: "BBVA", notas: "" },
  { id: 3, numero: "891/2020", parte: "María Rodríguez", juzgado: "2do Civil", materia: "Hipotecario", estado: "Abierto", prioridad: "Prioritario", banco: "Santander", notas: "Pendiente recibir documentos." },
  { id: 4, numero: "234/2022", parte: "Banco Azteca", juzgado: "2do Oral Mercantil", materia: "Mercantil", estado: "Cerrado", prioridad: "Normal", banco: "Banco Azteca", notas: "" },
]

function DetalleExpediente() {
  const { id } = useParams()
  const navigate = useNavigate()
  const exp = expedientesMock.find(e => e.id === parseInt(id))

  if (!exp) return (
    <div className="min-h-screen bg-gray-100 flex items-center justify-center">
      <div className="text-center">
        <p className="text-gray-500 mb-4">Expediente no encontrado</p>
        <button
          onClick={() => navigate('/expedientes')}
          className="text-blue-600 hover:underline text-sm"
        >
          Volver a expedientes
        </button>
      </div>
    </div>
  )

  return (
    <div className="min-h-screen bg-gray-100">
      <Navbar usuario="Carlos López" />

      <div className="max-w-4xl mx-auto px-6 py-8">
        {/* Botón volver */}
        <button
          onClick={() => navigate('/expedientes')}
          className="text-sm text-blue-600 hover:underline mb-6 flex items-center gap-1"
        >
          ← Volver a expedientes
        </button>

        {/* Encabezado */}
        <div className="flex justify-between items-start mb-6">
          <div>
            <h2 className="text-2xl font-bold text-gray-800">{exp.numero}</h2>
            <p className="text-gray-500 mt-1">{exp.parte}</p>
          </div>
          <div className="flex gap-2">
            <Badge texto={exp.estado} />
            <Badge texto={exp.prioridad} />
          </div>
        </div>

        {/* Datos del expediente */}
        <div className="bg-white rounded-lg shadow-sm p-6 mb-6">
          <h3 className="text-sm font-semibold text-gray-700 uppercase mb-4">
            Información del expediente
          </h3>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <p className="text-xs text-gray-400">Juzgado</p>
              <p className="text-sm text-gray-800 font-medium">{exp.juzgado}</p>
            </div>
            <div>
              <p className="text-xs text-gray-400">Materia</p>
              <p className="text-sm text-gray-800 font-medium">{exp.materia}</p>
            </div>
            <div>
              <p className="text-xs text-gray-400">Banco</p>
              <p className="text-sm text-gray-800 font-medium">{exp.banco}</p>
            </div>
          </div>
          {exp.notas && (
            <div className="mt-4 pt-4 border-t border-gray-100">
              <p className="text-xs text-gray-400 mb-1">Notas</p>
              <p className="text-sm text-gray-700">{exp.notas}</p>
            </div>
          )}
        </div>

        {/* Historial de etapas — placeholder */}
        <div className="bg-white rounded-lg shadow-sm p-6">
          <h3 className="text-sm font-semibold text-gray-700 uppercase mb-4">
            Historial de etapas
          </h3>
          <p className="text-sm text-gray-400 text-center py-4">
            El historial de etapas estará disponible cuando el backend esté listo
          </p>
        </div>
      </div>
    </div>
  )
}

export default DetalleExpediente