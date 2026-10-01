import { ChevronDown } from 'lucide-react'

// DJ-124: el <select> nativo deja su flecha al navegador, que no respeta el
// padding del control (queda pegada al borde) y la centra verticalmente de
// forma inconsistente entre navegadores. Este wrapper oculta esa flecha
// (appearance-none) y dibuja una propia, igual al resto de íconos del
// sistema (lucide-react). El padding-right va por `style` en vez de una
// clase Tailwind para no competir con el `px-*`/`pr-*` que cada caller ya
// trae en su `className` -- dos utilidades de Tailwind sobre la misma
// propiedad las resuelve el orden de la hoja generada, no el del código, así
// que un override inline es la única forma confiable de garantizarlo.
function SelectConFlecha({ className = '', wrapperClassName = '', children, ...props }) {
  return (
    <div className={`relative ${wrapperClassName}`}>
      <select
        {...props}
        className={`appearance-none ${className}`}
        style={{ paddingRight: '2.25rem' }}
      >
        {children}
      </select>
      <ChevronDown
        size={14}
        className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground"
      />
    </div>
  )
}

export default SelectConFlecha
