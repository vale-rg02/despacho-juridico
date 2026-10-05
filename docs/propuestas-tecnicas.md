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

## 2. Alias de nombres de banco en el matching del scraper no escala a multi-tenant (DJ-125)

**El problema:** `MencionaBancoOAlias` (en `ScraperAcuerdosService.cs`) resuelve que ADISON nombre a un banco distinto a su razón social (ej. "Bancomer" en vez de "BBVA México") con un `if` puntual hardcodeado en código C#, validado caso por caso con datos reales antes de desplegarlo. Funciona para Acedo e Hijos hoy porque el despacho tiene pocos bancos y el equipo puede medir y revisar cada alias a mano.

Ese enfoque dejaría de funcionar en un producto multi-tenant:
- Cada despacho cliente trae su propio portafolio de bancos, desconocido de antemano — no se puede precargar una lista fija de alias que cubra a todos.
- Desde DJ-105 (y reafirmado al abrir "Agregar banco" a cualquier usuario autenticado), cualquier usuario puede agregar un banco nuevo al catálogo sin que el equipo de desarrollo se entere — un alias hardcodeado en código nunca podría mantenerse al día con bancos agregados en producción después del último deploy.
- Agregar un `if` nuevo en el código por cada alias de cada despacho no escala operativamente (requiere un deploy por alias).

**Patrones de solución a futuro** *(solo para anotar, no decidir ni construir ahora)*:

1. **Catálogo de alias por banco, editable desde el panel.** Agregar un campo (ej. `Banco.NombresAlternativos`, lista de strings) que el usuario capture directamente — razón social, nombre comercial, variantes conocidas — sin pasar por el equipo de desarrollo ni un deploy.
   - *Riesgo:* depende de que el usuario del despacho sepa y se tome el tiempo de capturar los alias correctos; un alias mal puesto (muy corto o genérico) puede generar falsos positivos tan fácilmente como uno hardcodeado mal medido — ver el riesgo #2 más abajo, que aplica igual aquí.

2. **Matching tolerante a variantes, en el espíritu de `PartesCoinciden`/`SimilitudMaximaSubcadena` (Levenshtein) pero aplicado al nombre del banco**, en vez de depender de una lista de alias explícita.
   - *Riesgo:* un nombre comercial corto puede coincidir por accidente con texto ajeno sin relación — el mismo patrón de falso positivo ya documentado en DJ-79 para nombres de personas ("ANA" coincidiendo dentro de "LILIANA"). Para nombres de banco el riesgo es mayor aún porque las razones sociales mexicanas comparten fragmentos comunes ("Banco Nacional de...", "Banco X de México"). Cualquier implementación necesitaría un mínimo de longitud del patrón (como ya hace `EsNombreConfiable` para nombres de personas, que exige 2+ palabras) para no activar con fragmentos triviales.

Ambos patrones son compatibles entre sí (un catálogo de alias explícito + tolerancia a variaciones de ortografía dentro de cada alias) y no son mutuamente excluyentes — pero cualquiera de los dos es una decisión de producto nueva, no una extensión directa de lo que existe hoy.

**Qué falta decidir / construir:** *(pendiente — no se ha decidido si/cuándo se aborda; depende de si el negocio avanza hacia multi-tenant)*

---

## 3. El "número de expediente" que usa el scraper para encontrar coincidencias en realidad es el número de orden del juzgado, reutilizado entre trámites ajenos

**El problema:** el matching de `ScraperAcuerdosService` busca coincidencias de `AcuerdosScrapeados` contra los expedientes del despacho por número + juzgado. Al investigar DJ-125 (alias de bancos) se encontró que ese número, en varios juzgados, no identifica un solo caso — es el **número de orden** correlativo que el juzgado asigna a todo lo que recibe (cuadernillos, exhortos, legajos, oficios), reutilizado entre trámites completamente ajenos entre sí.

**Evidencia real (6 oct 2026):** al revisar los acuerdos reales ligados al expediente 108/2026 (Scotiabank, demandado Alma Melissa Salazar Gracia), entre los resultados aparecían divorcios, sucesorios, juicios de amparo y demandas de terceros sin ninguna relación con ese expediente ni ese banco — todos comparten el mismo número 108/2026 en el mismo juzgado, simplemente porque les tocó ese número de orden en algún momento. El mismo patrón se repitió en varios de los expedientes revisados ese día (24/2026, 242/2026, 299/2026, 300/2026, 325/2026, 347/2026, 363/2026, 57/2026, entre otros).

**Por qué no causó problemas en la medición de DJ-125:** `PartesCoinciden` sigue comparando contra `ParteDemandada`, así que estos acuerdos ajenos no pasaron como coincidencias reales — el ruido queda filtrado por el nombre, no por el número. Pero es ruido real que vive en `AcuerdosScrapeados`, y cualquier lógica futura que confíe más en número+juzgado que en el nombre de las partes (ej. un matching menos estricto, o un alias de banco demasiado permisivo) heredaría este riesgo de falsos positivos sin darse cuenta.

**Qué falta decidir / construir:** *(solo observación, no se investigó más a fondo — pendiente si alguien quiere profundizar)*
- Confirmar si es un patrón de TODOS los juzgados o solo de algunos (los ejemplos encontrados fueron foráneos, no de Hermosillo).
- Medir qué tan seguido genera "ruido" real en `AcuerdosScrapeados` (cuántas filas por expediente son del caso real vs. de otros trámites).
- Si el volumen de ruido crece, considerar si vale la pena dejar de guardar los acuerdos que no matchean por nombre, en vez de guardarlos todos bajo el mismo número.

---

*Última actualización: 6 de octubre de 2026.*
