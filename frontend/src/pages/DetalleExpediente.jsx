import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import {
  ArrowLeft, FileText, Gavel, BookOpen, StickyNote, Clock,
  ClipboardList, User, Landmark, Pencil, Trash2, ChevronDown, Scale, Send
} from 'lucide-react'
import Topbar from '../components/Topbar'
import InfoCard from '../components/InfoCard'
import FormularioEtapa from '../components/FormularioEtapa'
import HistorialEtapas from '../components/HistorialEtapas'
import ModalEditarEtapa from '../components/ModalEditarEtapa'
import FormularioExhorto from '../components/FormularioExhorto'
import ListaExhortos from '../components/ListaExhortos'
import ModalEditarExhorto from '../components/ModalEditarExhorto'
import { getHistorialEtapas, completarEtapa, revertirEtapa, eliminarEtapa } from '../services/etapas'
import { getExhortos, eliminarExhorto } from '../services/exhortos'
import { getUsuario } from '../services/auth'
import { getAcuerdos, marcarAcuerdoVisto } from '../services/acuerdos'
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
  const [exhortos, setExhortos] = useState([])
  const [mostrarFormExhorto, setMostrarFormExhorto] = useState(false)
  const [exhortoEditando, setExhortoEditando] = useState(null)

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

      const dataExhortos = await getExhortos(id)
      setExhortos(dataExhortos)

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

  function handleEditarExhorto(exhorto) {
    setExhortoEditando(exhorto)
  }

  async function handleGuardarEdicionExhorto() {
    setExhortoEditando(null)
    await cargarDatos()
    setExito('Exhorto actualizado correctamente')
    setTimeout(() => setExito(''), 3000)
  }

  async function handleEliminarExhorto(exhortoId) {
    try {
      await eliminarExhorto(id, exhortoId)
      await cargarDatos()
      setExito('Exhorto eliminado correctamente')
      setTimeout(() => setExito(''), 3000)
    } catch {
      setError('No se pudo eliminar el exhorto')
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

        {/* Exhortos */}
        <section>
          <div className="flex items-center justify-between mb-3">
            <EtiquetaSeccion>Exhortos</EtiquetaSeccion>
            {!mostrarFormExhorto && (
              <button
                onClick={() => setMostrarFormExhorto(true)}
                className="text-xs text-accent hover:underline font-medium"
              >
                + Registrar exhorto
              </button>
            )}
          </div>

          {mostrarFormExhorto && (
            <div className="mb-3">
              <FormularioExhorto
                expedienteId={id}
                onGuardado={() => {
                  setMostrarFormExhorto(false)
                  cargarDatos()
                }}
                onCancelar={() => setMostrarFormExhorto(false)}
              />
            </div>
          )}

          <div className="bg-card border border-border rounded-lg p-4">
            <ListaExhortos
              exhortos={exhortos}
              onEditar={handleEditarExhorto}
              onEliminar={handleEliminarExhorto}
            />
          </div>

          {exhortoEditando && (
            <ModalEditarExhorto
              expedienteId={id}
              exhorto={exhortoEditando}
              onGuardado={handleGuardarEdicionExhorto}
              onCerrar={() => setExhortoEditando(null)}
            />
          )}
        </section>

        {/* Acuerdos del Poder Judicial */}
        <section>
          <h2
            className="text-xs font-medium uppercase tracking-widest text-foreground mb-3 flex items-center gap-2"
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