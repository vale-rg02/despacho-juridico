import { useState, useRef, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { Scale, Bell, LogOut, AlertTriangle, Calendar } from 'lucide-react'
import { logout, getUsuario } from '../services/auth'
import { getAlertas, marcarAtendida } from '../services/notificaciones'
import { formatearFecha } from '../utils/formato'

function iniciales(nombre) {
  if (!nombre) return '??'
  const partes = nombre.trim().split(' ')
  if (partes.length === 1) return partes[0].slice(0, 2).toUpperCase()
  return (partes[0][0] + partes[partes.length - 1][0]).toUpperCase()
}

function urgenciaClase(diasRestantes) {
  if (diasRestantes <= 2) return 'text-red-600 bg-red-50'
  if (diasRestantes <= 7) return 'text-amber-700 bg-amber-50'
  return 'text-blue-700 bg-blue-50'
}

function formatFechaAlerta(diasRestantes) {
  if (diasRestantes < 0) return `Vencida hace ${Math.abs(diasRestantes)} día${Math.abs(diasRestantes) !== 1 ? 's' : ''}`
  if (diasRestantes === 0) return 'Hoy'
  if (diasRestantes === 1) return 'Mañana'
  return `en ${diasRestantes} días`
}

function AlertaItem({ alerta, onAtender, onNavegar }) {
  return (
    <div
      onClick={() => onNavegar(alerta.expedienteId)}
      className="px-4 py-3 hover:bg-secondary/40 transition cursor-pointer"
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-xs font-medium text-foreground truncate">{alerta.etapaNombre ?? 'Etapa'}</p>
          <p className="text-xs text-muted-foreground mt-0.5" style={{ fontFamily: "'DM Mono', monospace" }}>
            {alerta.numeroExpediente}
          </p>
          <p className="text-xs text-muted-foreground mt-0.5">
            Límite: {formatearFecha(alerta.fechaLimite)}
          </p>
        </div>
        <div className="flex flex-col items-end gap-1.5 shrink-0">
          <span className={`text-xs font-medium px-2 py-0.5 rounded-full whitespace-nowrap ${urgenciaClase(alerta.diasRestantes)}`}>
            {formatFechaAlerta(alerta.diasRestantes)}
          </span>
          <button
            onClick={(e) => { e.stopPropagation(); onAtender(alerta) }}
            className="text-xs text-muted-foreground hover:text-foreground underline"
          >
            Atender
          </button>
        </div>
      </div>
    </div>
  )
}

function Topbar({ breadcrumb }) {
  const navigate = useNavigate()
  const usuario = getUsuario()
  const esSocioPrincipal = usuario?.id === 1
  const [bellOpen, setBellOpen] = useState(false)
  const [alertas, setAlertas] = useState([])
  const bellRef = useRef(null)

  // Separar alertas propias de las ajenas (solo aplica para Socio Principal;
  // el backend ya limita a los demás roles a solo sus propias alertas)
  const alertasPropias = esSocioPrincipal
    ? alertas.filter(a => a.usuarioAsignadoId === usuario?.id || !a.usuarioAsignadoId)
    : alertas

  const alertasAjenas = esSocioPrincipal
    ? alertas.filter(a => a.usuarioAsignadoId !== usuario?.id && a.usuarioAsignadoId)
    : []

  const gruposAjenos = alertasAjenas.reduce((grupos, alerta) => {
    const nombre = alerta.usuarioAsignadoNombre ?? 'Sin asignar'
    if (!grupos[nombre]) grupos[nombre] = []
    grupos[nombre].push(alerta)
    return grupos
  }, {})

  useEffect(() => {
    cargarAlertas()
  }, [])

  useEffect(() => {
    function handler(e) {
      if (bellRef.current && !bellRef.current.contains(e.target)) {
        setBellOpen(false)
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  async function cargarAlertas() {
    try {
      const data = await getAlertas()
      setAlertas(data)
    } catch {
      // silencioso: la topbar no debe romper la pantalla
    }
  }

  async function handleAtender(alerta) {
    try {
      await marcarAtendida(alerta.expedienteId, alerta.etapaHistorialId)
      setAlertas(prev => prev.filter(a => a.etapaHistorialId !== alerta.etapaHistorialId))
    } catch {
      // si falla, se queda en la lista
    }
  }

  function irAlExpediente(expedienteId) {
    setBellOpen(false)
    navigate(`/expedientes/${expedienteId}`)
  }

  function handleLogout() {
    logout()
    navigate('/login')
  }

  return (
    <header className="bg-primary border-b border-white/10 sticky top-0 z-40">
      <div className="max-w-screen-xl mx-auto px-6 h-14 flex items-center gap-4">
        {/* Logo */}
        <div
          className="flex items-center gap-2.5 shrink-0 cursor-pointer"
          onClick={() => navigate('/expedientes')}
        >
          <Scale size={18} className="text-accent" />
          <span
            className="text-primary-foreground text-base tracking-wide"
            style={{ fontFamily: "'Playfair Display', serif", fontWeight: 600 }}
          >
            Despacho Jurídico Acedo e Hijos
          </span>
        </div>

        {breadcrumb && (
          <>
            <span className="text-primary-foreground/25 mx-1">·</span>
            {breadcrumb}
          </>
        )}

        <div className="ml-auto flex items-center gap-3">
          {/* Calendario */}
          <button
            onClick={() => navigate('/calendario')}
            className="p-2 rounded-md text-primary-foreground/60 hover:text-primary-foreground hover:bg-white/10 transition"
            title="Calendario"
          >
            <Calendar size={17} />
          </button>

          {/* Bell */}
          <div className="relative" ref={bellRef}>
            <button
              onClick={() => setBellOpen(o => !o)}
              className="relative p-2 rounded-md text-primary-foreground/60 hover:text-primary-foreground hover:bg-white/10 transition"
            >
              <Bell size={17} />
              {alertas.length > 0 && (
                <span className="absolute top-1 right-1 w-4 h-4 bg-red-500 text-white text-[10px] font-bold rounded-full flex items-center justify-center leading-none">
                  {alertas.length > 9 ? '9+' : alertas.length}
                </span>
              )}
            </button>

            {bellOpen && (
              <div className="absolute right-0 mt-2 w-[360px] bg-card border border-border rounded-lg shadow-xl z-50 overflow-hidden">
                <div className="px-4 py-3 border-b border-border flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <AlertTriangle size={14} className="text-accent" />
                    <span
                      className="text-xs font-medium uppercase tracking-widest text-muted-foreground"
                      style={{ fontFamily: "'DM Mono', monospace" }}
                    >
                      Alertas próximas
                    </span>
                  </div>
                  <span className="text-xs text-muted-foreground" style={{ fontFamily: "'DM Mono', monospace" }}>
                    ≤ 15 días
                  </span>
                </div>

                <div className="divide-y divide-border max-h-80 overflow-y-auto">
                  {alertas.length === 0 ? (
                    <p className="px-4 py-6 text-center text-sm text-muted-foreground">
                      Sin alertas pendientes
                    </p>
                  ) : (
                    <>
                      {alertasPropias.length > 0 && (
                        <>
                          {esSocioPrincipal && (
                            <div className="px-4 py-2 bg-secondary/30 border-b border-border">
                              <span className="text-xs font-medium text-muted-foreground uppercase tracking-widest"
                                style={{ fontFamily: "'DM Mono', monospace" }}>
                                Mis expedientes
                              </span>
                            </div>
                          )}
                          {alertasPropias.map(alerta => (
                            <AlertaItem key={alerta.etapaHistorialId} alerta={alerta} onAtender={handleAtender} onNavegar={irAlExpediente} />
                          ))}
                        </>
                      )}

                      {esSocioPrincipal && alertasAjenas.length > 0 && (
                        <>
                          <div className="px-4 py-2 bg-secondary/30 border-b border-border border-t">
                            <span className="text-xs font-medium text-muted-foreground uppercase tracking-widest"
                              style={{ fontFamily: "'DM Mono', monospace" }}>
                              Otros litigantes
                            </span>
                          </div>
                          {Object.entries(gruposAjenos).map(([nombre, alertasUsuario]) => (
                            <div key={nombre}>
                              <div className="px-4 py-1.5 bg-accent/5 border-b border-border">
                                <span className="text-xs font-medium text-accent"
                                  style={{ fontFamily: "'DM Mono', monospace" }}>
                                  {nombre}
                                </span>
                              </div>
                              {alertasUsuario.map(alerta => (
                                <AlertaItem key={alerta.etapaHistorialId} alerta={alerta} onAtender={handleAtender} onNavegar={irAlExpediente} />
                              ))}
                            </div>
                          ))}
                        </>
                      )}
                    </>
                  )}
                </div>

                <div className="px-4 py-2.5 border-t border-border bg-secondary/30">
                  <span className="text-xs text-muted-foreground">
                    {alertas.length} vencimiento{alertas.length !== 1 ? 's' : ''} en los próximos 15 días
                  </span>
                </div>

                <div className="px-4 py-2.5 border-t border-border">
                  <button
                    onClick={() => { setBellOpen(false); navigate('/notificaciones') }}
                    className="w-full text-xs text-accent hover:underline text-center font-medium"
                  >
                    Ver todas las notificaciones →
                  </button>
                </div>
              </div>
            )}
          </div>

          <span className="h-5 w-px bg-white/15" />

          {/* User */}
          <button
            onClick={() => navigate('/perfil')}
            className="flex items-center gap-2.5 px-2 py-1 rounded hover:bg-white/10 transition"
          >
            <div className="w-7 h-7 rounded-full bg-accent/20 border border-accent/30 flex items-center justify-center shrink-0">
              <span className="text-accent text-xs font-semibold" style={{ fontFamily: "'DM Mono', monospace" }}>
                {iniciales(usuario?.nombre)}
              </span>
            </div>
            <div className="hidden sm:block leading-tight text-left">
              <p className="text-xs font-medium text-primary-foreground">{usuario?.nombre ?? 'Usuario'}</p>
              <p className="text-[10px] text-primary-foreground/45">
                {usuario?.id === 1 ? 'Socio Principal' : usuario?.rol ?? ''}
              </p>
            </div>
          </button>

          <button
            onClick={handleLogout}
            className="flex items-center gap-1.5 text-xs text-primary-foreground/50 hover:text-primary-foreground/90 transition px-2 py-1.5 rounded hover:bg-white/10"
            title="Cerrar sesión"
          >
            <LogOut size={14} />
            <span className="hidden sm:inline">Salir</span>
          </button>
        </div>
      </div>
    </header>
  )
}

export default Topbar