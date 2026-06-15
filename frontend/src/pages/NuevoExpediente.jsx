import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
import Topbar from '../components/Topbar'
import { createExpediente } from '../services/expedientes'
import { getBancos, getUsuarios } from '../services/catalogos'

const MATERIAS = ['Hipotecario', 'Mercantil', 'Arrendamiento', 'Familiar']
const TIPOS_JUICIO = ['Civil', 'Oral Mercantil', 'Familiar', 'Arrendamiento']
const PRIORIDADES = [
  { valor: 0, etiqueta: 'Normal' },
  { valor: 1, etiqueta: 'Prioritario' },
  { valor: 2, etiqueta: 'Urgente' },
]

const labelClass = "block text-xs font-medium uppercase tracking-widest text-muted-foreground mb-1.5"
const inputBase = "w-full bg-input-background text-foreground text-sm px-3 py-2 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"

function NuevoExpediente() {
  const navigate = useNavigate()

  const [bancos, setBancos] = useState([])
  const [usuarios, setUsuarios] = useState([])
  const [cargandoCatalogos, setCargandoCatalogos] = useState(true)

  const [form, setForm] = useState({
    numeroExpediente: '',
    parteDemandada: '',
    bancoId: '',
    juzgado: '',
    materia: '',
    tipoJuicio: '',
    prioridad: 0,
    usuarioAsignadoId: '',
    notas: '',
  })

  const [errores, setErrores] = useState({})
  const [errorGeneral, setErrorGeneral] = useState('')
  const [guardando, setGuardando] = useState(false)

  useEffect(() => {
    cargarCatalogos()
  }, [])

  async function cargarCatalogos() {
    try {
      const [dataBancos, dataUsuarios] = await Promise.all([
        getBancos(),
        getUsuarios(),
      ])
      setBancos(dataBancos)
      setUsuarios(dataUsuarios)
    } catch {
      setErrorGeneral('No se pudieron cargar los catálogos de bancos y usuarios')
    } finally {
      setCargandoCatalogos(false)
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
        prioridad: Number(form.prioridad),
        usuarioAsignadoId: form.usuarioAsignadoId ? Number(form.usuarioAsignadoId) : null,
        notas: form.notas || null,
      }

      const expedienteCreado = await createExpediente(payload)
      navigate(`/expedientes/${expedienteCreado.id}`)
    } catch (err) {
      if (err.response?.status === 400 && err.response.data) {
        const erroresBackend = {}
        for (const [campo, mensajes] of Object.entries(err.response.data.errors ?? err.response.data)) {
          const campoNormalizado = campo.charAt(0).toLowerCase() + campo.slice(1)
          erroresBackend[campoNormalizado] = Array.isArray(mensajes) ? mensajes[0] : mensajes
        }
        setErrores(erroresBackend)
      } else {
        setErrorGeneral('No se pudo crear el expediente. Intenta de nuevo.')
      }
    } finally {
      setGuardando(false)
    }
  }

  return (
    <div className="min-h-screen bg-background">
      <Topbar />

      <main className="max-w-screen-xl mx-auto px-6 py-8 max-w-3xl">
        <button
          onClick={() => navigate('/expedientes')}
          className="text-sm text-accent hover:underline mb-6 flex items-center gap-1.5"
        >
          <ArrowLeft size={14} />
          Volver a expedientes
        </button>

        <h1 className="text-2xl text-foreground mb-6" style={{ fontFamily: "'Playfair Display', serif" }}>
          Nuevo expediente
        </h1>

        {errorGeneral && (
          <div className="mb-4 bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
            {errorGeneral}
          </div>
        )}

        <form onSubmit={handleSubmit} className="bg-card border border-border rounded-lg p-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">

            <div>
              <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>
                Número de expediente *
              </label>
              <input
                type="text"
                name="numeroExpediente"
                value={form.numeroExpediente}
                onChange={handleChange}
                placeholder="Ej. 673/2019"
                className={`${inputBase} ${errores.numeroExpediente ? 'ring-1 ring-red-400' : ''}`}
              />
              {errores.numeroExpediente && (
                <p className="text-xs text-red-500 mt-1">{errores.numeroExpediente}</p>
              )}
            </div>

            <div>
              <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>
                Parte demandada *
              </label>
              <input
                type="text"
                name="parteDemandada"
                value={form.parteDemandada}
                onChange={handleChange}
                placeholder="Nombre completo"
                className={`${inputBase} ${errores.parteDemandada ? 'ring-1 ring-red-400' : ''}`}
              />
              {errores.parteDemandada && (
                <p className="text-xs text-red-500 mt-1">{errores.parteDemandada}</p>
              )}
            </div>

            <div>
              <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Juzgado</label>
              <input
                type="text"
                name="juzgado"
                value={form.juzgado}
                onChange={handleChange}
                placeholder="Ej. 1ro Civil"
                className={inputBase}
              />
            </div>

            <div>
              <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Banco</label>
              <select
                name="bancoId"
                value={form.bancoId}
                onChange={handleChange}
                disabled={cargandoCatalogos}
                className={`${inputBase} cursor-pointer`}
              >
                <option value="">— Sin banco —</option>
                {bancos.map(b => (
                  <option key={b.id} value={b.id}>{b.nombre}</option>
                ))}
              </select>
            </div>

            <div>
              <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Materia</label>
              <select
                name="materia"
                value={form.materia}
                onChange={handleChange}
                className={`${inputBase} cursor-pointer`}
              >
                <option value="">— Selecciona —</option>
                {MATERIAS.map(m => (
                  <option key={m} value={m}>{m}</option>
                ))}
              </select>
            </div>

            <div>
              <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Tipo de juicio</label>
              <select
                name="tipoJuicio"
                value={form.tipoJuicio}
                onChange={handleChange}
                className={`${inputBase} cursor-pointer`}
              >
                <option value="">— Selecciona —</option>
                {TIPOS_JUICIO.map(t => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
            </div>

            <div>
              <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Prioridad</label>
              <select
                name="prioridad"
                value={form.prioridad}
                onChange={handleChange}
                className={`${inputBase} cursor-pointer`}
              >
                {PRIORIDADES.map(p => (
                  <option key={p.valor} value={p.valor}>{p.etiqueta}</option>
                ))}
              </select>
            </div>

            <div>
              <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Asignado a</label>
              <select
                name="usuarioAsignadoId"
                value={form.usuarioAsignadoId}
                onChange={handleChange}
                disabled={cargandoCatalogos}
                className={`${inputBase} cursor-pointer`}
              >
                <option value="">— Sin asignar —</option>
                {usuarios.map(u => (
                  <option key={u.id} value={u.id}>{u.nombre}</option>
                ))}
              </select>
            </div>

            <div className="md:col-span-2">
              <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Notas</label>
              <textarea
                name="notas"
                value={form.notas}
                onChange={handleChange}
                rows={3}
                placeholder="Información adicional relevante..."
                className={inputBase}
              />
            </div>
          </div>

          <div className="flex justify-end gap-3 mt-6 pt-4 border-t border-border">
            <button
              type="button"
              onClick={() => navigate('/expedientes')}
              className="px-4 py-2 text-sm text-muted-foreground hover:text-foreground transition"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={guardando}
              className="bg-accent text-accent-foreground px-5 py-2 rounded text-sm font-medium hover:opacity-90 transition disabled:opacity-50"
            >
              {guardando ? 'Guardando...' : 'Guardar expediente'}
            </button>
          </div>
        </form>
      </main>
    </div>
  )
}

export default NuevoExpediente