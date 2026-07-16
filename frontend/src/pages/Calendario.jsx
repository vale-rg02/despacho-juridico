import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import FullCalendar from '@fullcalendar/react'
import dayGridPlugin from '@fullcalendar/daygrid'
import timeGridPlugin from '@fullcalendar/timegrid'
import interactionPlugin from '@fullcalendar/interaction'
import esLocale from '@fullcalendar/core/locales/es'
import Topbar from '../components/Topbar'
import ModalCita from '../components/ModalCita'
import ModalEtapaCalendario from '../components/ModalEtapaCalendario'
import { getExpedientes } from '../services/expedientes'
import { getHistorialEtapas } from '../services/etapas'
import { getCitas } from '../services/citas'
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

// true si la etapa trae una hora real (no medianoche); se lee del string ISO
// directo, sin pasar por Date().getHours(), que la desfasaría según la zona
// horaria del navegador (fechaLimite viene "etiquetada" como UTC sin conversión real)
function tieneHoraReal(fechaISO) {
  const match = fechaISO.match(/T(\d{2}):(\d{2})/)
  return match ? !(match[1] === '00' && match[2] === '00') : false
}

// FullCalendar interpreta los ISO sin sufijo de zona como hora "flotante" (la
// muestra tal cual, sin convertir); como fechaLimite viene etiquetada como UTC
// sin conversión real, hay que quitarle la "Z" para que no se desfase al renderizar
function sinZonaUtc(fechaISO) {
  return fechaISO.endsWith('Z') ? fechaISO.slice(0, -1) : fechaISO
}

