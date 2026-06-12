import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import Navbar from '../components/Navbar'
import { getExpedienteById, updateExpediente } from '../services/expedientes'
import { getBancos, getUsuarios } from '../services/catalogos'

const MATERIAS = ['Hipotecario', 'Mercantil', 'Arrendamiento', 'Familiar']
const TIPOS_JUICIO = ['Civil', 'Oral Mercantil', 'Familiar', 'Arrendamiento']

function EditarExpediente() {
  const { id } = useParams()
  const navigate = useNavigate()

  const [bancos, setBancos] = useState([])
  const [usuarios, setUsuarios] = useState([])
  const [cargando, setCargando] = useState(true)

  const [form, setForm] = useState({
    numeroExpediente: '',
    parteDemandada: '',
    bancoId: '',
    juzgado: '',
    materia: '',
    tipoJuicio: '',
    usuarioAsignadoId: '',
    notas: '',
  })

  const [errores, setErrores] = useState({})
  const [errorGeneral, setErrorGeneral] = useState('')
  const [guardando, setGuardando] = useState(false)

  useEffect(() => {
    cargarDatos()
  }, [id])

  async function cargarDatos() {
    setCargando(true)
    try {
      const [expediente, dataBancos, dataUsuarios] = await Promise.all([
        getExpedienteById(id),
        getBancos(),
        getUsuarios(),
      ])

      setForm({
        numeroExpediente: expediente.numeroExpediente ?? '',
        parteDemandada: expediente.parteDemandada ?? '',
        bancoId: expediente.bancoId ?? '',
        juzgado: expediente.juzgado ?? '',
        materia: expediente.materia ?? '',
        tipoJuicio: expediente.tipoJuicio ?? '',
        usuarioAsignadoId: expediente.usuarioAsignadoId ?? '',
        notas: expediente.notas ?? '',
      })

      setBancos(dataBancos)
      setUsuarios(dataUsuarios)
    } catch (err) {
      setErrorGeneral('No se pudo cargar el expediente')
    } finally {
      setCargando(false)
    }
  }

  function handleChange(e) {
    const { name, value } = e.target
    setForm(prev => ({ ...prev, [name]: value }))
  }

  async function handleSubmit(e) {
    e.preventDefault()
    setErrores({})
    setErrorGeneral('')

    const erroresLocales = {}
    if (!form.numeroExpediente.trim()) {
      erroresLocales.numeroExpediente = 'El número de expediente es obligatorio'
    }
    if (!form.parteDemandada.trim()) {
      erroresLocales.parteDemandada = 'La parte demandada es obligatoria'
    }
    if (Object.keys(erroresLocales).length > 0) {
      setErrores(erroresLocales)
      return
    }

    setGuardando(true)
    try {
      const payload = {
        numeroExpediente: form.numeroExpediente.trim(),
        parteDemandada: form.parteDemandada.trim(),
        bancoId: form.bancoId ? Number(form.bancoId) : null,
        juzgado: form.juzgado || null,
        materia: form.materia || null,
        tipoJuicio: form.tipoJuicio || null,
        usuarioAsignadoId: form.usuarioAsignadoId ? Number(form.usuarioAsignadoId) : null,
        notas: form.notas || null,
      }

      await updateExpediente(id, payload)
      navigate(`/expedientes/${id}`)
    } catch (err) {
      if (err.response?.status === 400 && err.response.data) {
        const erroresBackend = {}
        for (const [campo, mensajes] of Object.entries(err.response.data.errors ?? err.response.data)) {
          const campoNormalizado = campo.charAt(0).toLowerCase() + campo.slice(1)
          erroresBackend[campoNormalizado] = Array.isArray(mensajes) ? mensajes[0] : mensajes
        }
        setErrores(erroresBackend)
      } else {
        setErrorGeneral('No se pudo guardar el expediente. Intenta de nuevo.')
      }
    } finally {
      setGuardando(false)
    }
  }

  if (cargando) {
    return (
      <div className="min-h-screen bg-gray-100">
        <Navbar />
        <div className="max-w-3xl mx-auto px-6 py-8 text-center text-gray-400">
          Cargando...
        </div>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-gray-100">
      <Navbar />

      <div className="max-w-3xl mx-auto px-6 py-8">
        <button
          onClick={() => navigate(`/expedientes/${id}`)}
          className="text-sm text-blue-600 hover:underline mb-6 flex items-center gap-1"
        >
          ← Volver al expediente
        </button>

        <h2 className="text-2xl font-bold text-gray-800 mb-6">Editar expediente</h2>

        {errorGeneral && (
          <div className="mb-4 bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
            {errorGeneral}
          </div>
        )}

        <form onSubmit={handleSubmit} className="bg-white rounded-lg shadow-sm p-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Número de expediente *
              </label>
              <input
                type="text"
                name="numeroExpediente"
                value={form.numeroExpediente}
                onChange={handleChange}
                className={`w-full border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 ${
                  errores.numeroExpediente ? 'border-red-400' : 'border-gray-300'
                }`}
              />
              {errores.numeroExpediente && (
                <p className="text-xs text-red-500 mt-1">{errores.numeroExpediente}</p>
              )}
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Parte demandada *
              </label>
              <input
                type="text"
                name="parteDemandada"
                value={form.parteDemandada}
                onChange={handleChange}
                className={`w-full border rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 ${
                  errores.parteDemandada ? 'border-red-400' : 'border-gray-300'
                }`}
              />
              {errores.parteDemandada && (
                <p className="text-xs text-red-500 mt-1">{errores.parteDemandada}</p>
              )}
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Juzgado</label>
              <input
                type="text"
                name="juzgado"
                value={form.juzgado}
                onChange={handleChange}
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Banco</label>
              <select
                name="bancoId"
                value={form.bancoId}
                onChange={handleChange}
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
              >
                <option value="">— Sin banco —</option>
                {bancos.map(b => (
                  <option key={b.id} value={b.id}>{b.nombre}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Materia</label>
              <select
                name="materia"
                value={form.materia}
                onChange={handleChange}
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
              >
                <option value="">— Selecciona —</option>
                {MATERIAS.map(m => (
                  <option key={m} value={m}>{m}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Tipo de juicio</label>
              <select
                name="tipoJuicio"
                value={form.tipoJuicio}
                onChange={handleChange}
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
              >
                <option value="">— Selecciona —</option>
                {TIPOS_JUICIO.map(t => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
            </div>

            <div className="md:col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">Asignado a</label>
              <select
                name="usuarioAsignadoId"
                value={form.usuarioAsignadoId}
                onChange={handleChange}
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
              >
                <option value="">— Sin asignar —</option>
                {usuarios.map(u => (
                  <option key={u.id} value={u.id}>{u.nombre}</option>
                ))}
              </select>
            </div>

            <div className="md:col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">Notas</label>
              <textarea
                name="notas"
                value={form.notas}
                onChange={handleChange}
                rows={3}
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
          </div>

          <div className="flex justify-end gap-3 mt-6 pt-4 border-t border-gray-100">
            <button
              type="button"
              onClick={() => navigate(`/expedientes/${id}`)}
              className="px-4 py-2 text-sm text-gray-600 hover:text-gray-800 transition-colors"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={guardando}
              className="bg-blue-600 text-white px-5 py-2 rounded-md hover:bg-blue-700 transition-colors text-sm font-medium disabled:opacity-50"
            >
              {guardando ? 'Guardando...' : 'Guardar cambios'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export default EditarExpediente