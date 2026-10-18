// DJ-83/DJ-124: estilos y utilidades compartidos por todos los desplegables de
// formulario (ComboboxCatalogo = con búsqueda, Select = lista cerrada). Un
// cambio visual aquí se aplica a ambos -- antes cada uno repetía sus clases y
// los <select> nativos ni siquiera controlaban el panel abierto (lo dibuja el
// sistema operativo).

// pl-3 + pr-9 (no px-3 + pr-9): dos utilidades de Tailwind sobre la misma
// propiedad las resuelve el orden de la hoja generada, no el del código. El
// pr-9 deja espacio para la flecha (14px a right-3) sin que el texto la pise.
export const CAJA_CON_FLECHA = "w-full bg-input-background text-foreground text-sm pl-3 pr-9 py-2 rounded transition"

// Opción vacía estándar de los formularios (permite dejar el campo sin elegir).
export const SIN_SELECCION = { valor: '', etiqueta: '— Selecciona —' }

export const PANEL = "absolute z-10 mt-1 w-full max-h-56 overflow-y-auto bg-card border border-border rounded-md shadow-lg"

export const FLECHA = "pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground transition-transform"

// pointer-coarse: en pantallas táctiles la fila crece a 44px (HIG / WCAG 2.5.5).
export function claseOpcion({ activa, seleccionada }) {
  return `w-full text-left px-3 py-2 pointer-coarse:min-h-11 text-sm transition flex items-center justify-between gap-2 ${activa ? 'bg-accent/10' : 'hover:bg-accent/10'} ${seleccionada ? 'font-medium' : ''}`
}

// Insensible a mayúsculas Y a acentos, mismo criterio que unaccent() del
// backend (DJ-110, AplicarFiltroBusqueda en ExpedientesController) -- solo
// para COMPARAR durante la búsqueda; el valor mostrado/guardado siempre es el
// texto original, con sus acentos correctos (ej. "Álamos").
// U+0300-U+036F es el bloque Unicode de marcas diacríticas combinantes (lo que
// normalize('NFD') separa de la letra base, ej. "Á" -> "A" + acento combinante).
export function normalizarParaBusqueda(texto) {
  return texto
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
}
