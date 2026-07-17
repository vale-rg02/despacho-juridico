import { useState, useEffect, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { AlertTriangle, Clock, XCircle, CheckCircle, Filter, ChevronDown } from 'lucide-react'
import Topbar from '../components/Topbar'
import EstadoVacio from '../components/EstadoVacio'
import { getUsuario } from '../services/auth'
import { getPanelNotificaciones, getUsuariosDisponibles, marcarAtendida } from '../services/notificaciones'
import { formatearFechaCorta } from '../utils/formato'

function urgenciaClase(dias) {
  if (dias <= 1) return 'text-red-600 bg-red-50'
  if (dias <= 7) return 'text-amber-700 bg-amber-50'
  return 'text-blue-700 bg-blue-50'
}

function textoDiasRestantes(dias) {
  if (dias === 0) return 'Hoy'
  if (dias === 1) return 'Mañana'
  return `en ${dias} días`
}

function CardUrgente({ exp, onClick }) {
  return (
    <button
      onClick={onClick}
      className="text-left bg-card border border-border rounded-lg p-4 transition hover:border-accent/40 hover:shadow-md hover:-translate-y-0.5"
    >
      <div className="flex items-center justify-between gap-2 mb-2">
        <span className="text-xs font-medium text-accent" style={{ fontFamily: "'DM Mono', monospace" }}>
          {exp.numeroExpediente}
        </span>
        <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-red-50 text-red-700">Urgente</span>
      </div>
      <p className="text-sm font-medium text-foreground mb-1">{exp.parteDemandada}</p>
      <p className="text-xs text-muted-foreground">{exp.juzgado ?? '—'} · {exp.materia ?? '—'}</p>
      {exp.usuarioAsignadoNombre && (
        <p className="text-xs text-muted-foreground mt-1">Asignado a: {exp.usuarioAsignadoNombre}</p>
      )}
    </button>
  )
}

function FilaEtapa({ item, tipo, onNavegar, onAtender }) {
  return (
    <div className="flex items-center justify-between gap-3 px-4 py-3 border-b border-border last:border-0">
      <div className="min-w-0">
        <div className="flex items-center gap-2 mb-0.5">
          <button
            onClick={() => onNavegar(item.expedienteId)}
            className="text-xs font-medium text-accent hover:underline"
            style={{ fontFamily: "'DM Mono', monospace" }}
          >
            {item.numeroExpediente}
          </button>
          {item.usuarioAsignadoNombre && (
            <span className="text-xs text-muted-foreground">· {item.usuarioAsignadoNombre}</span>
          )}
        </div>
        <p className="text-sm text-foreground truncate">{item.etapaNombre ?? 'Etapa'}</p>
        <p className="text-xs text-muted-foreground mt-0.5" style={{ fontFamily: "'DM Mono', monospace" }}>
          Límite: {formatearFechaCorta(item.fechaLimite)}
        </p>
      </div>
      <div className="flex flex-col items-end gap-1.5 shrink-0">
        {tipo === 'proxima' && (
          <span className={`text-xs font-medium px-2 py-0.5 rounded-full whitespace-nowrap ${urgenciaClase(item.diasRestantes)}`}>
            {textoDiasRestantes(item.diasRestantes)}
          </span>
        )}
        {tipo === 'vencida' && (
          <span className="text-xs font-medium px-2 py-0.5 rounded-full whitespace-nowrap bg-red-50 text-red-700">
            Vencida hace {item.diasVencida} día{item.diasVencida !== 1 ? 's' : ''}
          </span>
        )}
        {(tipo === 'proxima' || tipo === 'vencida') && (
          <button
            onClick={() => onAtender(item.expedienteId, item.id)}
            className="text-xs text-muted-foreground hover:text-foreground underline"
          >
            Marcar como atendida
          </button>
        )}
      </div>
    </div>
  )
}

function Notificaciones() {
  const navigate = useNavigate()
  const usuario = getUsuario()
  const esSocioPrincipal = usuario?.id === 1

  const [seccionActiva, setSeccionActiva] = useState('urgentes')
  const [panel, setPanel] = useState({ expedientesUrgentes: [], proximas: [], vencidas: [], atendidas: [] })
  const [usuarios, setUsuarios] = useState([])
  const [filtroUsuarioId, setFiltroUsuarioId] = useState(0)
  const [dropdownOpen, setDropdownOpen] = useState(false)

  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    if (esSocioPrincipal) cargarUsuariosDisponibles()
  }, [])

  useEffect(() => {
    cargarPanel()
  }, [filtroUsuarioId])

  async function cargarUsuariosDisponibles() {
    try {
      const data = await getUsuariosDisponibles()
      setUsuarios(data)
    } catch {
      // silencioso: el filtro simplemente no se puebla
    }
  }

  async function cargarPanel() {
    setCargando(true)
    setError('')
    try {
      const data = await getPanelNotificaciones(esSocioPrincipal ? filtroUsuarioId : undefined)
      setPanel(data)
    } catch {
      setError('No se pudo cargar el panel de notificaciones')
    } finally {
      setCargando(false)
    }
  }

  async function handleAtender(expedienteId, etapaId) {
    try {
      await marcarAtendida(expedienteId, etapaId)
      setPanel(prev => ({
        ...prev,
        proximas: prev.proximas.filter(p => p.id !== etapaId),
        vencidas: prev.vencidas.filter(v => v.id !== etapaId),
      }))
    } catch {
      setError('No se pudo marcar la etapa como atendida')
    }
  }

  const nombreFiltro = useMemo(() => {
    if (!esSocioPrincipal) return null
    if (filtroUsuarioId === 0) return 'Todos los usuarios'
    if (filtroUsuarioId === usuario?.id) return 'Mis notificaciones'
    const u = usuarios.find(u => u.id === filtroUsuarioId)
    return u ? u.nombre : 'Todos los usuarios'
  }, [filtroUsuarioId, usuarios])

  const secciones = [
    { key: 'urgentes', label: 'Urgentes', icon: AlertTriangle, count: panel.expedientesUrgentes.length },
    { key: 'proximas', label: 'Próximas', icon: Clock, count: panel.proximas.length },
    { key: 'vencidas', label: 'Vencidas', icon: XCircle, count: panel.vencidas.length },
    { key: 'atendidas', label: 'Atendidas', icon: CheckCircle, count: panel.atendidas.length },
  ]

  function irAlExpediente(id) {
    navigate(`/expedientes/${id}`)
  }

  return (
    <div className="min-h-screen bg-background">
      <Topbar
        breadcrumb={
          <span className="text-primary-foreground/75 text-sm">Notificaciones</span>
        }
      />

      <main className="max-w-screen-xl mx-auto px-6 py-8">
        <div className="flex items-center justify-between gap-4 mb-6">
          <h1 className="text-2xl text-foreground" style={{ fontFamily: "'Playfair Display', serif" }}>
            Panel de Notificaciones
          </h1>

          {esSocioPrincipal && (
            <div className="relative">
              <button
                onClick={() => setDropdownOpen(o => !o)}
                className="flex items-center gap-2 bg-input-background hover:bg-secondary text-foreground text-sm px-3 py-1.5 rounded transition border border-border"
              >
                <Filter size={12} className="text-muted-foreground" />
                <span className="max-w-[180px] truncate">{nombreFiltro}</span>
                <ChevronDown size={12} className={`text-muted-foreground transition-transform ${dropdownOpen ? 'rotate-180' : ''}`} />
              </button>
              {dropdownOpen && (
                <div className="absolute right-0 mt-1.5 w-56 bg-card border border-border rounded shadow-lg z-50 overflow-hidden max-h-64 overflow-y-auto">
                  <button
                    onClick={() => { setFiltroUsuarioId(0); setDropdownOpen(false) }}
                    className={`w-full text-left px-4 py-2.5 text-sm hover:bg-secondary transition ${filtroUsuarioId === 0 ? 'font-medium text-accent' : 'text-foreground'}`}
                  >
                    Todos los usuarios
                  </button>
                  <button
                    onClick={() => { setFiltroUsuarioId(usuario?.id); setDropdownOpen(false) }}
                    className={`w-full text-left px-4 py-2.5 text-sm hover:bg-secondary transition ${filtroUsuarioId === usuario?.id ? 'font-medium text-accent' : 'text-foreground'}`}
                  >
                    Mis notificaciones
                  </button>
                  <div className="border-t border-border" />
                  {usuarios.filter(u => u.id !== usuario?.id).map(u => (
                    <button
                      key={u.id}
                      onClick={() => { setFiltroUsuarioId(u.id); setDropdownOpen(false) }}
                      className={`w-full text-left px-4 py-2.5 text-sm hover:bg-secondary transition ${filtroUsuarioId === u.id ? 'font-medium text-accent' : 'text-foreground'}`}
                    >
                      {u.nombre}
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>

        {dropdownOpen && (
          <div className="fixed inset-0 z-20" onClick={() => setDropdownOpen(false)} />
        )}

        {error && (
          <div className="mb-4 bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
            {error}
          </div>
        )}

        {cargando ? (
          <div className="text-center py-16 text-muted-foreground text-sm">
            Cargando notificaciones...
          </div>
        ) : (
          <div className="flex flex-col md:flex-row gap-6">
            <aside className="md:w-48 shrink-0">
              <nav className="flex md:flex-col gap-1 overflow-x-auto md:overflow-visible">
                {secciones.map(s => (
                  <button
                    key={s.key}
                    onClick={() => setSeccionActiva(s.key)}
                    className={`flex items-center justify-between gap-2 px-3 py-2 rounded-md text-sm transition whitespace-nowrap ${
                      seccionActiva === s.key
                        ? 'bg-accent/10 text-accent font-medium'
                        : 'text-muted-foreground hover:bg-secondary hover:text-foreground'
                    }`}
                  >
                    <span className="flex items-center gap-2">
                      <s.icon size={15} />
                      {s.label}
                    </span>
                    {s.count > 0 && (
                      <span
                        className="text-xs bg-secondary text-muted-foreground rounded-full px-1.5 py-0.5"
                        style={{ fontFamily: "'DM Mono', monospace" }}
                      >
                        {s.count}
                      </span>
                    )}
                  </button>
                ))}
              </nav>
            </aside>

            <div className="flex-1 min-w-0">
              {seccionActiva === 'urgentes' && (
                panel.expedientesUrgentes.length === 0 ? (
                  <EstadoVacio
                    icon={AlertTriangle}
                    titulo="Sin expedientes urgentes"
                    subtitulo="Cuando un expediente entre en estado urgente aparecerá en esta lista."
                  />
                ) : (
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                    {panel.expedientesUrgentes.map(exp => (
                      <CardUrgente key={exp.id} exp={exp} onClick={() => irAlExpediente(exp.id)} />
                    ))}
                  </div>
                )
              )}

              {seccionActiva === 'proximas' && (
                panel.proximas.length === 0 ? (
                  <EstadoVacio
                    icon={Clock}
                    titulo="Sin etapas próximas a vencer"
                    subtitulo="Las etapas con fecha límite cercana se mostrarán aquí."
                  />
                ) : (
                  <div className="bg-card border border-border rounded-lg overflow-hidden">
                    {panel.proximas.map(item => (
                      <FilaEtapa key={item.id} item={item} tipo="proxima" onNavegar={irAlExpediente} onAtender={handleAtender} />
                    ))}
                  </div>
                )
              )}

              {seccionActiva === 'vencidas' && (
                panel.vencidas.length === 0 ? (
                  <EstadoVacio
                    icon={XCircle}
                    titulo="Sin etapas vencidas"
                    subtitulo="Buen trabajo — no hay plazos vencidos pendientes."
                  />
                ) : (
                  <div className="bg-card border border-border rounded-lg overflow-hidden">
                    {panel.vencidas.map(item => (
                      <FilaEtapa key={item.id} item={item} tipo="vencida" onNavegar={irAlExpediente} onAtender={handleAtender} />
                    ))}
                  </div>
                )
              )}

              {seccionActiva === 'atendidas' && (
                panel.atendidas.length === 0 ? (
                  <EstadoVacio
                    icon={CheckCircle}
                    titulo="Sin etapas atendidas"
                    subtitulo="Las etapas que marques como atendidas aparecerán aquí."
                  />
                ) : (
                  <div className="bg-card border border-border rounded-lg overflow-hidden">
                    {panel.atendidas.map(item => (
                      <FilaEtapa key={item.id} item={item} tipo="atendida" onNavegar={irAlExpediente} />
                    ))}
                  </div>
                )
              )}
            </div>
          </div>
        )}
      </main>
    </div>
  )
}

export default Notificaciones
