import { useState, useEffect } from 'react'
import { getEtapasCatalogo, registrarEtapa } from '../services/etapas'
import { calcularFechaLimite } from '../utils/diasHabiles'

function hoyISO() {
  return new Date().toISOString().slice(0, 10)
}

function FormularioEtapa({ expedienteId, tipoJuicio, onGuardado, onCancelar }) {
  const [catalogo, setCatalogo] = useState([])
  const [cargandoCatalogo, setCargandoCatalogo] = useState(true)

  const [etapaCatalogoId, setEtapaCatalogoId] = useState('')
  const [fechaInicio, setFechaInicio] = useState(hoyISO())
  const [fechaLimite, setFechaLimite] = useState('')
  const [notas, setNotas] = useState('')

  const [error, setError] = useState('')
  const [guardando, setGuardando] = useState(false)

  useEffect(() => {
    cargarCatalogo()
  }, [])

  async function cargarCatalogo() {
    try {
      const data = await getEtapasCatalogo(tipoJuicio)
      setCatalogo(data)
    } catch {
      setError('No se pudo cargar el catálogo de etapas')
    } finally {
      setCargandoCatalogo(false)
    }
  }

  // Cuando cambia la etapa o la fecha de inicio, recalculamos la sugerencia
  useEffect(() => {
    if (!etapaCatalogoId) return
    const etapa = catalogo.find(e => e.id === Number(etapaCatalogoId))
    if (!etapa) return

    const sugerida = calcularFechaLimite(fechaInicio, etapa.terminoDias, etapa.esDiasHabiles)
    setFechaLimite(sugerida ?? '')
  }, [etapaCatalogoId, fechaInicio, catalogo])

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')

    if (!etapaCatalogoId) {
      setError('Selecciona una etapa')
      return
    }

    setGuardando(true)
    try {
      await registrarEtapa(expedienteId, {
        etapaCatalogoId: Number(etapaCatalogoId),
        fechaInicio,
        fechaLimite: fechaLimite || null,
        notas: notas || null,
      })
      onGuardado()
    } catch (err) {
      setError('No se pudo registrar la etapa')
    } finally {
      setGuardando(false)
    }
  }

  const etapaSeleccionada = catalogo.find(e => e.id === Number(etapaCatalogoId))

  return (
    <form onSubmit={handleSubmit} className="bg-gray-50 rounded-lg p-4 border border-gray-200 mb-4">
      {error && (
        <div className="mb-3 bg-red-50 border border-red-200 text-red-600 text-sm rounded-md px-3 py-2">
          {error}
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-3 mb-3">
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">Etapa *</label>
          <select
            value={etapaCatalogoId}
            onChange={e => setEtapaCatalogoId(e.target.value)}
            disabled={cargandoCatalogo}
            className="w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
          >
            <option value="">— Selecciona —</option>
            {catalogo.map(e => (
              <option key={e.id} value={e.id}>{e.nombre}</option>
            ))}
          </select>
        </div>

        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">Fecha de inicio *</label>
          <input
            type="date"
            value={fechaInicio}
            onChange={e => setFechaInicio(e.target.value)}
            className="w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">
            Fecha límite
            {etapaSeleccionada?.terminoDias != null && (
              <span className="text-gray-400 font-normal"> (sugerida)</span>
            )}
          </label>
          <input
            type="date"
            value={fechaLimite}
            onChange={e => setFechaLimite(e.target.value)}
            placeholder={etapaSeleccionada?.terminoDias == null ? 'Sin plazo definido' : ''}
            className="w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          {etapaSeleccionada?.terminoDias == null && etapaCatalogoId && (
            <p className="text-xs text-gray-400 mt-1">Esta etapa no tiene plazo definido aún. Captúralo manualmente si lo conoces.</p>
          )}
        </div>
      </div>

      <div className="mb-3">
        <label className="block text-xs font-medium text-gray-600 mb-1">Notas</label>
        <input
          type="text"
          value={notas}
          onChange={e => setNotas(e.target.value)}
          placeholder="Información adicional (opcional)"
          className="w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div className="flex justify-end gap-2">
        <button
          type="button"
          onClick={onCancelar}
          className="px-3 py-1.5 text-sm text-gray-600 hover:text-gray-800"
        >
          Cancelar
        </button>
        <button
          type="submit"
          disabled={guardando}
          className="bg-blue-600 text-white px-4 py-1.5 rounded-md hover:bg-blue-700 transition-colors text-sm font-medium disabled:opacity-50"
        >
          {guardando ? 'Guardando...' : 'Registrar etapa'}
        </button>
      </div>
    </form>
  )
}

export default FormularioEtapa