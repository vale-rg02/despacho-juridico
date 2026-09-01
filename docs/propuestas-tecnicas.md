# Propuestas técnicas pendientes

Documento vivo para mejoras de arquitectura identificadas durante el trabajo normal, que no ameritan resolverse en el momento pero tampoco hay que perder de vista. A diferencia de `mecanica-legal-sonora.md` (mecánicas del mundo legal que el software debe replicar), esto es puramente técnico — decisiones de ingeniería, no de negocio.

---

## 1. Campo "Juzgado" del expediente como catálogo cerrado, no texto libre

**El problema:** `Expediente.Juzgado` es texto libre. Cada persona que captura un expediente puede escribir el nombre del juzgado con su propia redacción — con o sin "de lo", con typos ("Mecantil"), con o sin el sufijo "Hermosillo", etc. `JuzgadoCoincide` (en `ScraperAcuerdosService.cs`) depende de comparar ese texto libre contra patrones hardcodeados, así que cualquier variante de redacción no contemplada hace que el matching falle **en silencio** — el acuerdo simplemente nunca se guarda, sin error visible en ningún lado.

**Caso real que lo evidenció (1 de septiembre de 2026):** 28 expedientes de Mario capturados como `"SEGUNDO ORAL DE LO MERCANTIL"` (en vez de `"SEGUNDO ORAL MERCANTIL"`) nunca recibieron ningún acuerdo automático — el "DE LO" de más rompía el match. Se corrigió el patrón puntual, pero el problema de fondo (texto libre) sigue ahí para la siguiente variante de redacción que alguien escriba.

**Propuesta:** convertir el campo Juzgado en un selector de opciones fijas (dropdown), en vez de texto libre — así deja de existir la posibilidad de escribir una variante que rompa el match.

Dos puntos importantes para que la solución no reintroduzca el mismo problema en otra forma:

1. **Una sola fuente de verdad.** El dropdown debe alimentarse del mismo diccionario `Juzgados` que ya usa el scraper (`ScraperAcuerdosService.cs`) — idealmente expuesto vía un endpoint que el frontend consuma directo. Si el dropdown se arma con una lista escrita a mano por separado, se corre el riesgo de que las dos listas se desincronicen entre sí (el mismo problema, un nivel arriba).
2. **Mejor aún que comparar texto exacto:** una vez que el campo esté controlado, considerar guardar el `IdUnidad` (el identificador numérico que usa ADISON) en vez de/junto con el nombre — el match dejaría de depender de comparar strings por completo, y se volvería inmune a cualquier variación de redacción de forma permanente.

**Alcance:** esto resuelve la ruta de matching de juzgados de Hermosillo (la que usa `JuzgadoCoincide`). Los juzgados foráneos ya matchean solo por número de expediente, sin comparar el nombre del juzgado — no les afecta ni para bien ni para mal.

**Qué falta decidir / construir:** *(pendiente — no se ha decidido si/cuándo se aborda)*
- No es retroactivo: los expedientes ya capturados con texto libre (incluyendo datos de prueba tipo "asaasa", "kk") seguirían necesitando `JuzgadoCoincide` tal como está hoy, a menos que también se haga una migración de datos.
- Falta decidir si el dropdown también cubre "Sede" (ciudad/distrito) o solo el juzgado en sí.

---

*Última actualización: 1 de septiembre de 2026.*
