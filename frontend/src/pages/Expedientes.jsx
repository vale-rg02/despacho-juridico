import { useState, useEffect, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, ChevronDown, Filter, X, ArrowUpDown, ArrowUp, ArrowDown } from 'lucide-react'
import Topbar from '../components/Topbar'
import { getExpedientes } from '../services/expedientes'

const ESTADOS = ['Abierto', 'Pausado', 'Cerrado']

const estadoConfig = {
  Abierto: { bg: 'bg-emerald-50', text: 'text-emerald-800', dot: 'bg-emerald-500' },
  Pausado: { bg: 'bg-amber-50',   text: 'text-amber-800',   dot: 'bg-amber-500'  },
  Cerrado: { bg: 'bg-stone-100',  text: 'text-stone-600',   dot: 'bg-stone-400'  },
}

const prioridadConfig = {
  Urgente:     { color: 'text-red-700',   symbol: '▲' },
  Prioritario: { color: 'text-amber-700', symbol: '●' },
  Normal:      { color: 'text-stone-500', symbol: '▼' },
}

const COLUMNAS = [
  { key: 'numeroExpediente', label: 'Número' },
  { key: 'parteDemandada',   label: 'Parte Demandada' },
  { key: 'juzgado',          label: 'Juzgado' },
  { key: 'materia',          label: 'Materia' },
  { key: 'estado',           label: 'Estado' },
  { key: 'prioridad',        label: 'Prioridad' },
]

