import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import FullCalendar from '@fullcalendar/react'
import dayGridPlugin from '@fullcalendar/daygrid'
import timeGridPlugin from '@fullcalendar/timegrid'
import interactionPlugin from '@fullcalendar/interaction'
import esLocale from '@fullcalendar/core/locales/es'
import { CalendarDays } from 'lucide-react'
import Topbar from '../components/Topbar'
import { getExpedientes } from '../services/expedientes'
import { getHistorialEtapas } from '../services/etapas'
import './Calendario.css'

const TAMANO_LOTE = 20

function colorPorPrioridad(prioridad) {
  if (prioridad === 'Urgente') return '#ef4444'
  if (prioridad === 'Prioritario') return '#f59e0b'
  return '#9a7c3c'
}

// Corta el ISO a "YYYY-MM-DD" sin pasar por new Date(), para no desfasar
// el día al convertir el DateTime del backend a la zona horaria del navegador
function soloFecha(fechaISO) {
  return fechaISO.slice(0, 10)
}

async function obtenerEtapasEnLotes(expedientes) {
  const pares = []
  for (let i = 0; i < expedientes.length; i += TAMANO_LOTE) {
    const lote = expedientes.slice(i, i + TAMANO_LOTE)
    const etapasLote = await Promise.all(
      lote.map(exp => getHistorialEtapas(exp.id).catch(() => []))
    )
    lote.forEach((exp, idx) => pares.push({ expediente: exp, etapas: etapasLote[idx] }))
  }
  return pares
}

function construirEventos(pares) {
  const eventos = []
  pares.forEach(({ expediente, etapas }) => {
    etapas
      .filter(etapa => etapa.fechaLimite && !etapa.fechaCompletada)
      .forEach(etapa => {
        eventos.push({
          id: `etapa-${etapa.id}`,
          title: `${expediente.numeroExpediente} — ${etapa.etapaNombre}`,
          date: soloFecha(etapa.fechaLimite),
          backgroundColor: colorPorPrioridad(expediente.prioridad),
          borderColor: colorPorPrioridad(expediente.prioridad),
          extendedProps: {
            expedienteId: expediente.id,
            parteDemandada: expediente.parteDemandada,
            juzgado: expediente.juzgado,
            prioridad: expediente.prioridad,
          },
        })
      })
  })
  return eventos
}

function Calendario() {
  const navigate = useNavigate()
  const [eventos, setEventos] = useState([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    cargarEventos()
  }, [])

  async function cargarEventos() {
    setCargando(true)
    setError('')
    try {
      const [abiertos, pausados] = await Promise.all([
        getExpedientes({ estado: 'Abierto' }),
        getExpedientes({ estado: 'Pausado' }),
      ])
      const pares = await obtenerEtapasEnLotes([...abiertos, ...pausados])
      setEventos(construirEventos(pares))
    } catch {
      setError('No se pudo cargar el calendario')
    } finally {
      setCargando(false)
    }
  }

  function handleEventClick(info) {
    navigate(`/expedientes/${info.event.extendedProps.expedienteId}`)
  }

  return (
    <div className="min-h-screen bg-background">
      <Topbar
        breadcrumb={
          <span className="text-primary-foreground/75 text-sm">Calendario</span>
        }
      />

      <main className="max-w-screen-xl mx-auto px-6 py-8">
        {error && (
          <div className="mb-4 bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
            {error}
          </div>
        )}

        {cargando ? (
          <div className="text-center py-16 text-muted-foreground text-sm">
            Cargando calendario...
          </div>
        ) : eventos.length === 0 && !error ? (
          <div className="flex flex-col items-center justify-center gap-2 py-16 text-center">
            <CalendarDays size={28} className="text-muted-foreground/50" />
            <p className="text-sm text-muted-foreground">Sin compromisos pendientes</p>
          </div>
        ) : (
          <div className="fc-wrapper bg-card border border-border rounded-lg p-4">
            <FullCalendar
              plugins={[dayGridPlugin, timeGridPlugin, interactionPlugin]}
              initialView="dayGridMonth"
              headerToolbar={{
                left: 'prev,next today',
                center: 'title',
                right: 'dayGridMonth,timeGridWeek,timeGridDay',
              }}
              locale={esLocale}
              events={eventos}
              eventClick={handleEventClick}
              height="auto"
            />
          </div>
        )}
      </main>
    </div>
  )
}

export default Calendario
