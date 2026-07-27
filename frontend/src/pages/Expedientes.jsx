import { useState, useEffect, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, ChevronDown, Filter, X, ArrowUpDown, ArrowUp, ArrowDown, ChevronRight, UserPlus } from 'lucide-react'
import Topbar from '../components/Topbar'
import EstadoVacio from '../components/EstadoVacio'
import { getExpedientes, getExpedientesPorUsuario } from '../services/expedientes'
import { getExpedientesConAcuerdosNoVistos } from '../services/acuerdos'
import { agregarAcceso } from '../services/accesos'
import { getUsuario } from '../services/auth'
import api from '../services/api'

const estadoConfig = {
  Abierto: { bg: 'bg-secondary', text: 'text-foreground', dot: 'bg-emerald-500' },
  Pausado: { bg: 'bg-secondary', text: 'text-foreground', dot: 'bg-amber-400' },
  Cerrado: { bg: 'bg-secondary', text: 'text-muted-foreground', dot: 'bg-stone-400' },
}

const prioridadConfig = {
  Urgente:     { color: 'text-red-700',          border: 'border-red-300',    bg: 'bg-red-50',    symbol: '▲' },
  Prioritario: { color: 'text-amber-700',        border: 'border-amber-300',  bg: 'bg-amber-50',  symbol: '●' },
  Normal:      { color: 'text-muted-foreground', border: 'border-border',     bg: 'bg-secondary', symbol: '▼' },
}

const COLUMNAS = [
  { key: 'numeroExpediente', label: 'Número' },
  { key: 'parteDemandada',   label: 'Parte Demandada' },
  { key: 'juzgado',          label: 'Juzgado' },
  { key: 'materia',          label: 'Materia' },
  { key: 'estado',           label: 'Estado' },
  { key: 'prioridad',        label: 'Prioridad' },
]

