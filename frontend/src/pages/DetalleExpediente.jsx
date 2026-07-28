import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import {
  ArrowLeft, FileText, Gavel, BookOpen, StickyNote, Clock,
  ClipboardList, User, Landmark, Pencil, Trash2, ChevronDown, Scale, Send, Users, UserPlus, X, MapPin
} from 'lucide-react'
import Topbar from '../components/Topbar'
import InfoCard from '../components/InfoCard'
import FormularioEtapa from '../components/FormularioEtapa'
import HistorialEtapas from '../components/HistorialEtapas'
import ModalEditarEtapa from '../components/ModalEditarEtapa'
import { getHistorialEtapas, completarEtapa, revertirEtapa, eliminarEtapa } from '../services/etapas'
import { getUsuario } from '../services/auth'
import { getAcuerdos, marcarAcuerdoVisto, actualizarDestinoExhorto, registrarExhortoManual, eliminarAcuerdoManual } from '../services/acuerdos'
import { getAccesos, agregarAcceso, quitarAcceso } from '../services/accesos'
import { getUsuarios } from '../services/catalogos'
import { formatearFecha, formatearFechaCorta, ESTADOS, PRIORIDADES, estadoANumero, prioridadANumero } from '../utils/formato'
import { getExpedienteById, getBitacora, cambiarEstado, cambiarPrioridad, eliminarExpediente } from '../services/expedientes'
import EtiquetaSeccion from '../components/EtiquetaSeccion'
import EstadoVacio from '../components/EstadoVacio'

const estadoConfig = {
  Abierto: {
    bg: 'bg-emerald-50', text: 'text-emerald-800',
    dot: 'bg-emerald-500', optionBg: 'hover:bg-emerald-50', optionText: 'text-emerald-800'
  },
  Pausado: {
    bg: 'bg-amber-50', text: 'text-amber-800',
    dot: 'bg-amber-400', optionBg: 'hover:bg-amber-50', optionText: 'text-amber-800'
  },
  Cerrado: {
    bg: 'bg-stone-100', text: 'text-stone-600',
    dot: 'bg-stone-400', optionBg: 'hover:bg-stone-100', optionText: 'text-stone-600'
  },
}

const prioridadConfig = {
  Urgente:     { color: 'text-red-700',    border: 'border-red-300',    symbol: '▲', optionBg: 'hover:bg-red-50',    optionText: 'text-red-700'    },
  Prioritario: { color: 'text-amber-700',  border: 'border-amber-300',  symbol: '●', optionBg: 'hover:bg-amber-50',  optionText: 'text-amber-700'  },
  Normal:      { color: 'text-muted-foreground', border: 'border-border', symbol: '▼', optionBg: 'hover:bg-secondary', optionText: 'text-muted-foreground' },
}