// Extrae "HH:MM" directo del string ISO (mismo motivo que las funciones de arriba:
// nunca pasar por new Date().getHours(), que desfasaría la hora)
function horaDeIso(fechaISO) {
  if (!fechaISO) return ''
  const match = fechaISO.match(/T(\d{2}):(\d{2})/)
  if (!match) return ''
  const [, horas, minutos] = match
  return horas === '00' && minutos === '00' ? '' : `${horas}:${minutos}`
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

function construirEventosEtapas(pares) {
  const eventos = []
  pares.forEach(({ expediente, etapas }) => {
    etapas
      .filter(etapa => etapa.fechaLimite && !etapa.fechaCompletada)
      .forEach(etapa => {
        const tieneHora = tieneHoraReal(etapa.fechaLimite)
        eventos.push({
          id: `etapa-${etapa.id}`,
          title: `${expediente.numeroExpediente} — ${etapa.etapaNombre}`,
          // Con hora: evento con inicio/fin (1 hora de duración por defecto),
          // visible en el bloque correcto en vistas de semana y día.
          // Sin hora: evento de día completo, como antes.
          ...(tieneHora
            ? {
                start: sinZonaUtc(etapa.fechaLimite),
                end: sinZonaUtc(new Date(new Date(etapa.fechaLimite).getTime() + 60 * 60 * 1000).toISOString()),
                allDay: false,
              }
            : {
                date: soloFecha(etapa.fechaLimite),
                allDay: true,
              }
          ),
          backgroundColor: colorPorPrioridad(expediente.prioridad),
          borderColor: colorPorPrioridad(expediente.prioridad),
          extendedProps: {
            tipo: 'etapa',
            expedienteId: expediente.id,
            numeroExpediente: expediente.numeroExpediente,
            parteDemandada: expediente.parteDemandada,
            juzgado: expediente.juzgado,
            prioridad: expediente.prioridad,
            etapaId: etapa.id,
            etapaCatalogoId: etapa.etapaCatalogoId,
            etapaNombre: etapa.etapaNombre,
            fechaInicio: etapa.fechaInicio,
            fechaLimite: etapa.fechaLimite,
            notas: etapa.notas,
          },
        })
      })
  })
  return eventos
}

function construirEventosCitas(citas) {
  return citas.map(cita => ({
    id: `cita-${cita.id}`,
    title: cita.expedienteId
      ? `📌 ${cita.titulo} (${cita.numeroExpediente})`
      : `📌 ${cita.titulo}`,
    start: cita.fechaHora,
    backgroundColor: '#1c2b4a',
    borderColor: '#1c2b4a',
    textColor: '#ffffff',
    extendedProps: {
      tipo: 'cita',
      citaId: cita.id,
      titulo: cita.titulo,
      descripcion: cita.descripcion,
      expedienteId: cita.expedienteId,
      numeroExpediente: cita.numeroExpediente,
      fechaHora: cita.fechaHora,
    },
  }))
}

function mesAnioDe(fecha) {
  return { mes: fecha.getMonth() + 1, anio: fecha.getFullYear() }
}

function Calendario() {
  const navigate = useNavigate()

  const [eventosEtapas, setEventosEtapas] = useState([])
  const [citas, setCitas] = useState([])
  const [expedientesActivos, setExpedientesActivos] = useState([])
  const [modalCita, setModalCita] = useState(null)
  const [modalEtapa, setModalEtapa] = useState(null)
  const [mesVisible, setMesVisible] = useState(() => mesAnioDe(new Date()))

  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    cargarDatos()
  }, [])

  async function cargarDatos() {
    setCargando(true)
    setError('')
    try {
      const [abiertos, pausados] = await Promise.all([
        getExpedientes({ estado: 'Abierto' }),
        getExpedientes({ estado: 'Pausado' }),
      ])
      const activos = [...abiertos, ...pausados]
      setExpedientesActivos(activos)

      const [pares, dataCitas] = await Promise.all([
        obtenerEtapasEnLotes(activos),
        getCitas(mesVisible.mes, mesVisible.anio),
      ])
      setEventosEtapas(construirEventosEtapas(pares))
      setCitas(dataCitas)
    } catch {
      setError('No se pudo cargar el calendario')
    } finally {
      setCargando(false)
    }
  }

  async function cargarCitasDelMes(mes, anio) {
    try {
      const data = await getCitas(mes, anio)
      setCitas(data)
    } catch {
      // no bloquear el calendario si solo falla la recarga de citas
    }
  }

  async function recargarEtapas() {
    try {
      const pares = await obtenerEtapasEnLotes(expedientesActivos)
      setEventosEtapas(construirEventosEtapas(pares))
    } catch {
      // no bloquear el calendario si solo falla la recarga de etapas
    }
  }

  function handleDatesSet(info) {
    const { mes, anio } = mesAnioDe(info.view.currentStart)
    if (mes !== mesVisible.mes || anio !== mesVisible.anio) {
      setMesVisible({ mes, anio })
      cargarCitasDelMes(mes, anio)
    }
  }

  function handleDateClick(info) {
    setModalCita({
      modo: 'crear',
      fecha: info.dateStr.slice(0, 10),
      hora: '09:00',
    })
  }

  function handleEventClick(info) {
    const { extendedProps } = info.event

    if (extendedProps.tipo === 'cita') {
      setModalCita({
        modo: 'editar',
        cita: {
          id: extendedProps.citaId,
          titulo: extendedProps.titulo,
          descripcion: extendedProps.descripcion,
          expedienteId: extendedProps.expedienteId,
          fechaHora: extendedProps.fechaHora,
        },
      })
    } else {
      setModalEtapa({
        etapaId: extendedProps.etapaId,
        etapaCatalogoId: extendedProps.etapaCatalogoId,
        expedienteId: extendedProps.expedienteId,
        numeroExpediente: extendedProps.numeroExpediente,
        etapaNombre: extendedProps.etapaNombre,
        fechaInicio: extendedProps.fechaInicio,
        notas: extendedProps.notas,
        fechaLimite: extendedProps.fechaLimite ? soloFecha(extendedProps.fechaLimite) : '',
        horaLimite: horaDeIso(extendedProps.fechaLimite),
      })
    }
  }

  const todosLosEventos = [...eventosEtapas, ...construirEventosCitas(citas)]

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
              events={todosLosEventos}
              dateClick={handleDateClick}
              eventClick={handleEventClick}
              datesSet={handleDatesSet}
              height="auto"
            />
          </div>
        )}
      </main>

      {modalCita && (
        <ModalCita
          modalCita={modalCita}
          setModalCita={setModalCita}
          expedientesActivos={expedientesActivos}
          onGuardado={() => cargarCitasDelMes(mesVisible.mes, mesVisible.anio)}
        />
      )}

      {modalEtapa && (
        <ModalEtapaCalendario
          modalEtapa={modalEtapa}
          setModalEtapa={setModalEtapa}
          onGuardado={recargarEtapas}
          onVerExpediente={id => navigate(`/expedientes/${id}`)}
        />
      )}
    </div>
  )
}

export default Calendario