function Expedientes() {
  const navigate = useNavigate()
  const [busqueda, setBusqueda] = useState('')
  const [filtroEstado, setFiltroEstado] = useState('Todos')
  const [dropdownOpen, setDropdownOpen] = useState(false)
  const [sortKey, setSortKey] = useState('numeroExpediente')
  const [sortDir, setSortDir] = useState('asc')
  const [expedientes, setExpedientes] = useState([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    cargarExpedientes()
  }, [filtroEstado])

  useEffect(() => {
    const timeout = setTimeout(() => {
      cargarExpedientes()
    }, 400)
    return () => clearTimeout(timeout)
  }, [busqueda])

  async function cargarExpedientes() {
    setCargando(true)
    setError('')
    try {
      const data = await getExpedientes({ estado: filtroEstado, busqueda })
      setExpedientes(data)
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

  const expedientesOrdenados = useMemo(() => {
    const list = [...expedientes]
    list.sort((a, b) => {
      const av = (a[sortKey] ?? '').toString()
      const bv = (b[sortKey] ?? '').toString()
      return sortDir === 'asc' ? av.localeCompare(bv, 'es') : bv.localeCompare(av, 'es')
    })
    return list
  }, [expedientes, sortKey, sortDir])

  function SortIcon({ col }) {
    if (sortKey !== col) return <ArrowUpDown size={13} className="opacity-30 ml-1 inline" />
    return sortDir === 'asc'
      ? <ArrowUp size={13} className="ml-1 inline text-accent" />
      : <ArrowDown size={13} className="ml-1 inline text-accent" />
  }

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
              <button
                onClick={() => setBusqueda('')}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground transition"
              >
                <X size={12} />
              </button>
            )}
          </div>

          <div className="relative ml-auto">
            <button
              onClick={() => setDropdownOpen(o => !o)}
              className="flex items-center gap-2 bg-input-background hover:bg-secondary text-foreground text-sm px-3 py-1.5 rounded transition border border-border"
            >
              <Filter size={12} className="text-muted-foreground" />
              <span>{filtroEstado === 'Todos' ? 'Todos los estados' : filtroEstado}</span>
              <ChevronDown size={12} className={`text-muted-foreground transition-transform ${dropdownOpen ? 'rotate-180' : ''}`} />
            </button>
            {dropdownOpen && (
              <div className="absolute right-0 mt-1.5 w-48 bg-card border border-border rounded shadow-lg z-50 overflow-hidden">
                <button
                  onClick={() => { setFiltroEstado('Todos'); setDropdownOpen(false) }}
                  className={`w-full text-left px-4 py-2.5 text-sm hover:bg-secondary transition ${filtroEstado === 'Todos' ? 'font-medium text-accent' : 'text-foreground'}`}
                >
                  Todos los estados
                </button>
                {ESTADOS.map(e => (
                  <button
                    key={e}
                    onClick={() => { setFiltroEstado(e); setDropdownOpen(false) }}
                    className={`w-full text-left px-4 py-2.5 text-sm flex items-center gap-2.5 hover:bg-secondary transition ${filtroEstado === e ? 'font-medium text-accent' : 'text-foreground'}`}
                  >
                    <span className={`w-2 h-2 rounded-full ${estadoConfig[e]?.dot}`} />
                    {e}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
      {dropdownOpen && (
        <div className="fixed inset-0 z-20" onClick={() => setDropdownOpen(false)} />
      )}

      <main className="max-w-screen-xl mx-auto px-6 py-8">
        <div className="mb-6 flex items-end justify-between gap-4">
          <div>
            <h1 className="text-2xl text-foreground leading-tight" style={{ fontFamily: "'Playfair Display', serif" }}>
              Expedientes
            </h1>
            <p className="text-sm text-muted-foreground mt-0.5">
              {expedientesOrdenados.length} expediente{expedientesOrdenados.length !== 1 ? 's' : ''}
              {filtroEstado !== 'Todos' ? ` · ${filtroEstado}` : ''}
              {busqueda ? ` · "${busqueda}"` : ''}
            </p>
          </div>
          <div className="flex items-center gap-2">
            {filtroEstado !== 'Todos' && (
              <button
                onClick={() => setFiltroEstado('Todos')}
                className="flex items-center gap-1.5 text-xs bg-accent/10 text-accent border border-accent/20 px-2.5 py-1 rounded-full hover:bg-accent/20 transition"
              >
                {filtroEstado} <X size={11} />
              </button>
            )}
            {busqueda && (
              <button
                onClick={() => setBusqueda('')}
                className="flex items-center gap-1.5 text-xs bg-accent/10 text-accent border border-accent/20 px-2.5 py-1 rounded-full hover:bg-accent/20 transition"
              >
                "{busqueda}" <X size={11} />
              </button>
            )}
            <button
              onClick={() => navigate('/expedientes/nuevo')}
              className="bg-accent text-accent-foreground px-4 py-1.5 rounded text-sm font-medium hover:opacity-90 transition"
            >
              + Nuevo expediente
            </button>
          </div>
        </div>

        {error && (
          <div className="mb-4 bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
            {error}
          </div>
        )}

        <div className="bg-card border border-border rounded-lg overflow-hidden shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-sm border-collapse">
              <thead>
                <tr className="border-b border-border bg-secondary/60">
                  {COLUMNAS.map(col => (
                    <th
                      key={col.key}
                      onClick={() => handleSort(col.key)}
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
                {cargando ? (
                  <tr>
                    <td colSpan={6} className="text-center py-16 text-muted-foreground text-sm">
                      Cargando expedientes...
                    </td>
                  </tr>
                ) : expedientesOrdenados.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="text-center py-16 text-muted-foreground text-sm">
                      No se encontraron expedientes con los filtros aplicados.
                    </td>
                  </tr>
                ) : (
                  expedientesOrdenados.map((exp, i) => (
                    <tr
                      key={exp.id}
                      onClick={() => navigate(`/expedientes/${exp.id}`)}
                      className={`border-b border-border last:border-0 hover:bg-accent/5 hover:cursor-pointer transition-colors group ${i % 2 === 0 ? '' : 'bg-secondary/20'}`}
                    >
                      <td className="px-4 py-3.5 whitespace-nowrap">
                        <span className="text-accent font-medium group-hover:underline" style={{ fontFamily: "'DM Mono', monospace", fontSize: '0.8rem' }}>
                          {exp.numeroExpediente}
                        </span>
                      </td>
                      <td className="px-4 py-3.5">
                        <span className="text-foreground font-medium">{exp.parteDemandada}</span>
                      </td>
                      <td className="px-4 py-3.5 text-muted-foreground whitespace-nowrap">{exp.juzgado ?? '—'}</td>
                      <td className="px-4 py-3.5 text-foreground whitespace-nowrap">{exp.materia ?? '—'}</td>
                      <td className="px-4 py-3.5 whitespace-nowrap">
                        <span className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium ${estadoConfig[exp.estado]?.bg} ${estadoConfig[exp.estado]?.text}`}>
                          <span className={`w-1.5 h-1.5 rounded-full ${estadoConfig[exp.estado]?.dot}`} />
                          {exp.estado}
                        </span>
                      </td>
                      <td className="px-4 py-3.5 whitespace-nowrap">
                        <span className={`text-xs font-medium ${prioridadConfig[exp.prioridad]?.color}`} style={{ fontFamily: "'DM Mono', monospace" }}>
                          {prioridadConfig[exp.prioridad]?.symbol} {exp.prioridad}
                        </span>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="px-4 py-3 border-t border-border bg-secondary/30 flex items-center justify-between">
            <span className="text-xs text-muted-foreground" style={{ fontFamily: "'DM Mono', monospace" }}>
              {expedientesOrdenados.length} expediente{expedientesOrdenados.length !== 1 ? 's' : ''}
            </span>
            <span className="text-xs text-muted-foreground" style={{ fontFamily: "'DM Mono', monospace" }}>
              Despacho Jurídico · {new Date().getFullYear()}
            </span>
          </div>
        </div>
      </main>
    </div>
  )
}

export default Expedientes