function DropdownEstado({ valor, onChange }) {
  const [abierto, setAbierto] = useState(false)
  const ref = useRef(null)
  const cfg = estadoConfig[valor] ?? estadoConfig.Abierto

  useEffect(() => {
    function handleClickOutside(e) {
      if (ref.current && !ref.current.contains(e.target)) setAbierto(false)
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={() => setAbierto(v => !v)}
        className={`flex items-center gap-2 pl-2.5 pr-2 py-1.5 rounded-full text-xs font-medium border border-border bg-secondary text-foreground transition-colors hover:bg-muted focus:outline-none focus:ring-2 focus:ring-accent/30`}
        style={{ fontFamily: "'DM Mono', monospace" }}
      >
        <span className={`w-1.5 h-1.5 rounded-full shrink-0 ${cfg.dot}`} />
        {valor}
        <ChevronDown size={10} className={`transition-transform ${abierto ? 'rotate-180' : ''}`} />
      </button>

      {abierto && (
        <div className="absolute left-0 top-full mt-1 z-50 bg-card border border-border rounded-lg shadow-md overflow-hidden min-w-[120px]">
          {Object.entries(estadoConfig).map(([etiqueta, c]) => (
            <button
              key={etiqueta}
              onClick={() => {
                onChange(estadoANumero(etiqueta))
                setAbierto(false)
              }}
              className={`w-full flex items-center gap-2 px-3 py-2 text-xs font-medium transition-colors ${c.optionBg} ${c.optionText} ${valor === etiqueta ? 'opacity-100 font-semibold' : 'opacity-80'}`}
              style={{ fontFamily: "'DM Mono', monospace" }}
            >
              <span className={`w-1.5 h-1.5 rounded-full shrink-0 ${c.dot}`} />
              {etiqueta}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

function DropdownPrioridad({ valor, onChange }) {
  const [abierto, setAbierto] = useState(false)
  const ref = useRef(null)
  const cfg = prioridadConfig[valor] ?? prioridadConfig.Normal

  useEffect(() => {
    function handleClickOutside(e) {
      if (ref.current && !ref.current.contains(e.target)) setAbierto(false)
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={() => setAbierto(v => !v)}
        className={`flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-medium border bg-secondary transition-colors hover:bg-muted focus:outline-none focus:ring-2 focus:ring-accent/30 ${cfg.color} ${cfg.border}`}
        style={{ fontFamily: "'DM Mono', monospace" }}
      >
        <span>{cfg.symbol}</span>
        {valor}
        <ChevronDown size={10} className={`transition-transform ${abierto ? 'rotate-180' : ''}`} />
      </button>

      {abierto && (
        <div className="absolute left-0 top-full mt-1 z-50 bg-card border border-border rounded-lg shadow-md overflow-hidden min-w-[130px]">
          {Object.entries(prioridadConfig).map(([etiqueta, c]) => (
            <button
              key={etiqueta}
              onClick={() => {
                onChange(prioridadANumero(etiqueta))
                setAbierto(false)
              }}
              className={`w-full flex items-center gap-2 px-3 py-2 text-xs font-medium transition-colors ${c.optionBg} ${c.optionText} ${valor === etiqueta ? 'font-semibold' : 'opacity-80'}`}
              style={{ fontFamily: "'DM Mono', monospace" }}
            >
              <span>{c.symbol}</span>
              {etiqueta}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

function DropdownAgregarColaborador({ usuariosDisponibles, onAgregar }) {
  const [abierto, setAbierto] = useState(false)
  const [guardando, setGuardando] = useState(false)
  const [error, setError] = useState('')
  const ref = useRef(null)

  useEffect(() => {
    function handleClickOutside(e) {
      if (ref.current && !ref.current.contains(e.target)) setAbierto(false)
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  async function handleSeleccionar(usuarioId) {
    setError('')
    setGuardando(true)
    try {
      await onAgregar(usuarioId)
      setAbierto(false)
    } catch (err) {
      setError(err.response?.data?.mensaje ?? 'No se pudo agregar al colaborador')
    } finally {
      setGuardando(false)
    }
  }

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={() => setAbierto(v => !v)}
        className="flex items-center gap-2 pl-2.5 pr-2 py-1.5 rounded-full text-xs font-medium border border-border bg-secondary text-foreground transition-colors hover:bg-muted focus:outline-none focus:ring-2 focus:ring-accent/30"
        style={{ fontFamily: "'DM Mono', monospace" }}
      >
        <UserPlus size={12} />
        Agregar colaborador
        <ChevronDown size={10} className={`transition-transform ${abierto ? 'rotate-180' : ''}`} />
      </button>

      {abierto && (
        <div className="absolute right-0 top-full mt-1 z-50 bg-card border border-border rounded-lg shadow-md overflow-hidden min-w-[200px] max-h-64 overflow-y-auto">
          {error && (
            <p className="px-3 py-2 text-xs text-red-600 border-b border-border">{error}</p>
          )}
          {usuariosDisponibles.length === 0 ? (
            <p className="px-3 py-2 text-xs text-muted-foreground">No hay usuarios disponibles</p>
          ) : (
            usuariosDisponibles.map(u => (
              <button
                key={u.id}
                disabled={guardando}
                onClick={() => handleSeleccionar(u.id)}
                className="w-full text-left px-3 py-2 text-xs font-medium text-foreground hover:bg-secondary transition-colors disabled:opacity-50"
              >
                {u.nombre}
              </button>
            ))
          )}
        </div>
      )}
    </div>
  )
}

function CapturarDestinoExhorto({ acuerdoId, onGuardar }) {
  const [editando, setEditando] = useState(false)
  const [valor, setValor] = useState('')
  const [guardando, setGuardando] = useState(false)

  if (!editando) {
    return (
      <button
        onClick={() => setEditando(true)}
        className="text-xs text-accent hover:underline font-medium flex items-center gap-1"
      >
        <MapPin size={11} />
        + Agregar destino
      </button>
    )
  }

  async function handleGuardar() {
    if (!valor.trim()) return
    setGuardando(true)
    try {
      await onGuardar(acuerdoId, valor.trim())
      setEditando(false)
    } finally {
      setGuardando(false)
    }
  }

  return (
    <div className="flex items-center gap-2">
      <input
        type="text"
        value={valor}
        onChange={e => setValor(e.target.value)}
        placeholder="Ej. Guadalajara, Jalisco"
        autoFocus
        className="flex-1 bg-input-background text-foreground text-xs px-2.5 py-1 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"
      />
      <button
        onClick={handleGuardar}
        disabled={!valor.trim() || guardando}
        className="text-xs text-accent hover:underline font-medium disabled:opacity-50"
      >
        {guardando ? 'Guardando...' : 'Guardar'}
      </button>
      <button
        onClick={() => setEditando(false)}
        className="text-xs text-muted-foreground hover:text-foreground transition"
      >
        Cancelar
      </button>
    </div>
  )
}

function DetalleExpediente() {
  const { id } = useParams()
  const navigate = useNavigate()
  const usuario = getUsuario()

  const [expediente, setExpediente] = useState(null)
  const [etapas, setEtapas] = useState([])
  const [bitacora, setBitacora] = useState([])
  const [acuerdos, setAcuerdos] = useState([])
  const [acuerdosNuevosIds, setAcuerdosNuevosIds] = useState(new Set())
  const [mostrarFormEtapa, setMostrarFormEtapa] = useState(false)
  const [etapaEditando, setEtapaEditando] = useState(null)
  const [accesos, setAccesos] = useState([])
  const [usuarios, setUsuarios] = useState([])
  const [mostrarFormExhorto, setMostrarFormExhorto] = useState(false)
  const [formExhorto, setFormExhorto] = useState({
    sintesis: '',
    fechaAcuerdo: '',
    nombreJuzgado: '',
    ciudadDestino: ''
  })

  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const [exito, setExito] = useState('')

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

      const dataAccesos = await getAccesos(id)
      setAccesos(dataAccesos)

      const dataUsuarios = await getUsuarios()
      setUsuarios(dataUsuarios)

      const dataAcuerdos = await getAcuerdos(id)
      setAcuerdos(dataAcuerdos)

      const idsNuevos = dataAcuerdos.filter(a => !a.visto).map(a => a.id)
      setAcuerdosNuevosIds(new Set(idsNuevos))
      idsNuevos.forEach(acuerdoId => {
        marcarAcuerdoVisto(acuerdoId).catch(() => {})
      })

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

  async function handleCambiarEstado(nuevoEstado) {
    try {
      await cambiarEstado(id, nuevoEstado)
      await cargarDatos()
    } catch {
      setError('No se pudo cambiar el estado')
    }
  }

  async function handleCambiarPrioridad(nuevaPrioridad) {
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

  async function handleRevertirEtapa(etapaId) {
    try {
      await revertirEtapa(id, etapaId)
      await cargarDatos()
      setExito('Etapa revertida a pendiente correctamente')
      setTimeout(() => setExito(''), 3000)
    } catch {
      setError('No se pudo revertir la etapa')
    }
  }

  function handleEditarEtapa(etapa) {
    setEtapaEditando(etapa)
  }

  async function handleGuardarEdicionEtapa() {
    setEtapaEditando(null)
    await cargarDatos()
    setExito('Etapa actualizada correctamente')
    setTimeout(() => setExito(''), 3000)
  }

  async function handleEliminarEtapa(etapaId) {
    try {
      await eliminarEtapa(id, etapaId)
      await cargarDatos()
      setExito('Etapa eliminada correctamente')
      setTimeout(() => setExito(''), 3000)
    } catch {
      setError('No se pudo eliminar la etapa')
    }
  }

  async function handleGuardarDestinoExhorto(acuerdoId, ciudadDestino) {
    try {
      await actualizarDestinoExhorto(acuerdoId, ciudadDestino)
      await cargarDatos()
    } catch {
      setError('No se pudo guardar el destino del exhorto')
    }
  }

  async function handleGuardarExhortoManual() {
    try {
      await registrarExhortoManual(id, {
        sintesis: formExhorto.sintesis,
        fechaAcuerdo: formExhorto.fechaAcuerdo,
        nombreJuzgado: formExhorto.nombreJuzgado || null,
        ciudadDestino: formExhorto.ciudadDestino || null,
      })
      setMostrarFormExhorto(false)
      setFormExhorto({ sintesis: '', fechaAcuerdo: '', nombreJuzgado: '', ciudadDestino: '' })
      await cargarDatos()
      setExito('Exhorto registrado correctamente')
      setTimeout(() => setExito(''), 3000)
    } catch {
      setError('No se pudo registrar el exhorto')
    }
  }

  async function handleEliminarAcuerdoManual(acuerdoId) {
    if (!window.confirm('¿Eliminar este exhorto registrado manualmente?')) return
    try {
      await eliminarAcuerdoManual(acuerdoId)
      await cargarDatos()
      setExito('Registro eliminado correctamente')
      setTimeout(() => setExito(''), 3000)
    } catch {
      setError('No se pudo eliminar el registro')
    }
  }

  async function handleAgregarColaborador(usuarioId) {
    await agregarAcceso(id, usuarioId)
    await cargarDatos()
    setExito('Colaborador agregado correctamente')
    setTimeout(() => setExito(''), 3000)
  }

  async function handleQuitarColaborador(accesoId) {
    try {
      await quitarAcceso(id, accesoId)
      await cargarDatos()
      setExito('Colaborador removido correctamente')
      setTimeout(() => setExito(''), 3000)
    } catch {
      setError('No se pudo quitar al colaborador')
    }
  }

  async function handleEliminar() {
    if (!window.confirm(`¿Estás seguro de que deseas eliminar el expediente ${expediente.numeroExpediente}? Esta acción no se puede deshacer.`)) return
    try {
      await eliminarExpediente(id)
      navigate('/expedientes')
    } catch {
      setError('No se pudo eliminar el expediente')
    }
  }

  if (cargando) {
    return (
      <div className="min-h-screen bg-background">
        <Topbar />
        <div className="max-w-screen-xl mx-auto px-6 py-8 text-center text-muted-foreground">
          Cargando expediente...
        </div>
      </div>
    )
  }

  if (error && !expediente) {
    return (
      <div className="min-h-screen bg-background">
        <Topbar />
        <div className="max-w-screen-xl mx-auto px-6 py-8 text-center">
          <p className="text-muted-foreground mb-4">{error}</p>
          <button
            onClick={() => navigate('/expedientes')}
            className="text-accent hover:underline text-sm"
          >
            ← Volver a expedientes
          </button>
        </div>
      </div>
    )
  }

  const puedeGestionarColaboradores = usuario?.id === 1 || usuario?.id === expediente.usuarioAsignadoId
  const usuariosDisponiblesColaborador = usuarios.filter(u =>
    u.id !== expediente.usuarioAsignadoId &&
    !accesos.some(a => a.usuarioId === u.id)
  )

  const labelClass = "block text-xs font-medium uppercase tracking-widest text-muted-foreground mb-1.5"
  const inputClass = "w-full bg-input-background text-foreground text-sm px-3 py-1.5 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"

  return (
    <div className="min-h-screen bg-background">
      <Topbar
        breadcrumb={
          <div className="flex items-center gap-2 text-sm">
            <button
              onClick={() => navigate('/expedientes')}
              className="text-primary-foreground/55 hover:text-primary-foreground transition flex items-center gap-1"
            >
              <ArrowLeft size={13} />
              Expedientes
            </button>
            <span className="text-primary-foreground/25">/</span>
            <span className="text-primary-foreground/75" style={{ fontFamily: "'DM Mono', monospace" }}>
              {expediente.numeroExpediente}
            </span>
          </div>
        }
      />

      <main className="max-w-screen-xl mx-auto px-6 py-8 space-y-8">

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
            {error}
          </div>
        )}

        {exito && (
          <div className="bg-emerald-50 border border-emerald-200 text-emerald-700 text-sm rounded-md px-3 py-2">
            {exito}
          </div>
        )}

        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4">
          <div>
            <div className="flex items-center gap-2 mb-1">
              <FileText size={14} className="text-accent" />
              <span className="text-xs text-muted-foreground" style={{ fontFamily: "'DM Mono', monospace" }}>
                {expediente.numeroExpediente}
              </span>
            </div>
            <h1
              className="text-2xl text-foreground leading-snug"
              style={{ fontFamily: "'Playfair Display', serif" }}
            >
              {expediente.parteDemandada}
            </h1>
            <p className="text-sm text-muted-foreground mt-1">{expediente.materia ?? '—'}</p>
          </div>

          <div className="flex items-center gap-2 shrink-0">

            <DropdownEstado
              valor={expediente.estado}
              onChange={handleCambiarEstado}
            />

            <DropdownPrioridad
              valor={expediente.prioridad}
              onChange={handleCambiarPrioridad}
            />

            {puedeGestionarColaboradores && (
              <DropdownAgregarColaborador
                usuariosDisponibles={usuariosDisponiblesColaborador}
                onAgregar={handleAgregarColaborador}
              />
            )}

            {/* Botón Editar */}
            <button
              onClick={() => navigate(`/expedientes/${id}/editar`)}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-medium border border-accent text-accent hover:bg-accent hover:text-accent-foreground transition-colors"
            >
              <Pencil size={11} />
              Editar
            </button>

            {/* Botón Eliminar */}
            <button
              onClick={handleEliminar}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-medium border border-destructive/40 text-destructive hover:bg-destructive hover:text-destructive-foreground transition-colors"
            >
              <Trash2 size={11} />
              Eliminar
            </button>

          </div>
        </div>

        {/* Info grid */}
        <section>
          <EtiquetaSeccion className="mb-3">Información del expediente</EtiquetaSeccion>
          <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
            <InfoCard icon={Gavel}    label="Juzgado"              value={expediente.juzgado ?? '—'} />
            <InfoCard icon={FileText} label="Tipo de juicio"       value={expediente.tipoJuicio ?? '—'} />
            <InfoCard icon={User}     label="Asignado a"           value={expediente.usuarioAsignadoNombre ?? '—'} />
            <InfoCard
              icon={Users}
              label="Visible para"
              value={
                accesos.length === 0 ? '—' : (
                  <div className="flex flex-wrap gap-1">
                    {accesos.map(acceso => (
                      <span
                        key={acceso.id}
                        className="inline-flex items-center gap-1 bg-secondary text-foreground text-xs px-2 py-0.5 rounded-full"
                      >
                        {acceso.usuarioNombre}
                        {puedeGestionarColaboradores && (
                          <button
                            onClick={() => handleQuitarColaborador(acceso.id)}
                            className="text-muted-foreground hover:text-destructive transition"
                          >
                            <X size={10} />
                          </button>
                        )}
                      </span>
                    ))}
                  </div>
                )
              }
            />
            <InfoCard icon={BookOpen} label="Materia"              value={expediente.materia ?? '—'} />
            <InfoCard icon={Landmark} label="Banco"                value={expediente.bancoNombre ?? '—'} />
            <InfoCard icon={Clock}    label="Última actualización" value={formatearFecha(expediente.actualizadoEn)} />
          </div>

          {/* Notas */}
          <div className="mt-3 bg-card border border-border rounded-lg p-5">
            <div className="flex items-center gap-2 mb-3">
              <StickyNote size={14} className="text-accent" />
              <span
                className="text-xs font-medium uppercase tracking-widest text-foreground"
                style={{ fontFamily: "'DM Mono', monospace" }}
              >
                Notas del expediente
              </span>
            </div>
            <p className="text-sm text-foreground leading-relaxed">
              {expediente.notas || 'Sin notas registradas.'}
            </p>
          </div>
        </section>

        {/* Etapas */}
        <section>
          <div className="flex items-center justify-between mb-3">
            <EtiquetaSeccion>Historial de etapas procesales</EtiquetaSeccion>
            {!mostrarFormEtapa && (
              <button
                onClick={() => setMostrarFormEtapa(true)}
                className="text-xs text-accent hover:underline font-medium"
              >
                + Registrar etapa
              </button>
            )}
          </div>

          {mostrarFormEtapa && (
            <div className="mb-3">
              <FormularioEtapa
                expedienteId={id}
                tipoJuicio={expediente.tipoJuicio}
                onGuardado={() => {
                  setMostrarFormEtapa(false)
                  cargarDatos()
                }}
                onCancelar={() => setMostrarFormEtapa(false)}
              />
            </div>
          )}

          <div className="bg-card border border-border rounded-lg p-4">
            <HistorialEtapas
              etapas={etapas}
              onCompletar={handleCompletarEtapa}
              onRevertir={handleRevertirEtapa}
              onEditar={handleEditarEtapa}
              onEliminar={handleEliminarEtapa}
            />
          </div>

          {etapaEditando && (
            <ModalEditarEtapa
              expedienteId={id}
              etapa={etapaEditando}
              tipoJuicio={expediente.tipoJuicio}
              onGuardado={handleGuardarEdicionEtapa}
              onCerrar={() => setEtapaEditando(null)}
            />
          )}
        </section>

        {/* Acuerdos del Poder Judicial */}
        <section>
          <div className="flex items-center justify-between mb-3">
            <h2
              className="text-xs font-medium uppercase tracking-widest text-foreground flex items-center gap-2"
              style={{ fontFamily: "'DM Mono', monospace" }}
            >
              <Scale size={13} className="text-accent" />
              Acuerdos del Poder Judicial
              {acuerdosNuevosIds.size > 0 && (
                <span className="bg-accent text-accent-foreground text-[10px] font-semibold rounded-full px-1.5 py-0.5 normal-case tracking-normal">
                  {acuerdosNuevosIds.size} nuevo{acuerdosNuevosIds.size !== 1 ? 's' : ''}
                </span>
              )}
            </h2>
            {!mostrarFormExhorto && (
              <button
                onClick={() => setMostrarFormExhorto(true)}
                className="text-xs text-accent hover:underline font-medium"
              >
                + Registrar exhorto manualmente
              </button>
            )}
          </div>

          {mostrarFormExhorto && (
            <div className="bg-secondary/40 border border-border rounded-lg p-4 mb-3">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-3">
                <div>
                  <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Fecha del acuerdo *</label>
                  <input
                    type="date"
                    value={formExhorto.fechaAcuerdo}
                    onChange={e => setFormExhorto(f => ({ ...f, fechaAcuerdo: e.target.value }))}
                    className={inputClass}
                  />
                </div>
                <div>
                  <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Juzgado (opcional)</label>
                  <input
                    type="text"
                    value={formExhorto.nombreJuzgado}
                    onChange={e => setFormExhorto(f => ({ ...f, nombreJuzgado: e.target.value }))}
                    placeholder="Ej. 1ro Civil Hermosillo"
                    className={inputClass}
                  />
                </div>
              </div>
              <div className="mb-3">
                <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Ciudad/estado destino (opcional)</label>
                <input
                  type="text"
                  value={formExhorto.ciudadDestino}
                  onChange={e => setFormExhorto(f => ({ ...f, ciudadDestino: e.target.value }))}
                  placeholder="Ej. Guadalajara, Jalisco"
                  className={inputClass}
                />
              </div>
              <div className="mb-3">
                <label className={labelClass} style={{ fontFamily: "'DM Mono', monospace" }}>Síntesis *</label>
                <textarea
                  value={formExhorto.sintesis}
                  onChange={e => setFormExhorto(f => ({ ...f, sintesis: e.target.value }))}
                  rows={3}
                  placeholder="Descripción del exhorto..."
                  className={`${inputClass} resize-none`}
                />
              </div>
              <div className="flex justify-end gap-2">
                <button
                  onClick={() => setMostrarFormExhorto(false)}
                  className="px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground transition"
                >
                  Cancelar
                </button>
                <button
                  onClick={handleGuardarExhortoManual}
                  disabled={!formExhorto.sintesis || !formExhorto.fechaAcuerdo}
                  className="bg-accent text-accent-foreground px-4 py-1.5 rounded text-sm font-medium hover:opacity-90 transition disabled:opacity-50"
                >
                  Guardar exhorto
                </button>
              </div>
            </div>
          )}

          {acuerdos.length === 0 ? (
            <div className="bg-card border border-border rounded-lg p-5">
              <EstadoVacio
                icon={Scale}
                titulo="Sin acuerdos registrados"
                subtitulo="Los acuerdos del Poder Judicial aparecerán aquí automáticamente."
              />
            </div>
          ) : (
            <div className="space-y-3">
              {[...acuerdos]
                .sort((a, b) => new Date(b.fechaAcuerdo) - new Date(a.fechaAcuerdo))
                .map((acuerdo) => (
                  <div key={acuerdo.id} className="bg-card border border-border rounded-lg p-5">
                    <div className="flex flex-wrap items-center justify-between gap-2 mb-2">
                      <span className="flex items-center gap-1.5 text-xs font-medium text-foreground">
                        <Gavel size={12} className="text-accent" />
                        {acuerdo.nombreJuzgado}
                        {acuerdo.esExhorto && (
                          <span className="flex items-center gap-1 bg-amber-100 text-amber-800 text-[10px] font-semibold rounded-full px-1.5 py-0.5">
                            <Send size={9} />
                            Exhorto
                            {acuerdo.registradoManualmente && (
                              <span className="italic font-normal">(manual)</span>
                            )}
                          </span>
                        )}
                        {acuerdo.registradoManualmente && (
                          <button
                            onClick={() => handleEliminarAcuerdoManual(acuerdo.id)}
                            className="text-xs text-red-400 hover:text-red-600 transition"
                            title="Eliminar registro manual"
                          >
                            Eliminar
                          </button>
                        )}
                        {acuerdosNuevosIds.has(acuerdo.id) && (
                          <span className="flex items-center gap-1 bg-accent/10 text-accent text-[10px] font-semibold rounded-full px-1.5 py-0.5">
                            <span className="w-1.5 h-1.5 rounded-full bg-accent" />
                            Nuevo
                          </span>
                        )}
                      </span>
                      <span
                        className="text-xs text-muted-foreground"
                        style={{ fontFamily: "'DM Mono', monospace" }}
                      >
                        {formatearFechaCorta(acuerdo.fechaAcuerdo)}
                      </span>
                    </div>
                    <p className="text-xs text-muted-foreground mb-2">{acuerdo.partes}</p>
                    <p className="text-sm text-foreground leading-relaxed">{acuerdo.sintesis}</p>
                    {acuerdo.esExhorto && (
                      <div className="mt-3 pt-3 border-t border-border">
                        {acuerdo.ciudadDestino ? (
                          <p className="text-xs text-muted-foreground flex items-center gap-1.5">
                            <MapPin size={11} className="text-accent" />
                            Destino: <span className="text-foreground font-medium">{acuerdo.ciudadDestino}</span>
                          </p>
                        ) : (
                          <CapturarDestinoExhorto acuerdoId={acuerdo.id} onGuardar={handleGuardarDestinoExhorto} />
                        )}
                      </div>
                    )}
                  </div>
                ))}
            </div>
          )}
        </section>

        {/* Bitácora — solo Socio */}
        {usuario?.rol === 'Socio' && (
          <section>
            <h2
              className="text-xs font-medium uppercase tracking-widest text-foreground mb-3 flex items-center gap-2"
              style={{ fontFamily: "'DM Mono', monospace" }}
            >
              <ClipboardList size={13} className="text-accent" />
              Bitácora de cambios
            </h2>
            <div className="bg-card border border-border rounded-lg overflow-hidden">
              {bitacora.length === 0 ? (
                <EstadoVacio
                  icon={ClipboardList}
                  titulo="Sin registros todavía"
                  subtitulo="Los cambios realizados en este expediente se registrarán aquí."
                />
              ) : (
                <table className="w-full text-sm border-collapse">
                  <thead>
                    <tr className="bg-secondary/60 border-b border-border">
                      {['Acción', 'Usuario', 'Fecha y hora', 'Detalle'].map(col => (
                        <th
                          key={col}
                          className="text-left px-4 py-2.5 text-xs font-medium text-muted-foreground uppercase tracking-wider"
                          style={{ fontFamily: "'DM Mono', monospace" }}
                        >
                          {col}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {bitacora.map((item, i) => (
                      <tr key={item.id} className={`border-b border-border last:border-0 ${i % 2 === 0 ? '' : 'bg-secondary/20'}`}>
                        <td className="px-4 py-3 whitespace-nowrap">
                          <span className="flex items-center gap-1.5 text-foreground font-medium text-xs capitalize">
                            <Clock size={11} className="text-accent" />
                            {item.accion.replace('_', ' ')}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-muted-foreground text-xs whitespace-nowrap">{item.usuarioNombre}</td>
                        <td className="px-4 py-3 text-muted-foreground text-xs whitespace-nowrap" style={{ fontFamily: "'DM Mono', monospace" }}>
                          {formatearFecha(item.fecha)}
                        </td>
                        <td className="px-4 py-3 text-muted-foreground text-xs">{item.detalle}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </section>
        )}

        <div className="pb-4">
          <button
            onClick={() => navigate('/expedientes')}
            className="flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground transition"
          >
            <ArrowLeft size={14} />
            Volver al listado
          </button>
        </div>
      </main>
    </div>
  )
}

export default DetalleExpediente