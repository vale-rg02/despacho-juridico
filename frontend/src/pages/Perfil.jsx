import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { User, Lock, ShieldCheck, Users, Download, Pencil, Plus, X, Check } from 'lucide-react'
import Topbar from '../components/Topbar'
import { getUsuario } from '../services/auth'
import api from '../services/api'

const SOCIO_PRINCIPAL_ID = 1

function Perfil() {
  const navigate = useNavigate()
  const usuario = getUsuario()
  const esSocioPrincipal = usuario?.id === SOCIO_PRINCIPAL_ID
  const puedeVerAdmin = usuario?.rol === 'Socio'

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

  // Estado gestión de usuarios
  const [usuarios, setUsuarios] = useState([])
  const [cargandoUsuarios, setCargandoUsuarios] = useState(false)
  const [modalUsuario, setModalUsuario] = useState(null) // null | 'crear' | {id, nombre, email, rol, activo}
  const [formUsuario, setFormUsuario] = useState({ nombre: '', email: '', password: '', rol: 'Litigante' })
  const [errorUsuario, setErrorUsuario] = useState('')
  const [msgUsuario, setMsgUsuario] = useState('')
  const [guardandoUsuario, setGuardandoUsuario] = useState(false)

  useEffect(() => {
    if (seccion === 'admin') cargarActividad()
    if (seccion === 'usuarios') cargarUsuarios()
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

  async function cargarUsuarios() {
    setCargandoUsuarios(true)
    try {
      const res = await api.get('/usuarios')
      setUsuarios(res.data)
    } catch {
      setUsuarios([])
    } finally {
      setCargandoUsuarios(false)
    }
  }

  async function handleCambiarPassword() {
    setMsgPassword('')
    setErrorPassword('')
    if (!nuevaPassword || !confirmarPassword) { setErrorPassword('Completa ambos campos'); return }
    if (nuevaPassword !== confirmarPassword) { setErrorPassword('Las contraseñas no coinciden'); return }
    setCargandoPassword(true)
    try {
      await api.put(`/usuarios/${usuario.id}/password`, { nuevaPassword, confirmarPassword })
      setMsgPassword('Contraseña actualizada correctamente')
      setNuevaPassword('')
      setConfirmarPassword('')
    } catch (err) {
      setErrorPassword(err.response?.data?.mensaje ?? 'Error al actualizar contraseña')
    } finally {
      setCargandoPassword(false)
    }
  }

  function abrirCrear() {
    setFormUsuario({ nombre: '', email: '', password: '', rol: 'Litigante' })
    setErrorUsuario('')
    setMsgUsuario('')
    setModalUsuario('crear')
  }

  function abrirEditar(u) {
    setFormUsuario({ nombre: u.nombre, email: u.email, password: '', rol: u.rol })
    setErrorUsuario('')
    setMsgUsuario('')
    setModalUsuario(u)
  }

  async function handleGuardarUsuario() {
    setErrorUsuario('')
    setGuardandoUsuario(true)
    try {
      if (modalUsuario === 'crear') {
        await api.post('/usuarios', {
          nombre: formUsuario.nombre,
          email: formUsuario.email,
          password: formUsuario.password,
          rol: formUsuario.rol === 'Socio' ? 1 : 0
        })
        setMsgUsuario('Usuario creado correctamente')
      } else {
        await api.put(`/usuarios/${modalUsuario.id}`, {
          nombre: formUsuario.nombre,
          email: formUsuario.email,
          rol: formUsuario.rol === 'Socio' ? 1 : 0
        })
        setMsgUsuario('Usuario actualizado correctamente')
      }
      await cargarUsuarios()
      setModalUsuario(null)
    } catch (err) {
      setErrorUsuario(err.response?.data?.mensaje ?? 'Error al guardar usuario')
    } finally {
      setGuardandoUsuario(false)
    }
  }

  async function handleToggleActivo(u) {
    try {
      await api.patch(`/usuarios/${u.id}/activo`, { activo: !u.activo })
      await cargarUsuarios()
    } catch {
      // silencioso
    }
  }

  async function handleDescargarExcel() {
    try {
      const res = await api.get('/export/expedientes', { responseType: 'blob' })
      const url = window.URL.createObjectURL(new Blob([res.data]))
      const a = document.createElement('a')
      a.href = url
      a.download = `expedientes_${new Date().toISOString().slice(0, 10)}.xlsx`
      a.click()
      window.URL.revokeObjectURL(url)
    } catch {
      alert('Error al generar el archivo')
    }
  }

  const monoStyle = { fontFamily: "'DM Mono', monospace" }
  const serifStyle = { fontFamily: "'Playfair Display', serif" }

  const btnSeccion = (id, Icon, label) => (
    <button
      onClick={() => setSeccion(id)}
      className={`w-full flex items-center gap-2.5 px-3 py-2 rounded-md text-sm transition ${seccion === id ? 'bg-accent/10 text-accent font-medium' : 'text-muted-foreground hover:text-foreground hover:bg-secondary/50'}`}
    >
      <Icon size={14} />
      {label}
    </button>
  )

  return (
    <div className="min-h-screen bg-background">
      <Topbar />
      <div className="max-w-screen-xl mx-auto px-6 py-8 flex gap-8">

        {/* Sidebar */}
        <aside className="w-52 shrink-0">
          <nav className="space-y-1">
            {btnSeccion('info', User, 'Mi información')}
            {btnSeccion('password', Lock, 'Cambiar contraseña')}
            {puedeVerAdmin && (
              <>
                <div className="h-px bg-border my-2" />
                {btnSeccion('admin', ShieldCheck, 'Panel admin')}
                {btnSeccion('usuarios', Users, 'Usuarios')}
                {btnSeccion('respaldo', Download, 'Respaldo')}
              </>
            )}
          </nav>
        </aside>

        {/* Contenido */}
        <main className="flex-1 min-w-0">

          {/* Mi información */}
          {seccion === 'info' && (
            <div className="bg-card border border-border rounded-lg p-6 space-y-4">
              <h2 className="text-lg font-medium text-foreground" style={serifStyle}>Mi información</h2>
              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <p className="text-muted-foreground text-xs uppercase tracking-widest mb-1" style={monoStyle}>Nombre</p>
                  <p className="text-foreground font-medium">{usuario?.nombre}</p>
                </div>
                <div>
                  <p className="text-muted-foreground text-xs uppercase tracking-widest mb-1" style={monoStyle}>Correo</p>
                  <p className="text-foreground">{usuario?.email}</p>
                </div>
                <div>
                  <p className="text-muted-foreground text-xs uppercase tracking-widest mb-1" style={monoStyle}>Rol</p>
                  <p className="text-foreground">{esSocioPrincipal ? 'Socio Principal' : usuario?.rol}</p>
                </div>
              </div>
            </div>
          )}

          {/* Cambiar contraseña */}
          {seccion === 'password' && (
            <div className="bg-card border border-border rounded-lg p-6 space-y-4 max-w-md">
              <h2 className="text-lg font-medium text-foreground" style={serifStyle}>Cambiar contraseña</h2>
              <div className="space-y-3">
                <div>
                  <label className="text-xs text-muted-foreground uppercase tracking-widest block mb-1" style={monoStyle}>Nueva contraseña</label>
                  <input type="password" value={nuevaPassword} onChange={e => setNuevaPassword(e.target.value)}
                    className="w-full border border-border rounded-md px-3 py-2 text-sm bg-background focus:outline-none focus:ring-1 focus:ring-accent/50" />
                </div>
                <div>
                  <label className="text-xs text-muted-foreground uppercase tracking-widest block mb-1" style={monoStyle}>Confirmar contraseña</label>
                  <input type="password" value={confirmarPassword} onChange={e => setConfirmarPassword(e.target.value)}
                    className="w-full border border-border rounded-md px-3 py-2 text-sm bg-background focus:outline-none focus:ring-1 focus:ring-accent/50" />
                </div>
                {errorPassword && <p className="text-red-500 text-xs">{errorPassword}</p>}
                {msgPassword && <p className="text-emerald-600 text-xs">{msgPassword}</p>}
                <button onClick={handleCambiarPassword} disabled={cargandoPassword}
                  className="bg-accent text-white text-sm px-4 py-2 rounded-md hover:bg-accent/90 transition disabled:opacity-50">
                  {cargandoPassword ? 'Guardando...' : 'Guardar contraseña'}
                </button>
              </div>
            </div>
          )}

          {/* Panel admin */}
          {seccion === 'admin' && puedeVerAdmin && (
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <h2 className="text-lg font-medium text-foreground" style={serifStyle}>Panel de actividad</h2>
                <div className="flex gap-2">
                  {['dia', 'semana', 'mes'].map(p => (
                    <button key={p} onClick={() => setPeriodo(p)}
                      className={`text-xs px-3 py-1.5 rounded-md transition capitalize ${periodo === p ? 'bg-accent text-white' : 'bg-secondary text-muted-foreground hover:text-foreground'}`}>
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
                      <span className="text-xs text-muted-foreground" style={monoStyle}>{grupo.totalAcciones} acción{grupo.totalAcciones !== 1 ? 'es' : ''}</span>
                    </div>
                    <table className="w-full text-xs border-collapse">
                      <thead>
                        <tr className="bg-secondary/40 border-b border-border">
                          {['Acción', 'Expediente', 'Detalle', 'Fecha'].map(h => (
                            <th key={h} className="text-left px-4 py-2 text-muted-foreground font-medium uppercase tracking-wider" style={monoStyle}>{h}</th>
                          ))}
                        </tr>
                      </thead>
                      <tbody>
                        {grupo.acciones.map(a => (
                          <tr key={a.id} className="border-b border-border last:border-0 hover:bg-secondary/20">
                            <td className="px-4 py-2.5 capitalize text-foreground">{a.accion.replace('_', ' ')}</td>
                            <td className="px-4 py-2.5 text-muted-foreground" style={monoStyle}>{a.numeroExpediente}</td>
                            <td className="px-4 py-2.5 text-muted-foreground max-w-xs truncate">{a.detalle}</td>
                            <td className="px-4 py-2.5 text-muted-foreground whitespace-nowrap" style={monoStyle}>
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

          {/* Gestión de usuarios */}
          {seccion === 'usuarios' && puedeVerAdmin && (
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <h2 className="text-lg font-medium text-foreground" style={serifStyle}>Gestión de usuarios</h2>
                <button onClick={abrirCrear}
                  className="flex items-center gap-1.5 text-xs bg-accent text-white px-3 py-2 rounded-md hover:bg-accent/90 transition">
                  <Plus size={13} /> Nuevo usuario
                </button>
              </div>
              {cargandoUsuarios ? (
                <p className="text-sm text-muted-foreground">Cargando usuarios...</p>
              ) : (
                <div className="bg-card border border-border rounded-lg overflow-hidden">
                  <table className="w-full text-sm border-collapse">
                    <thead>
                      <tr className="bg-secondary/40 border-b border-border">
                        {['Nombre', 'Correo', 'Rol', 'Estado', ''].map(h => (
                          <th key={h} className="text-left px-4 py-2.5 text-xs text-muted-foreground font-medium uppercase tracking-wider" style={monoStyle}>{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {usuarios.map(u => (
                        <tr key={u.id} className="border-b border-border last:border-0 hover:bg-secondary/20">
                          <td className="px-4 py-3 text-foreground font-medium">{u.nombre}</td>
                          <td className="px-4 py-3 text-muted-foreground text-xs" style={monoStyle}>{u.email}</td>
                          <td className="px-4 py-3 text-muted-foreground text-xs">{u.id === SOCIO_PRINCIPAL_ID ? 'Socio Principal' : u.rol}</td>
                          <td className="px-4 py-3">
                            <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${u.activo ? 'bg-emerald-50 text-emerald-700' : 'bg-stone-100 text-stone-500'}`}>
                              {u.activo ? 'Activo' : 'Inactivo'}
                            </span>
                          </td>
                          <td className="px-4 py-3">
                            <div className="flex items-center gap-2 justify-end">
                              {u.id !== SOCIO_PRINCIPAL_ID && (
                                <>
                                  <button onClick={() => abrirEditar(u)}
                                    className="text-muted-foreground hover:text-foreground transition" title="Editar">
                                    <Pencil size={13} />
                                  </button>
                                  <button onClick={() => handleToggleActivo(u)}
                                    className={`transition text-xs ${u.activo ? 'text-red-400 hover:text-red-600' : 'text-emerald-500 hover:text-emerald-700'}`}
                                    title={u.activo ? 'Desactivar' : 'Activar'}>
                                    {u.activo ? <X size={13} /> : <Check size={13} />}
                                  </button>
                                </>
                              )}
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {/* Modal crear/editar */}
              {modalUsuario && (
                <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center">
                  <div className="bg-card border border-border rounded-lg p-6 w-full max-w-md shadow-xl space-y-4">
                    <h3 className="text-base font-medium text-foreground" style={serifStyle}>
                      {modalUsuario === 'crear' ? 'Nuevo usuario' : 'Editar usuario'}
                    </h3>
                    <div className="space-y-3">
                      <div>
                        <label className="text-xs text-muted-foreground uppercase tracking-widest block mb-1" style={monoStyle}>Nombre</label>
                        <input value={formUsuario.nombre} onChange={e => setFormUsuario(f => ({ ...f, nombre: e.target.value }))}
                          className="w-full border border-border rounded-md px-3 py-2 text-sm bg-background focus:outline-none focus:ring-1 focus:ring-accent/50" />
                      </div>
                      <div>
                        <label className="text-xs text-muted-foreground uppercase tracking-widest block mb-1" style={monoStyle}>Correo</label>
                        <input value={formUsuario.email} onChange={e => setFormUsuario(f => ({ ...f, email: e.target.value }))}
                          className="w-full border border-border rounded-md px-3 py-2 text-sm bg-background focus:outline-none focus:ring-1 focus:ring-accent/50" />
                      </div>
                      {modalUsuario === 'crear' && (
                        <div>
                          <label className="text-xs text-muted-foreground uppercase tracking-widest block mb-1" style={monoStyle}>Contraseña inicial</label>
                          <input type="password" value={formUsuario.password} onChange={e => setFormUsuario(f => ({ ...f, password: e.target.value }))}
                            className="w-full border border-border rounded-md px-3 py-2 text-sm bg-background focus:outline-none focus:ring-1 focus:ring-accent/50" />
                        </div>
                      )}
                      <div>
                        <label className="text-xs text-muted-foreground uppercase tracking-widest block mb-1" style={monoStyle}>Rol</label>
                        <select value={formUsuario.rol} onChange={e => setFormUsuario(f => ({ ...f, rol: e.target.value }))}
                          className="w-full border border-border rounded-md px-3 py-2 text-sm bg-background focus:outline-none focus:ring-1 focus:ring-accent/50">
                          <option value="Litigante">Litigante</option>
                          <option value="Socio">Socio</option>
                        </select>
                      </div>
                      {errorUsuario && <p className="text-red-500 text-xs">{errorUsuario}</p>}
                    </div>
                    <div className="flex justify-end gap-2 pt-2">
                      <button onClick={() => setModalUsuario(null)}
                        className="text-sm text-muted-foreground hover:text-foreground px-3 py-2 rounded-md hover:bg-secondary/50 transition">
                        Cancelar
                      </button>
                      <button onClick={handleGuardarUsuario} disabled={guardandoUsuario}
                        className="bg-accent text-white text-sm px-4 py-2 rounded-md hover:bg-accent/90 transition disabled:opacity-50">
                        {guardandoUsuario ? 'Guardando...' : 'Guardar'}
                      </button>
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Respaldo */}
          {seccion === 'respaldo' && puedeVerAdmin && (
            <div className="space-y-4">
              <h2 className="text-lg font-medium text-foreground" style={serifStyle}>Respaldo de datos</h2>

              <div className="bg-card border border-border rounded-lg p-6 space-y-3">
                <h3 className="text-sm font-medium text-foreground">Exportar expedientes</h3>
                <p className="text-xs text-muted-foreground">Descarga un archivo Excel con todos los expedientes registrados en el sistema.</p>
                <button onClick={handleDescargarExcel}
                  className="flex items-center gap-2 text-sm bg-accent text-white px-4 py-2 rounded-md hover:bg-accent/90 transition">
                  <Download size={14} />
                  Descargar Excel
                </button>
              </div>

              <div className="bg-card border border-border rounded-lg p-6 space-y-3">
                <h3 className="text-sm font-medium text-foreground">Respaldo completo de base de datos</h3>
                <p className="text-xs text-muted-foreground">
                  Para un respaldo completo de la base de datos, accede al panel de Railway → servicio Postgres → pestaña <span className="font-medium text-foreground">Backups</span>, donde puedes descargar un respaldo completo en cualquier momento.
                </p>
                <p className="text-xs text-muted-foreground">
                  También puedes hacer un dump manual desde la terminal con las credenciales de conexión disponibles en Railway.
                </p>
              </div>
            </div>
          )}

        </main>
      </div>
    </div>
  )
}

export default Perfil