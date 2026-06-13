import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import Navbar from '../components/Navbar'
import FormularioEtapa from '../components/FormularioEtapa'
import HistorialEtapas from '../components/HistorialEtapas'
import { getExpedienteById, getBitacora, cambiarEstado, cambiarPrioridad } from '../services/expedientes'
import { getHistorialEtapas, completarEtapa } from '../services/etapas'
import { getUsuario } from '../services/auth'
import { formatearFecha, ESTADOS, PRIORIDADES, estadoANumero, prioridadANumero } from '../utils/formato'

function DetalleExpediente() {
  const { id } = useParams()
  const navigate = useNavigate()
  const usuario = getUsuario()

  const [expediente, setExpediente] = useState(null)
  const [etapas, setEtapas] = useState([])
  const [bitacora, setBitacora] = useState([])
  const [mostrarFormEtapa, setMostrarFormEtapa] = useState(false)

  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    cargarDatos()
  }, [id])

  async function cargarDatos() {
    setCargando(true)
    setError('')
    try {
      const dataExpediente = await getExpedienteById(id)
      setExpediente(dataExpediente)

      const dataEtapas = await getHistorialEtapas(id)
      setEtapas(dataEtapas)

      // La bitácora solo se carga si el usuario es Socio
      if (usuario?.rol === 'Socio') {
        const dataBitacora = await getBitacora(id)
        setBitacora(dataBitacora)
      }
    } catch (err) {
      if (err.response?.status === 404) {
        setError('Expediente no encontrado')
      } else {
        setError('No se pudo cargar el expediente')
      }
    } finally {
      setCargando(false)
    }
  }

  async function handleCambiarEstado(e) {
    const nuevoEstado = Number(e.target.value)
    try {
      await cambiarEstado(id, nuevoEstado)
      await cargarDatos()
    } catch {
      setError('No se pudo cambiar el estado')
    }
  }

  async function handleCambiarPrioridad(e) {
    const nuevaPrioridad = Number(e.target.value)
    try {
      await cambiarPrioridad(id, nuevaPrioridad)
      await cargarDatos()
    } catch {
      setError('No se pudo cambiar la prioridad')
    }
  }

  async function handleCompletarEtapa(etapaId) {
    try {
      await completarEtapa(id, etapaId)
      await cargarDatos()
    } catch {
      setError('No se pudo completar la etapa')
    }
  }

  if (cargando) {
    return (
      <div className="min-h-screen bg-gray-100">
        <Navbar />
        <div className="max-w-4xl mx-auto px-6 py-8 text-center text-gray-400">
          Cargando expediente...
        </div>
      </div>
    )
  }

  if (error || !expediente) {
    return (
      <div className="min-h-screen bg-gray-100">
        <Navbar />
        <div className="max-w-4xl mx-auto px-6 py-8 text-center">
          <p className="text-gray-500 mb-4">{error || 'Expediente no encontrado'}</p>
          <button
            onClick={() => navigate('/expedientes')}
            className="text-blue-600 hover:underline text-sm"
          >
            ← Volver a expedientes
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-gray-100">
      <Navbar />

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
            <h2 className="text-2xl font-bold text-gray-800">{expediente.numeroExpediente}</h2>
            <p className="text-gray-500 mt-1">{expediente.parteDemandada}</p>
          </div>
          <button
            onClick={() => navigate(`/expedientes/${id}/editar`)}
            className="text-sm text-blue-600 hover:underline"
          >
            Editar
          </button>
        </div>

        {/* Selectores rápidos de estado y prioridad */}
        <div className="flex gap-4 mb-6">
          <div>
            <label className="block text-xs text-gray-400 mb-1">Estado</label>
            <select
              value={estadoANumero(expediente.estado)}
              onChange={handleCambiarEstado}
              className="border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
            >
              {ESTADOS.map(e => (
                <option key={e.valor} value={e.valor}>{e.etiqueta}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-400 mb-1">Prioridad</label>
            <select
              value={prioridadANumero(expediente.prioridad)}
              onChange={handleCambiarPrioridad}
              className="border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
            >
              {PRIORIDADES.map(p => (
                <option key={p.valor} value={p.valor}>{p.etiqueta}</option>
              ))}
            </select>
          </div>
        </div>

        {/* Mensaje de error (cambios de estado/prioridad/etapas) */}
        {error && (
          <div className="mb-4 bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
            {error}
          </div>
        )}

        {/* Datos del expediente */}
        <div className="bg-white rounded-lg shadow-sm p-6 mb-6">
          <h3 className="text-sm font-semibold text-gray-700 uppercase mb-4">
            Información del expediente
          </h3>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <p className="text-xs text-gray-400">Juzgado</p>
              <p className="text-sm text-gray-800 font-medium">{expediente.juzgado ?? '—'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-400">Materia</p>
              <p className="text-sm text-gray-800 font-medium">{expediente.materia ?? '—'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-400">Tipo de juicio</p>
              <p className="text-sm text-gray-800 font-medium">{expediente.tipoJuicio ?? '—'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-400">Banco</p>
              <p className="text-sm text-gray-800 font-medium">{expediente.bancoNombre ?? '—'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-400">Asignado a</p>
              <p className="text-sm text-gray-800 font-medium">{expediente.usuarioAsignadoNombre ?? '—'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-400">Última actualización</p>
              <p className="text-sm text-gray-800 font-medium">{formatearFecha(expediente.actualizadoEn)}</p>
            </div>
          </div>
          {expediente.notas && (
            <div className="mt-4 pt-4 border-t border-gray-100">
              <p className="text-xs text-gray-400 mb-1">Notas</p>
              <p className="text-sm text-gray-700">{expediente.notas}</p>
            </div>
          )}
        </div>

        {/* Historial de etapas */}
        <div className="bg-white rounded-lg shadow-sm p-6 mb-6">
          <div className="flex justify-between items-center mb-4">
            <h3 className="text-sm font-semibold text-gray-700 uppercase">
              Historial de etapas
            </h3>
            {!mostrarFormEtapa && (
              <button
                onClick={() => setMostrarFormEtapa(true)}
                className="text-sm text-blue-600 hover:underline"
              >
                + Registrar etapa
              </button>
            )}
          </div>

          {mostrarFormEtapa && (
            <FormularioEtapa
              expedienteId={id}
              tipoJuicio={expediente.tipoJuicio}
              onGuardado={() => {
                setMostrarFormEtapa(false)
                cargarDatos()
              }}
              onCancelar={() => setMostrarFormEtapa(false)}
            />
          )}

          <HistorialEtapas etapas={etapas} onCompletar={handleCompletarEtapa} />
        </div>

        {/* Bitácora — solo visible para el rol Socio */}
        {usuario?.rol === 'Socio' && (
          <div className="bg-white rounded-lg shadow-sm p-6">
            <h3 className="text-sm font-semibold text-gray-700 uppercase mb-4">
              Bitácora de cambios
            </h3>
            {bitacora.length === 0 ? (
              <p className="text-sm text-gray-400 text-center py-4">
                Sin registros todavía
              </p>
            ) : (
              <div className="space-y-3">
                {bitacora.map(item => (
                  <div key={item.id} className="border-b border-gray-100 pb-3 last:border-0 last:pb-0">
                    <div className="flex justify-between items-start mb-1">
                      <span className="text-sm font-medium text-gray-800 capitalize">
                        {item.accion.replace('_', ' ')}
                      </span>
                      <span className="text-xs text-gray-400">{formatearFecha(item.fecha)}</span>
                    </div>
                    <p className="text-sm text-gray-600">{item.detalle}</p>
                    <p className="text-xs text-gray-400 mt-1">por {item.usuarioNombre}</p>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  )
}

export default DetalleExpediente