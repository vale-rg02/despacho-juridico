import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import ComboboxCatalogo from './ComboboxCatalogo'

// DJ-112/DJ-87: el combobox genérico reusado para Sede y Juzgado -- lo que más
// importa probar es que Juzgado (DJ-87) ya no acepta texto libre y que el
// filtrado funciona, ya que es la base del combobox dependiente del formulario
// de expediente.
describe('ComboboxCatalogo', () => {
  const opcionesHermosillo = ['1ro Civil Hermosillo', '2do Civil Hermosillo', '1ro Oral Mercantil Hermosillo']

  it('muestra todas las opciones al enfocar, sin filtrar todavía', () => {
    render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesHermosillo} />)

    fireEvent.focus(screen.getByRole('combobox'))

    for (const opcion of opcionesHermosillo) {
      expect(screen.getByText(opcion)).toBeInTheDocument()
    }
  })

  it('filtra las opciones según el texto escrito', () => {
    render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesHermosillo} />)

    const input = screen.getByRole('combobox')
    fireEvent.focus(input)
    fireEvent.change(input, { target: { value: 'oral' } })

    expect(screen.getByText('1ro Oral Mercantil Hermosillo')).toBeInTheDocument()
    expect(screen.queryByText('1ro Civil Hermosillo')).not.toBeInTheDocument()
    expect(screen.queryByText('2do Civil Hermosillo')).not.toBeInTheDocument()
  })

  it('al hacer clic en una opción, llama onChange con ese valor', () => {
    const onChange = vi.fn()
    render(<ComboboxCatalogo value="" onChange={onChange} opciones={opcionesHermosillo} />)

    fireEvent.focus(screen.getByRole('combobox'))
    fireEvent.click(screen.getByText('2do Civil Hermosillo'))

    expect(onChange).toHaveBeenCalledWith('2do Civil Hermosillo')
  })

  it('DJ-87: si se escribe texto que no coincide con ninguna opción y se quita el foco, revierte (ya no acepta texto libre)', () => {
    const onChange = vi.fn()
    const { container } = render(
      <div>
        <ComboboxCatalogo value="1ro Civil Hermosillo" onChange={onChange} opciones={opcionesHermosillo} />
        <button>afuera</button>
      </div>
    )

    const input = screen.getByRole('combobox')
    fireEvent.focus(input)
    fireEvent.change(input, { target: { value: 'texto que no existe en el catalogo' } })

    // clic afuera del combobox -- simula perder el foco
    fireEvent.mouseDown(screen.getByText('afuera'))

    expect(input.value).toBe('1ro Civil Hermosillo') // revertido, no se quedó el texto libre
    expect(onChange).not.toHaveBeenCalled() // nunca se confirmó un valor inválido
  })

  it('encuentra opciones con acento aunque se escriba sin acento', () => {
    const opcionesMunicipios = ['Álamos', 'Hermosillo', 'Cananea']
    render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesMunicipios} />)

    const input = screen.getByRole('combobox')
    fireEvent.focus(input)
    fireEvent.change(input, { target: { value: 'Alamos' } })

    expect(screen.getByText('Álamos')).toBeInTheDocument()
    expect(screen.queryByText('Hermosillo')).not.toBeInTheDocument()
  })

  it('escribir la primera letra sin acento ("A") encuentra la opción acentuada', () => {
    const opcionesMunicipios = ['Álamos', 'Hermosillo', 'Cananea']
    render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesMunicipios} />)

    const input = screen.getByRole('combobox')
    fireEvent.focus(input)
    fireEvent.change(input, { target: { value: 'A' } })

    expect(screen.getByText('Álamos')).toBeInTheDocument()
  })

  it('la búsqueda es insensible a acentos en cualquier combinación (con/sin acento, mayúsculas/minúsculas)', () => {
    const opcionesMunicipios = ['Álamos', 'Hermosillo', 'Cananea']

    for (const consulta of ['alamos', 'ALAMOS', 'Álamos', 'álamos', 'ÁLAMOS']) {
      const { unmount } = render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesMunicipios} />)
      const input = screen.getByRole('combobox')
      fireEvent.focus(input)
      fireEvent.change(input, { target: { value: consulta } })
      expect(screen.getByText('Álamos')).toBeInTheDocument()
      unmount()
    }
  })

  it('al seleccionar una opción con acento, el valor guardado conserva el acento correcto', () => {
    const onChange = vi.fn()
    const opcionesMunicipios = ['Álamos', 'Hermosillo', 'Cananea']
    render(<ComboboxCatalogo value="" onChange={onChange} opciones={opcionesMunicipios} />)

    const input = screen.getByRole('combobox')
    fireEvent.focus(input)
    fireEvent.change(input, { target: { value: 'Alamos' } })
    fireEvent.click(screen.getByText('Álamos'))

    expect(onChange).toHaveBeenCalledWith('Álamos') // se guarda con el acento correcto, no el texto tecleado
    expect(input.value).toBe('Álamos')
  })

  it('nombres sin acento no se ven afectados por la normalización', () => {
    render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesHermosillo} />)

    const input = screen.getByRole('combobox')
    fireEvent.focus(input)
    fireEvent.change(input, { target: { value: 'civil' } })

    expect(screen.getByText('1ro Civil Hermosillo')).toBeInTheDocument()
    expect(screen.getByText('2do Civil Hermosillo')).toBeInTheDocument()
    expect(screen.queryByText('1ro Oral Mercantil Hermosillo')).not.toBeInTheDocument()
  })

  // DJ-123: antes, al abrir un combobox que YA tenía un valor elegido (ej.
  // Sede="Hermosillo"), solo se mostraban las opciones que calzaban con ese
  // texto -- en la práctica, solo la ya elegida. Había que borrarla para ver
  // el resto del catálogo. Ahora se muestra todo hasta que el usuario escribe.
  it('DJ-123: con un valor ya elegido, al enfocar muestra TODAS las opciones (no solo la ya elegida)', () => {
    render(<ComboboxCatalogo value="1ro Civil Hermosillo" onChange={() => {}} opciones={opcionesHermosillo} />)

    fireEvent.focus(screen.getByRole('combobox'))

    for (const opcion of opcionesHermosillo) {
      expect(screen.getByText(opcion)).toBeInTheDocument()
    }
  })

  it('DJ-123: tras abrir con un valor ya elegido, escribir sí refina la lista', () => {
    render(<ComboboxCatalogo value="1ro Civil Hermosillo" onChange={() => {}} opciones={opcionesHermosillo} />)

    const input = screen.getByRole('combobox')
    fireEvent.focus(input)
    fireEvent.change(input, { target: { value: 'oral' } })

    expect(screen.getByText('1ro Oral Mercantil Hermosillo')).toBeInTheDocument()
    expect(screen.queryByText('1ro Civil Hermosillo')).not.toBeInTheDocument()
  })

  // DJ-122: Juzgado recibe en `opciones` la lista YA filtrada por Sede+Materia
  // (o el fallback a la sede completa) -- ComboboxCatalogo no sabe nada de esa
  // lógica, solo muestra lo que se le pasa. Esta prueba confirma que "mostrar
  // todo al abrir" respeta ese subconjunto ya filtrado, no un catálogo aparte.
  it('DJ-123/DJ-122: con un `opciones` ya acotado (ej. por el filtro de Juzgado), abrir muestra ese subconjunto completo', () => {
    const juzgadosFiltradosPorFamiliar = ['1ro Familiar Hermosillo', '2do Familiar Hermosillo']
    render(<ComboboxCatalogo value="1ro Familiar Hermosillo" onChange={() => {}} opciones={juzgadosFiltradosPorFamiliar} />)

    fireEvent.focus(screen.getByRole('combobox'))

    expect(screen.getByText('1ro Familiar Hermosillo')).toBeInTheDocument()
    expect(screen.getByText('2do Familiar Hermosillo')).toBeInTheDocument()
    // Ningún juzgado fuera del subconjunto filtrado debería aparecer nunca
    expect(screen.queryByText('1ro Civil Hermosillo')).not.toBeInTheDocument()
  })

  // DJ-123: hallazgo real al verificar en vivo -- Chrome vuelve tab-stop a un
  // contenedor con overflow-y-auto si NINGUNO de sus hijos es alcanzable por
  // Tab (para poder scrollearlo con flechas), aunque el contenedor no tenga
  // tabindex explícito. Como las opciones usan tabIndex={-1} a propósito (se
  // navegan con flechas, no con Tab), eso metía una parada extra vacía y
  // confusa entre Juzgado y el siguiente campo. tabIndex={-1} explícito en el
  // propio listbox le dice al navegador que no aplique ese heurístico.
  it('DJ-123: el listbox tiene tabIndex=-1 explícito (evita que el navegador lo vuelva tab-stop por ser scrollable)', () => {
    render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesHermosillo} />)

    fireEvent.focus(screen.getByRole('combobox'))

    expect(screen.getByRole('listbox')).toHaveAttribute('tabindex', '-1')
  })

  it('DJ-123: muestra la flecha visual (chevron), igual que SelectConFlecha', () => {
    const { container } = render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesHermosillo} />)

    expect(container.querySelector('svg.lucide-chevron-down')).toBeInTheDocument()
  })

  describe('DJ-123: navegación por teclado', () => {
    it('ArrowDown abre el combobox y resalta la primera opción', () => {
      render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesHermosillo} />)
      const input = screen.getByRole('combobox')

      fireEvent.keyDown(input, { key: 'ArrowDown' })

      expect(screen.getByRole('listbox')).toBeInTheDocument()
      expect(screen.getByRole('option', { name: opcionesHermosillo[0] })).toHaveAttribute('aria-selected', 'true')
    })

    it('ArrowDown repetido avanza la opción resaltada, ArrowUp retrocede', () => {
      render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesHermosillo} />)
      const input = screen.getByRole('combobox')

      fireEvent.keyDown(input, { key: 'ArrowDown' })
      fireEvent.keyDown(input, { key: 'ArrowDown' })
      expect(screen.getByRole('option', { name: opcionesHermosillo[1] })).toHaveAttribute('aria-selected', 'true')

      fireEvent.keyDown(input, { key: 'ArrowUp' })
      expect(screen.getByRole('option', { name: opcionesHermosillo[0] })).toHaveAttribute('aria-selected', 'true')
    })

    it('Enter selecciona la opción resaltada y cierra el combobox', () => {
      const onChange = vi.fn()
      render(<ComboboxCatalogo value="" onChange={onChange} opciones={opcionesHermosillo} />)
      const input = screen.getByRole('combobox')

      fireEvent.keyDown(input, { key: 'ArrowDown' })
      fireEvent.keyDown(input, { key: 'ArrowDown' })
      fireEvent.keyDown(input, { key: 'Enter' })

      expect(onChange).toHaveBeenCalledWith(opcionesHermosillo[1])
      expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
    })

    it('Enter sin haber navegado con flechas, con un solo resultado filtrado, lo selecciona', () => {
      const onChange = vi.fn()
      render(<ComboboxCatalogo value="" onChange={onChange} opciones={opcionesHermosillo} />)
      const input = screen.getByRole('combobox')

      fireEvent.focus(input)
      fireEvent.change(input, { target: { value: 'oral' } })
      fireEvent.keyDown(input, { key: 'Enter' })

      expect(onChange).toHaveBeenCalledWith('1ro Oral Mercantil Hermosillo')
    })

    it('Enter sin navegar y con varios resultados filtrados no selecciona nada (ambiguo)', () => {
      const onChange = vi.fn()
      render(<ComboboxCatalogo value="" onChange={onChange} opciones={opcionesHermosillo} />)
      const input = screen.getByRole('combobox')

      fireEvent.focus(input)
      fireEvent.change(input, { target: { value: 'civil' } })
      fireEvent.keyDown(input, { key: 'Enter' })

      expect(onChange).not.toHaveBeenCalled()
    })

    it('Escape cierra el combobox y revierte texto inválido sin confirmar', () => {
      const onChange = vi.fn()
      render(<ComboboxCatalogo value="1ro Civil Hermosillo" onChange={onChange} opciones={opcionesHermosillo} />)
      const input = screen.getByRole('combobox')

      fireEvent.focus(input)
      fireEvent.change(input, { target: { value: 'texto que no existe' } })
      fireEvent.keyDown(input, { key: 'Escape' })

      expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
      expect(input.value).toBe('1ro Civil Hermosillo')
      expect(onChange).not.toHaveBeenCalled()
    })

    it('ArrowDown con el combobox ya navegado a la "+ Agregar nueva" y Enter la dispara', () => {
      const onAgregarNuevo = vi.fn()
      render(
        <ComboboxCatalogo
          value=""
          onChange={() => {}}
          opciones={opcionesHermosillo}
          onAgregarNuevo={onAgregarNuevo}
          textoAgregarNuevo="+ Agregar juzgado nuevo"
        />
      )
      const input = screen.getByRole('combobox')

      fireEvent.keyDown(input, { key: 'ArrowDown' }) // opción 0
      fireEvent.keyDown(input, { key: 'ArrowDown' }) // opción 1
      fireEvent.keyDown(input, { key: 'ArrowDown' }) // opción 2
      fireEvent.keyDown(input, { key: 'ArrowDown' }) // "+ Agregar juzgado nuevo"
      fireEvent.keyDown(input, { key: 'Enter' })

      expect(onAgregarNuevo).toHaveBeenCalled()
    })

    it('Tab (perder el foco hacia afuera) cierra el combobox y revierte texto inválido, igual que un clic afuera', () => {
      const onChange = vi.fn()
      render(
        <div>
          <ComboboxCatalogo value="1ro Civil Hermosillo" onChange={onChange} opciones={opcionesHermosillo} />
          <button>siguiente campo</button>
        </div>
      )
      const input = screen.getByRole('combobox')

      fireEvent.focus(input)
      fireEvent.change(input, { target: { value: 'texto invalido' } })
      fireEvent.blur(input, { relatedTarget: screen.getByText('siguiente campo') })

      expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
      expect(input.value).toBe('1ro Civil Hermosillo')
      expect(onChange).not.toHaveBeenCalled()
    })

    it('el blur hacia una opción propia del combobox (clic con mouse) no cierra antes de tiempo', () => {
      render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesHermosillo} />)
      const input = screen.getByRole('combobox')

      fireEvent.focus(input)
      const opcion = screen.getByText(opcionesHermosillo[0])
      fireEvent.blur(input, { relatedTarget: opcion })

      // Sigue abierto -- el blur hacia una opción interna no debe cerrar el dropdown
      expect(screen.getByRole('listbox')).toBeInTheDocument()
    })
  })

  it('no muestra "+ Agregar nueva" si no se pasa onAgregarNuevo (usuario no-admin)', () => {
    render(<ComboboxCatalogo value="" onChange={() => {}} opciones={opcionesHermosillo} />)

    fireEvent.focus(screen.getByRole('combobox'))

    expect(screen.queryByText(/Agregar nueva/i)).not.toBeInTheDocument()
  })

  it('muestra "+ Agregar nueva" cuando sí se pasa onAgregarNuevo (admin)', () => {
    render(
      <ComboboxCatalogo
        value=""
        onChange={() => {}}
        opciones={opcionesHermosillo}
        onAgregarNuevo={() => {}}
        textoAgregarNuevo="+ Agregar juzgado nuevo"
      />
    )

    fireEvent.focus(screen.getByRole('combobox'))

    expect(screen.getByText('+ Agregar juzgado nuevo')).toBeInTheDocument()
  })
})