function TablaExpedientes({ expedientes, cargando, onRowClick, sortKey, sortDir, onSort, expedientesConAcuerdosNuevos, seleccionados, onToggleSeleccion, onToggleSeleccionarTodos }) {
  function SortIcon({ col }) {
    if (sortKey !== col) return <ArrowUpDown size={13} className="opacity-30 ml-1 inline" />
    return sortDir === 'asc'
      ? <ArrowUp size={13} className="ml-1 inline text-accent" />
      : <ArrowDown size={13} className="ml-1 inline text-accent" />
  }

  if (cargando) {
    return (
      <div className="text-center py-10 text-muted-foreground text-sm">Cargando expedientes...</div>
    )
  }

  if (expedientes.length === 0) {
    return (
      <EstadoVacio
        icon={Search}
        titulo="No se encontraron expedientes"
        subtitulo="Ajusta la búsqueda o los filtros para ver más resultados."
      />
    )
  }

  const todosSeleccionados = expedientes.every(e => seleccionados.has(e.id))

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm border-collapse">
        <thead>
          <tr className="border-b border-border bg-secondary/60">
            <th className="px-4 py-3 w-8">
              <input
                type="checkbox"
                checked={todosSeleccionados}
                onChange={() => onToggleSeleccionarTodos(expedientes.map(e => e.id))}
                className="accent-accent cursor-pointer"
              />
            </th>
            {COLUMNAS.map(col => (
              <th
                key={col.key}
                onClick={() => onSort(col.key)}
                className="text-left px-4 py-3 text-xs font-medium text-muted-foreground uppercase tracking-wider cursor-pointer select-none hover:text-foreground transition whitespace-nowrap"
                style={{ fontFamily: "'DM Mono', monospace" }}
              >
                {col.label}
                <SortIcon col={col.key} />
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {expedientes.map((exp, i) => (
            <tr
              key={exp.id}
              onClick={() => onRowClick(exp.id)}
              className={`border-b border-border last:border-0 hover:bg-accent/5 hover:shadow-[inset_2px_0_0_var(--accent)] hover:cursor-pointer transition-colors group ${i % 2 === 0 ? '' : 'bg-secondary/20'}`}
            >
              <td className="px-4 py-3.5" onClick={e => e.stopPropagation()}>
                <input
                  type="checkbox"
                  checked={seleccionados.has(exp.id)}
                  onChange={() => onToggleSeleccion(exp.id)}
                  className="accent-accent cursor-pointer"
                />
              </td>
              <td className="px-4 py-3.5 whitespace-nowrap">
                <span className="inline-flex items-center gap-1.5">
                  {expedientesConAcuerdosNuevos?.has(exp.id) && (
                    <span
                      className="w-1.5 h-1.5 rounded-full bg-accent shrink-0"
                      title="Acuerdos nuevos sin ver"
                    />
                  )}
                  <span className="text-accent font-medium group-hover:underline" style={{ fontFamily: "'DM Mono', monospace", fontSize: '0.8rem' }}>
                    {exp.numeroExpediente}
                  </span>
                </span>
              </td>
              <td className="px-4 py-3.5">
                <span className="text-foreground font-medium">{exp.parteDemandada}</span>
              </td>
              <td className="px-4 py-3.5 text-muted-foreground whitespace-nowrap">{exp.juzgado ?? '—'}</td>
              <td className="px-4 py-3.5 text-foreground whitespace-nowrap">{exp.materia ?? '—'}</td>
              <td className="px-4 py-3.5 whitespace-nowrap">
                <span className={`inline-flex items-center gap-1.5 pl-2 pr-2.5 py-0.5 rounded-full text-xs font-medium border border-border ${estadoConfig[exp.estado]?.bg} ${estadoConfig[exp.estado]?.text}`}
                  style={{ fontFamily: "'DM Mono', monospace" }}>
                  <span className={`w-1.5 h-1.5 rounded-full shrink-0 ${estadoConfig[exp.estado]?.dot}`} />
                  {exp.estado}
                </span>
              </td>
              <td className="px-4 py-3.5 whitespace-nowrap">
                <span className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium border ${prioridadConfig[exp.prioridad]?.color} ${prioridadConfig[exp.prioridad]?.border} ${prioridadConfig[exp.prioridad]?.bg}`}
                  style={{ fontFamily: "'DM Mono', monospace" }}>
                  <span>{prioridadConfig[exp.prioridad]?.symbol}</span>
                  {exp.prioridad}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function BloqueExpandible({ titulo, expedientes, cargando, onRowClick, sortKey, sortDir, onSort, defaultAbierto = true, expedientesConAcuerdosNuevos, seleccionados, onToggleSeleccion, onToggleSeleccionarTodos }) {
  const [abierto, setAbierto] = useState(defaultAbierto)

  return (
    <div className="bg-card border border-border rounded-lg overflow-hidden shadow-sm mb-4">
      <button
        onClick={() => setAbierto(a => !a)}
        className="w-full flex items-center justify-between px-5 py-4 hover:bg-secondary/30 transition"
      >
        <div className="flex items-center gap-3">
          <ChevronRight
            size={16}
            className={`text-accent transition-transform ${abierto ? 'rotate-90' : ''}`}
          />
          <span className="text-sm font-medium text-foreground" style={{ fontFamily: "'Playfair Display', serif" }}>
            {titulo}
          </span>
        </div>
        <span className="text-xs text-muted-foreground" style={{ fontFamily: "'DM Mono', monospace" }}>
          {expedientes.length} expediente{expedientes.length !== 1 ? 's' : ''}
        </span>
      </button>
      {abierto && (
        <div className="border-t border-border">
          <TablaExpedientes
            expedientes={expedientes}
            cargando={cargando}
            onRowClick={onRowClick}
            sortKey={sortKey}
            sortDir={sortDir}
            onSort={onSort}
            expedientesConAcuerdosNuevos={expedientesConAcuerdosNuevos}
            seleccionados={seleccionados}
            onToggleSeleccion={onToggleSeleccion}
            onToggleSeleccionarTodos={onToggleSeleccionarTodos}
          />
        </div>
      )}
    </div>
  )
}

function ModalAgregarColaboradorMasivo({ expedienteIds, usuarios, onCerrar, onCompletado }) {
  const [usuarioId, setUsuarioId] = useState('')
  const [guardando, setGuardando] = useState(false)

  async function handleAgregar() {
    if (!usuarioId) return
    setGuardando(true)
    const resultados = await Promise.allSettled(
      expedienteIds.map(id => agregarAcceso(id, Number(usuarioId)))
    )
    const exitosos = resultados.filter(r => r.status === 'fulfilled').length
    setGuardando(false)
    onCompletado(exitosos, resultados.length - exitosos)
  }

  return (
    <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center" onClick={onCerrar}>
      <div className="bg-card border border-border rounded-lg p-6 w-full max-w-sm" onClick={e => e.stopPropagation()}>
        <h3 className="text-sm font-medium text-foreground mb-1" style={{ fontFamily: "'Playfair Display', serif" }}>
          Agregar colaborador
        </h3>
        <p className="text-xs text-muted-foreground mb-4">
          Se agregará a {expedienteIds.length} expediente{expedienteIds.length !== 1 ? 's' : ''} seleccionado{expedienteIds.length !== 1 ? 's' : ''}.
        </p>
        <select
          value={usuarioId}
          onChange={e => setUsuarioId(e.target.value)}
          className="w-full bg-input-background text-foreground text-sm px-3 py-1.5 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition mb-4"
        >
          <option value="">Selecciona un usuario…</option>
          {usuarios.map(u => (
            <option key={u.id} value={u.id}>{u.nombre}</option>
          ))}
        </select>
        <div className="flex justify-end gap-2">
          <button
            onClick={onCerrar}
            className="px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground transition"
          >
            Cancelar
          </button>
          <button
            onClick={handleAgregar}
            disabled={!usuarioId || guardando}
            className="bg-accent text-accent-foreground px-4 py-1.5 rounded text-sm font-medium hover:opacity-90 transition disabled:opacity-50"
          >
            {guardando ? 'Agregando...' : 'Agregar'}
          </button>
        </div>
      </div>
    </div>
  )
}

function Expedientes() {
  const navigate = useNavigate()
  const usuario = getUsuario()
  const esSocioPrincipal = usuario?.id === 1

  const [busqueda, setBusqueda] = useState('')
  const [filtroUsuarioId, setFiltroUsuarioId] = useState(esSocioPrincipal ? 0 : (usuario?.id ?? null))
  const [dropdownUsuarioOpen, setDropdownUsuarioOpen] = useState(false)
  const [sortKey, setSortKey] = useState('numeroExpediente')
  const [sortDir, setSortDir] = useState('asc')
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  const [expedientesActivos, setExpedientesActivos] = useState([])
  const [expedientesMuertos, setExpedientesMuertos] = useState([])
  const [expedientesPorUsuario, setExpedientesPorUsuario] = useState([])

  const [usuarios, setUsuarios] = useState([])
  const [expedientesConAcuerdosNuevos, setExpedientesConAcuerdosNuevos] = useState(new Set())

  const [seleccionados, setSeleccionados] = useState(new Set())
  const [mostrarModalColaborador, setMostrarModalColaborador] = useState(false)
  const [exito, setExito] = useState('')

  useEffect(() => {
    cargarUsuarios()
    cargarAcuerdosNoVistos()
  }, [])

  async function cargarAcuerdosNoVistos() {
    const ids = await getExpedientesConAcuerdosNoVistos()
    setExpedientesConAcuerdosNuevos(new Set(ids))
  }

  useEffect(() => {
    const timeout = setTimeout(() => cargarExpedientes(), 400)
    return () => clearTimeout(timeout)
  }, [busqueda, filtroUsuarioId])

  async function cargarUsuarios() {
    try {
      const res = await api.get('/usuarios?excluirSoporte=true')
      // Para no-admins el backend ya regresa solo Id/Nombre (sin "activo") y ya
      // filtrados a usuarios activos; para admins sí trae el campo y lo filtramos aquí
      setUsuarios(res.data.filter(u => u.activo !== false))
    } catch {
      // silencioso
    }
  }

  async function cargarExpedientes() {
    setCargando(true)
    setError('')
    setSeleccionados(new Set())
    try {
      if (filtroUsuarioId === 0) {
        const [porUsuario, cerrados] = await Promise.all([
          getExpedientesPorUsuario(busqueda),
          getExpedientes({ estado: 'Cerrado', busqueda, usuarioId: 0 }),
        ])
        setExpedientesPorUsuario(porUsuario)
        setExpedientesActivos([])
        setExpedientesMuertos(cerrados)
      } else {
        const [abiertos, pausados, cerrados] = await Promise.all([
          getExpedientes({ estado: 'Abierto', busqueda, usuarioId: filtroUsuarioId }),
          getExpedientes({ estado: 'Pausado', busqueda, usuarioId: filtroUsuarioId }),
          getExpedientes({ estado: 'Cerrado', busqueda, usuarioId: filtroUsuarioId }),
        ])
        setExpedientesActivos([...abiertos, ...pausados])
        setExpedientesMuertos(cerrados)
        setExpedientesPorUsuario([])
      }
    } catch {
      setError('No se pudieron cargar los expedientes')
    } finally {
      setCargando(false)
    }
  }

  function handleSort(key) {
    if (sortKey === key) {
      setSortDir(d => (d === 'asc' ? 'desc' : 'asc'))
    } else {
      setSortKey(key)
      setSortDir('asc')
    }
  }

  function ordenar(lista) {
    const list = [...lista]
    list.sort((a, b) => {
      const av = (a[sortKey] ?? '').toString()
      const bv = (b[sortKey] ?? '').toString()
      return sortDir === 'asc' ? av.localeCompare(bv, 'es') : bv.localeCompare(av, 'es')
    })
    return list
  }

  function handleToggleSeleccion(id) {
    setSeleccionados(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  function handleToggleSeleccionarTodos(ids) {
    setSeleccionados(prev => {
      const todosSeleccionados = ids.length > 0 && ids.every(id => prev.has(id))
      const next = new Set(prev)
      ids.forEach(id => todosSeleccionados ? next.delete(id) : next.add(id))
      return next
    })
  }

  function handleColaboradorMasivoCompletado(exitosos, fallidos) {
    setMostrarModalColaborador(false)
    setSeleccionados(new Set())
    if (fallidos === 0) {
      setExito(`Colaborador agregado a ${exitosos} expediente${exitosos !== 1 ? 's' : ''}`)
    } else {
      setExito(`Colaborador agregado a ${exitosos} expediente${exitosos !== 1 ? 's' : ''}; ${fallidos} no se pudieron actualizar`)
    }
    setTimeout(() => setExito(''), 4000)
  }

  const nombreFiltroUsuario = useMemo(() => {
    if (filtroUsuarioId === 0) return 'Todos los usuarios'
    if (filtroUsuarioId === usuario?.id) return 'Mis expedientes'
    const u = usuarios.find(u => u.id === filtroUsuarioId)
    return u ? `Expedientes de ${u.nombre}` : 'Todos los usuarios'
  }, [filtroUsuarioId, usuarios])

  const tituloBloque = useMemo(() => {
    if (filtroUsuarioId === 0) return null
    if (filtroUsuarioId === usuario?.id) return 'Mis expedientes'
    const u = usuarios.find(u => u.id === filtroUsuarioId)
    return u ? `Expedientes de ${u.nombre}` : 'Expedientes'
  }, [filtroUsuarioId, usuarios])

  // Al ver los expedientes propios, separamos los que son como Titular de
  // aquellos donde solo se es Colaborador (el backend los mezcla en la misma lista)
  const esVistaPropia = filtroUsuarioId !== 0 && filtroUsuarioId === usuario?.id
  const activosTitular = esVistaPropia ? expedientesActivos.filter(e => !e.esColaborador) : expedientesActivos
  const activosColaborador = esVistaPropia ? expedientesActivos.filter(e => e.esColaborador) : []
  const muertosTitular = esVistaPropia ? expedientesMuertos.filter(e => !e.esColaborador) : expedientesMuertos
  const muertosColaborador = esVistaPropia ? expedientesMuertos.filter(e => e.esColaborador) : []

  return (
    <div className="min-h-screen bg-background">
      <Topbar />

      {/* Search / filter bar */}
      <div className="bg-card border-b border-border sticky top-14 z-30">
        <div className="max-w-screen-xl mx-auto px-6 h-12 flex items-center gap-4">
          <div className="flex-1 max-w-md relative">
            <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground pointer-events-none" />
            <input
              type="text"
              placeholder="Buscar por número o parte demandada…"
              value={busqueda}
              onChange={e => setBusqueda(e.target.value)}
              className="w-full bg-input-background text-foreground placeholder:text-muted-foreground text-sm pl-9 pr-8 py-1.5 rounded focus:outline-none focus:ring-1 focus:ring-accent/50 transition"
            />
            {busqueda && (
              <button onClick={() => setBusqueda('')} className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground transition">
                <X size={12} />
              </button>
            )}
          </div>

          {/* Filtro de usuario — todos/mis expedientes/un compañero específico */}
          <div className="relative">
            <button
              onClick={() => setDropdownUsuarioOpen(o => !o)}
              className="flex items-center gap-2 bg-input-background hover:bg-secondary text-foreground text-sm px-3 py-1.5 rounded transition border border-border"
            >
              <Filter size={12} className="text-muted-foreground" />
              <span className="max-w-[180px] truncate">{nombreFiltroUsuario}</span>
              <ChevronDown size={12} className={`text-muted-foreground transition-transform ${dropdownUsuarioOpen ? 'rotate-180' : ''}`} />
            </button>
            {dropdownUsuarioOpen && (
              <div className="absolute right-0 mt-1.5 w-56 bg-card border border-border rounded shadow-lg z-50 overflow-hidden max-h-64 overflow-y-auto">
                <button
                  onClick={() => { setFiltroUsuarioId(0); setDropdownUsuarioOpen(false) }}
                  className={`w-full text-left px-4 py-2.5 text-sm hover:bg-secondary transition ${filtroUsuarioId === 0 ? 'font-medium text-accent' : 'text-foreground'}`}
                >
                  Todos los usuarios
                </button>
                <button
                  onClick={() => { setFiltroUsuarioId(usuario?.id); setDropdownUsuarioOpen(false) }}
                  className={`w-full text-left px-4 py-2.5 text-sm hover:bg-secondary transition ${filtroUsuarioId === usuario?.id ? 'font-medium text-accent' : 'text-foreground'}`}
                >
                  Mis expedientes
                </button>
                <div className="border-t border-border" />
                {usuarios.filter(u => u.id !== usuario?.id).map(u => (
                  <button
                    key={u.id}
                    onClick={() => { setFiltroUsuarioId(u.id); setDropdownUsuarioOpen(false) }}
                    className={`w-full text-left px-4 py-2.5 text-sm hover:bg-secondary transition ${filtroUsuarioId === u.id ? 'font-medium text-accent' : 'text-foreground'}`}
                  >
                    {u.nombre}
                  </button>
                ))}
              </div>
            )}
          </div>

          <button
            onClick={() => navigate('/expedientes/nuevo')}
            className="ml-auto bg-accent text-accent-foreground px-4 py-1.5 rounded text-sm font-medium hover:opacity-90 transition"
          >
            + Nuevo expediente
          </button>
        </div>
      </div>

      {dropdownUsuarioOpen && (
        <div className="fixed inset-0 z-20" onClick={() => setDropdownUsuarioOpen(false)} />
      )}

      <main className="max-w-screen-xl mx-auto px-6 py-8">
        {error && (
          <div className="mb-4 bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
            {error}
          </div>
        )}

        {exito && (
          <div className="mb-4 bg-emerald-50 border border-emerald-200 text-emerald-700 text-sm rounded-md px-3 py-2">
            {exito}
          </div>
        )}

        {seleccionados.size > 0 && (
          <div className="mb-4 flex items-center justify-between bg-accent/10 border border-accent/30 rounded-lg px-4 py-2.5">
            <span className="text-sm text-foreground">
              {seleccionados.size} expediente{seleccionados.size !== 1 ? 's' : ''} seleccionado{seleccionados.size !== 1 ? 's' : ''}
            </span>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setSeleccionados(new Set())}
                className="text-xs text-muted-foreground hover:text-foreground transition"
              >
                Cancelar selección
              </button>
              <button
                onClick={() => setMostrarModalColaborador(true)}
                className="flex items-center gap-1.5 bg-accent text-accent-foreground px-3 py-1.5 rounded text-xs font-medium hover:opacity-90 transition"
              >
                <UserPlus size={12} />
                Agregar colaborador
              </button>
            </div>
          </div>
        )}

        {/* Bloques agrupados por usuario cuando se selecciona "Todos" */}
        {filtroUsuarioId === 0 ? (
          expedientesPorUsuario.map(grupo => (
            <BloqueExpandible
              key={grupo.usuarioId}
              titulo={`Expedientes de ${grupo.usuarioNombre}`}
              expedientes={ordenar(grupo.expedientes)}
              cargando={cargando}
              onRowClick={id => navigate(`/expedientes/${id}`)}
              sortKey={sortKey}
              sortDir={sortDir}
              onSort={handleSort}
              defaultAbierto={true}
              expedientesConAcuerdosNuevos={expedientesConAcuerdosNuevos}
              seleccionados={seleccionados}
              onToggleSeleccion={handleToggleSeleccion}
              onToggleSeleccionarTodos={handleToggleSeleccionarTodos}
            />
          ))
        ) : (
          <>
            <BloqueExpandible
              titulo={tituloBloque ?? 'Expedientes'}
              expedientes={ordenar(activosTitular)}
              cargando={cargando}
              onRowClick={id => navigate(`/expedientes/${id}`)}
              sortKey={sortKey}
              sortDir={sortDir}
              onSort={handleSort}
              defaultAbierto={true}
              expedientesConAcuerdosNuevos={expedientesConAcuerdosNuevos}
              seleccionados={seleccionados}
              onToggleSeleccion={handleToggleSeleccion}
              onToggleSeleccionarTodos={handleToggleSeleccionarTodos}
            />
            {esVistaPropia && activosColaborador.length > 0 && (
              <BloqueExpandible
                titulo="Como colaborador"
                expedientes={ordenar(activosColaborador)}
                cargando={cargando}
                onRowClick={id => navigate(`/expedientes/${id}`)}
                sortKey={sortKey}
                sortDir={sortDir}
                onSort={handleSort}
                defaultAbierto={true}
                expedientesConAcuerdosNuevos={expedientesConAcuerdosNuevos}
                seleccionados={seleccionados}
                onToggleSeleccion={handleToggleSeleccion}
                onToggleSeleccionarTodos={handleToggleSeleccionarTodos}
              />
            )}
          </>
        )}

        {/* Bloque de expedientes cerrados */}
        <BloqueExpandible
          titulo="Expedientes cerrados"
          expedientes={ordenar(muertosTitular)}
          cargando={cargando}
          onRowClick={id => navigate(`/expedientes/${id}`)}
          sortKey={sortKey}
          sortDir={sortDir}
          onSort={handleSort}
          defaultAbierto={false}
          expedientesConAcuerdosNuevos={expedientesConAcuerdosNuevos}
          seleccionados={seleccionados}
          onToggleSeleccion={handleToggleSeleccion}
          onToggleSeleccionarTodos={handleToggleSeleccionarTodos}
        />
        {esVistaPropia && muertosColaborador.length > 0 && (
          <BloqueExpandible
            titulo="Expedientes cerrados — como colaborador"
            expedientes={ordenar(muertosColaborador)}
            cargando={cargando}
            onRowClick={id => navigate(`/expedientes/${id}`)}
            sortKey={sortKey}
            sortDir={sortDir}
            onSort={handleSort}
            defaultAbierto={false}
            expedientesConAcuerdosNuevos={expedientesConAcuerdosNuevos}
            seleccionados={seleccionados}
            onToggleSeleccion={handleToggleSeleccion}
            onToggleSeleccionarTodos={handleToggleSeleccionarTodos}
          />
        )}

        {mostrarModalColaborador && (
          <ModalAgregarColaboradorMasivo
            expedienteIds={[...seleccionados]}
            usuarios={usuarios}
            onCerrar={() => setMostrarModalColaborador(false)}
            onCompletado={handleColaboradorMasivoCompletado}
          />
        )}
      </main>
    </div>
  )
}

export default Expedientes