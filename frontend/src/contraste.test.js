import { describe, it, expect } from 'vitest'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'

// Revisión UX/UI (WCAG 1.4.3): el dorado de marca (--accent) no alcanza 4.5:1
// como texto, por eso existen --accent-strong (texto/botones sobre claro) y
// --accent-light (texto sobre el navy). Esta prueba evita que un retoque futuro
// de la paleta vuelva a dejar texto por debajo del mínimo sin que nadie lo note.
const css = readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'index.css'), 'utf8')

function token(nombre) {
  const m = css.match(new RegExp(`--${nombre}:\\s*(#[0-9a-fA-F]{6})`))
  if (!m) throw new Error(`token --${nombre} no encontrado en index.css`)
  return m[1]
}

function luminancia(hex) {
  const [r, g, b] = [1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16) / 255)
  const f = v => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4)
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b)
}

function contraste(a, b) {
  const [x, y] = [luminancia(a), luminancia(b)]
  return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05)
}

describe('contraste de la paleta (WCAG AA, 4.5:1 para texto normal)', () => {
  it('texto en dorado fuerte sobre el fondo crema y sobre tarjetas blancas', () => {
    expect(contraste(token('accent-strong'), token('background'))).toBeGreaterThanOrEqual(4.5)
    expect(contraste(token('accent-strong'), token('card'))).toBeGreaterThanOrEqual(4.5)
  })

  it('texto blanco sobre botones de dorado fuerte', () => {
    expect(contraste(token('accent-foreground'), token('accent-strong'))).toBeGreaterThanOrEqual(4.5)
  })

  it('texto en dorado claro sobre el navy de la barra superior', () => {
    expect(contraste(token('accent-light'), token('primary'))).toBeGreaterThanOrEqual(4.5)
  })

  it('texto atenuado (muted) sobre el fondo y sobre los campos', () => {
    expect(contraste(token('muted-foreground'), token('background'))).toBeGreaterThanOrEqual(4.5)
    expect(contraste(token('muted-foreground'), token('input-background'))).toBeGreaterThanOrEqual(4.5)
  })

  it('el dorado de marca sin oscurecer sigue por debajo -- por eso no debe usarse como texto', () => {
    expect(contraste(token('accent'), token('background'))).toBeLessThan(4.5)
  })
})
