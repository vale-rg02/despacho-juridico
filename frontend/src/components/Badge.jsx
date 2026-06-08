const variantes = {
  // Prioridad
  Normal:      "bg-gray-100 text-gray-600",
  Prioritario: "bg-yellow-100 text-yellow-700",
  Urgente:     "bg-red-100 text-red-700",
  // Estado
  Abierto:     "bg-green-100 text-green-700",
  Cerrado:     "bg-gray-100 text-gray-500",
  Pausado:     "bg-orange-100 text-orange-700",
}

function Badge({ texto }) {
  return (
    <span className={`text-xs font-medium px-2 py-1 rounded-full ${variantes[texto] ?? "bg-gray-100 text-gray-600"}`}>
      {texto}
    </span>
  )
}

export default Badge