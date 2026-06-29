import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { User, Lock, ShieldCheck, Clock } from 'lucide-react'
import Topbar from '../components/Topbar'
import { getUsuario } from '../services/auth'
import api from '../services/api'

const SOCIO_PRINCIPAL_ID = 1

function Perfil() {
  const navigate = useNavigate()
  const usuario = getUsuario()
  const esSocioPrincipal = usuario?.id === SOCIO_PRINCIPAL_ID

  const [seccion, setSeccion] = useState('info')

  // Estado cambio de contraseña
  const [nuevaPassword, setNuevaPassword] = useState('')
  const [confirmarPassword, setConfirmarPassword] = useState('')
  const [msgPassword, setMsgPassword] = useState('')
  const [errorPassword, setErrorPassword] = useState('')
  const [cargandoPassword, setCargandoPassword] = useState(false)

  // Estado panel admin
  const [periodo, setPeriodo] = useState('semana')
  const [actividad, setActividad] = useState(null)
  const [cargandoAdmin, setCargandoAdmin] = useState(false)

  useEffect(() => {
    if (seccion === 'admin') cargarActividad()
  }, [seccion, periodo])

  async function cargarActividad() {
    setCargandoAdmin(true)
    try {
      const res = await api.get(`/admin/actividad?periodo=${periodo}`)
      setActividad(res.data)
    } catch {
      setActividad(null)
    } finally {
      setCargandoAdmin(false)
    }
  }

  async function handleCambiarPassword() {
    setMsgPassword('')
    setErrorPassword('')
    if (!nuevaPassword || !confirmarPassword) {
      setErrorPassword('Completa ambos campos')
      return
    }
    if (nuevaPassword !== confirmarPassword) {
      setErrorPassword('Las contraseñas no coinciden')
      return
    }
    setCargandoPassword(true)
    try {
      await api.put(`/usuarios/${usuario.id}/password`, {
        nuevaPassword,
        confirmarPassword
      })
      setMsgPassword('Contraseña actualizada correctamente')
      setNuevaPassword('')
      setConfirmarPassword('')
    } catch (err) {
      setErrorPassword(err.response?.data?.mensaje ?? 'Error al actualizar contraseña')
    } finally {
      setCargandoPassword(false)
    }
  }

  return (
    <div className="min-h-screen bg-background">
      <Topbar />
      <div className="max-w-screen-xl mx-auto px-6 py-8 flex gap-8">

        {/* Sidebar */}
        <aside className="w-52 shrink-0">
          <nav className="space-y-1">
            <button
              onClick={() => setSeccion('info')}
              className={`w-full flex items-center gap-2.5 px-3 py-2 rounded-md text-sm transition ${seccion === 'info' ? 'bg-accent/10 text-accent font-medium' : 'text-muted-foreground hover:text-foreground hover:bg-secondary/50'}`}
            >
              <User size={14} />
              Mi información
            </button>
            <button
              onClick={() => setSeccion('password')}
              className={`w-full flex items-center gap-2.5 px-3 py-2 rounded-md text-sm transition ${seccion === 'password' ? 'bg-accent/10 text-accent font-medium' : 'text-muted-foreground hover:text-foreground hover:bg-secondary/50'}`}
            >
              <Lock size={14} />
              Cambiar contraseña
            </button>

            {esSocioPrincipal && (
              <>
                <div className="h-px bg-border my-2" />
                <button
                  onClick={() => setSeccion('admin')}
                  className={`w-full flex items-center gap-2.5 px-3 py-2 rounded-md text-sm transition ${seccion === 'admin' ? 'bg-accent/10 text-accent font-medium' : 'text-muted-foreground hover:text-foreground hover:bg-secondary/50'}`}
                >
                  <ShieldCheck size={14} />
                  Panel admin
                </button>
              </>
            )}
          </nav>
        </aside>

        {/* Contenido */}
        <main className="flex-1 min-w-0">

          {seccion === 'info' && (
            <div className="bg-card border border-border rounded-lg p-6 space-y-4">
              <h2 className="text-lg font-medium text-foreground" style={{ fontFamily: "'Playfair Display', serif" }}>
                Mi información
              </h2>
              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <p className="text-muted-foreground text-xs uppercase tracking-widest mb-1" style={{ fontFamily: "'DM Mono', monospace" }}>Nombre</p>
                  <p className="text-foreground font-medium">{usuario?.nombre}</p>
                </div>
                <div>
                  <p className="text-muted-foreground text-xs uppercase tracking-widest mb-1" style={{ fontFamily: "'DM Mono', monospace" }}>Correo</p>
                  <p className="text-foreground">{usuario?.email}</p>
                </div>
                <div>
                  <p className="text-muted-foreground text-xs uppercase tracking-widest mb-1" style={{ fontFamily: "'DM Mono', monospace" }}>Rol</p>
                  <p className="text-foreground">{esSocioPrincipal ? 'Socio Principal' : usuario?.rol}</p>
                </div>
              </div>
            </div>
          )}

          {seccion === 'password' && (
            <div className="bg-card border border-border rounded-lg p-6 space-y-4 max-w-md">
              <h2 className="text-lg font-medium text-foreground" style={{ fontFamily: "'Playfair Display', serif" }}>
                Cambiar contraseña
              </h2>
              <div className="space-y-3">
                <div>
                  <label className="text-xs text-muted-foreground uppercase tracking-widest block mb-1" style={{ fontFamily: "'DM Mono', monospace" }}>
                    Nueva contraseña
                  </label>
                  <input
                    type="password"
                    value={nuevaPassword}
                    onChange={e => setNuevaPassword(e.target.value)}
                    className="w-full border border-border rounded-md px-3 py-2 text-sm bg-background focus:outline-none focus:ring-1 focus:ring-accent/50"
                  />
                </div>
                <div>
                  <label className="text-xs text-muted-foreground uppercase tracking-widest block mb-1" style={{ fontFamily: "'DM Mono', monospace" }}>
                    Confirmar contraseña
                  </label>
                  <input
                    type="password"
                    value={confirmarPassword}
                    onChange={e => setConfirmarPassword(e.target.value)}
                    className="w-full border border-border rounded-md px-3 py-2 text-sm bg-background focus:outline-none focus:ring-1 focus:ring-accent/50"
                  />
                </div>
                {errorPassword && <p className="text-red-500 text-xs">{errorPassword}</p>}
                {msgPassword && <p className="text-emerald-600 text-xs">{msgPassword}</p>}
                <button
                  onClick={handleCambiarPassword}
                  disabled={cargandoPassword}
                  className="bg-accent text-white text-sm px-4 py-2 rounded-md hover:bg-accent/90 transition disabled:opacity-50"
                >
                  {cargandoPassword ? 'Guardando...' : 'Guardar contraseña'}
                </button>
              </div>
            </div>
          )}

          {seccion === 'admin' && esSocioPrincipal && (
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <h2 className="text-lg font-medium text-foreground" style={{ fontFamily: "'Playfair Display', serif" }}>
                  Panel de actividad
                </h2>
                <div className="flex gap-2">
                  {['dia', 'semana', 'mes'].map(p => (
                    <button
                      key={p}
                      onClick={() => setPeriodo(p)}
                      className={`text-xs px-3 py-1.5 rounded-md transition capitalize ${periodo === p ? 'bg-accent text-white' : 'bg-secondary text-muted-foreground hover:text-foreground'}`}
                    >
                      {p === 'dia' ? 'Hoy' : p === 'semana' ? 'Esta semana' : 'Este mes'}
                    </button>
                  ))}
                </div>
              </div>

              {cargandoAdmin ? (
                <p className="text-sm text-muted-foreground">Cargando actividad...</p>
              ) : actividad?.resumenPorUsuario?.length === 0 ? (
                <p className="text-sm text-muted-foreground">Sin actividad en este período.</p>
              ) : (
                actividad?.resumenPorUsuario?.map(grupo => (
                  <div key={grupo.usuario} className="bg-card border border-border rounded-lg overflow-hidden">
                    <div className="px-4 py-3 border-b border-border flex items-center justify-between">
                      <span className="text-sm font-medium text-foreground">{grupo.usuario}</span>
                      <span className="text-xs text-muted-foreground" style={{ fontFamily: "'DM Mono', monospace" }}>
                        {grupo.totalAcciones} acción{grupo.totalAcciones !== 1 ? 'es' : ''}
                      </span>
                    </div>
                    <table className="w-full text-xs border-collapse">
                      <thead>
                        <tr className="bg-secondary/40 border-b border-border">
                          <th className="text-left px-4 py-2 text-muted-foreground font-medium uppercase tracking-wider" style={{ fontFamily: "'DM Mono', monospace" }}>Acción</th>
                          <th className="text-left px-4 py-2 text-muted-foreground font-medium uppercase tracking-wider" style={{ fontFamily: "'DM Mono', monospace" }}>Expediente</th>
                          <th className="text-left px-4 py-2 text-muted-foreground font-medium uppercase tracking-wider" style={{ fontFamily: "'DM Mono', monospace" }}>Detalle</th>
                          <th className="text-left px-4 py-2 text-muted-foreground font-medium uppercase tracking-wider" style={{ fontFamily: "'DM Mono', monospace" }}>Fecha</th>
                        </tr>
                      </thead>
                      <tbody>
                        {grupo.acciones.map(a => (
                          <tr key={a.id} className="border-b border-border last:border-0 hover:bg-secondary/20">
                            <td className="px-4 py-2.5 capitalize text-foreground">{a.accion.replace('_', ' ')}</td>
                            <td className="px-4 py-2.5 text-muted-foreground" style={{ fontFamily: "'DM Mono', monospace" }}>{a.numeroExpediente}</td>
                            <td className="px-4 py-2.5 text-muted-foreground max-w-xs truncate">{a.detalle}</td>
                            <td className="px-4 py-2.5 text-muted-foreground whitespace-nowrap" style={{ fontFamily: "'DM Mono', monospace" }}>
                              {new Date(a.fecha).toLocaleString('es-MX', { dateStyle: 'short', timeStyle: 'short' })}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ))
              )}
            </div>
          )}

        </main>
      </div>
    </div>
  )
}

export default Perfil