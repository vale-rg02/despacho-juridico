---
name: mecanica-legal
description: Agrega un hallazgo nuevo a docs/mecanica-legal-sonora.md, el documento vivo del proyecto despacho-juridico que captura mecánicas del sistema judicial de Sonora (tipos de juicio, ADISON, notificaciones, plazos, etc.) que el software necesita replicar correctamente. Úsala cada vez que el usuario diga "agrega esto al documento de mecánica legal", "documenta este hallazgo", "anota esta mecánica", "esto hay que meterlo al documento legal" — o, sin que lo pida explícitamente, cuando durante el trabajo se descubra que el software se equivocó (o casi se equivoca) por no entender correctamente un procedimiento o concepto legal real (ej. un tipo de juicio con reglas propias, un plazo que se cuenta distinto, una figura procesal que el código no contempla). En ese caso, señala el hallazgo y ofrece documentarlo.
---

# Documentar mecánica legal

`docs/mecanica-legal-sonora.md` no es una bitácora de bugs — es la traducción, en lenguaje que el software pueda usar, de cómo funciona de verdad el sistema judicial que este proyecto tiene que modelar. Cada entrada existe porque un caso real reveló que el código asumía algo sobre el mundo legal que no era cierto. El valor está en capturar la mecánica (la regla del mundo real), no la anécdota del bug — la próxima persona que lea el documento necesita poder diseñar correctamente a partir de él, sin haber vivido el caso que lo originó.

## Antes de escribir

Lee `docs/mecanica-legal-sonora.md` completo (si no existe, créalo usando el mismo encabezado/intro que ya tiene la primera entrada como plantilla — pregúntale al usuario si no hay ninguna entrada previa de la que copiar el tono). Fíjate en:

- El número de la última entrada, para saber qué número le toca a la nueva.
- El estilo real de las entradas existentes — no reinventes el formato, sigue el que ya está.

## Qué reunir antes de redactar

De la conversación (o preguntando si falta algo), necesitas cuatro piezas. Si te falta alguna, pregunta — no la inventes ni la dejes vacía sin decir que quedó pendiente:

1. **La mecánica legal en sí**, explicada en términos generales, no solo el caso puntual — como si nunca fuera a volver a pasar exactamente igual, pero sí algo parecido.
2. **Dónde truena en el software**: qué asume el código (nombra el archivo/función si se identificó) que la mecánica contradice.
3. **El caso real que lo destapó**: expediente, fecha, y evidencia concreta (el texto exacto de ADISON, el campo de la BD, lo que sea que lo haga verificable — no una paráfrasis vaga).
4. **Qué falta decidir o construir**: si ya se decidió una solución, escríbela. Si no, dilo explícitamente como pendiente — no te inventes una solución que nadie propuso.

## Formato de la entrada

Sigue exactamente esta estructura (ya establecida en el documento):

```markdown
## N. Título corto de la mecánica

**La mecánica:** ...

**Dónde truena en el software:** ...

**Caso real que lo destapó (fecha):**
...

**Qué falta decidir / construir:** *(pendiente — no se ha diseñado la solución)* o la decisión ya tomada
- ...
```

Después de agregar la entrada:
- Suma el link correspondiente al índice al inicio del documento (`[N. Título](#n-título-corto-de-la-mecánica)`).
- Actualiza la línea `*Última actualización: [fecha de hoy].*` al final del archivo.

## Después de escribir

Muéstrale al usuario la entrada que agregaste (no solo digas "listo, lo agregué" — enséñasela, igual que revisarías cualquier otro cambio) y pregunta si algo necesita ajuste antes de darlo por cerrado. No hagas `git commit` a menos que el usuario lo pida explícitamente.
