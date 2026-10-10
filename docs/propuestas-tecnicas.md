# Propuestas técnicas pendientes

Documento vivo para mejoras de arquitectura identificadas durante el trabajo normal, que no ameritan resolverse en el momento pero tampoco hay que perder de vista. A diferencia de `mecanica-legal-sonora.md` (mecánicas del mundo legal que el software debe replicar), esto es puramente técnico — decisiones de ingeniería, no de negocio.

## Índice (estado de un vistazo)

| # | Sección | Estado |
|---|---|---|
| 1 | Campo "Juzgado" como catálogo cerrado | Implementada parcialmente — el dropdown fijo sí existe; las dos salvaguardas pedidas (fuente única de verdad, IdUnidad) se decidieron no implementar |
| 2 | Alias de nombres de banco no escala a multi-tenant | Investigada y descartada para el alcance actual (DJ-125) — el patrón para el SaaS sigue sin implementar |
| 3 | Número de orden del juzgado reutilizado entre trámites | Pendiente — solo observación, sin investigar a fondo |
| 4 | Proceso de pull requests y ambientes | Sin decidir |

---

## 1. Campo "Juzgado" del expediente como catálogo cerrado, no texto libre

**Estado: Implementada parcialmente — el dropdown de catálogo fijo sí existe (DJ-87, DJ-112, DJ-122, DJ-123; commits `5f881a7`, `35f461b`, `d432ad8`), pero las dos salvaguardas que pedía la propuesta original (fuente única de verdad, guardar IdUnidad) se decidieron explícitamente NO implementarlas — ver nota abajo.**

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

> **Nota de verificación (contra el código real, no contra memoria):** el dropdown de Sede + Juzgado dependiente existe y está en producción (`JuzgadoCatalogo`/`SedeCatalogo`, DJ-87/DJ-112, commit `5f881a7`), con filtro por Materia (DJ-122, commit `35f461b`) y navegación por teclado (DJ-123, commit `d432ad8`) — así que la pregunta de si "también cubre Sede" ya quedó resuelta: sí. Pero el campo `Expediente.Juzgado` **sigue siendo texto plano**, no una FK ni un `IdUnidad` — es una decisión documentada a propósito en el propio código: el comentario de `JuzgadoCatalogo.cs` dice literalmente que es *"una copia deliberada, no una lectura en vivo"* del diccionario del scraper, *"para no arriesgar la lógica de matching ya afinada en varios tickets"*. Es decir: las dos salvaguardas que pedía esta propuesta para que el dropdown no reintrodujera el problema un nivel arriba **se evaluaron y se decidió no tomarlas**, no que se les haya olvidado. Esto significa que el catálogo de UI y el diccionario del scraper son hoy dos listas separadas que alguien tiene que mantener sincronizadas a mano — el riesgo que la propuesta original quería evitar sigue latente, solo que ahora es un riesgo conocido y aceptado en vez de uno no contemplado. La migración retroactiva sí se hizo (`MigrarSedeYJuzgadoDesdeTextoLibreAsync`), aunque es best-effort (expedientes que no se pudieron mapear con confianza quedan con `Sede`/`Juzgado` en null en vez de forzar un valor incorrecto).

---

## 2. Alias de nombres de banco en el matching del scraper no escala a multi-tenant (DJ-125)

**Estado: Investigada y descartada para el alcance actual (DJ-125, commit `f908a46`, 4 de octubre de 2026) — se midió con datos reales si hacía falta repetir el alias de Bancomer para otros bancos del portafolio y no se encontró evidencia que lo justificara. El patrón de solución para el SaaS (catálogo de alias editable / matching tolerante) sigue sin implementar — ver índice.**

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

> **Nota de verificación:** `MencionaBancoOAlias` hoy (commit `f908a46`) sigue teniendo exactamente un alias hardcodeado (Bancomer → BBVA México, confirmado con 13 de 48 menciones reales). El comentario en el propio código documenta que se investigó expandirlo a Scotiabank, Banco Nacional de México, HSBC, Banco Azteca y Santander, y en ningún caso se encontró el mismo patrón que justificó el de Bancomer (ADISON nombrando solo al banco, sin el demandado) — en los casos con datos suficientes, `PartesCoinciden` ya resuelve el match por el nombre del demandado sin necesitar alias. No hubo cambios de código en esa investigación, solo documentación — la propuesta de catálogo editable / matching tolerante para el SaaS sigue intacta, sin empezar.

---

## 3. El "número de expediente" que usa el scraper para encontrar coincidencias en realidad es el número de orden del juzgado, reutilizado entre trámites ajenos

**Estado: Pendiente — solo observación documentada el 4 de octubre de 2026 (commit `f908a46`, junto con la investigación de DJ-125), sin investigación adicional desde entonces.**

**El problema:** el matching de `ScraperAcuerdosService` busca coincidencias de `AcuerdosScrapeados` contra los expedientes del despacho por número + juzgado. Al investigar DJ-125 (alias de bancos) se encontró que ese número, en varios juzgados, no identifica un solo caso — es el **número de orden** correlativo que el juzgado asigna a todo lo que recibe (cuadernillos, exhortos, legajos, oficios), reutilizado entre trámites completamente ajenos entre sí.

**Evidencia real (6 oct 2026):** al revisar los acuerdos reales ligados al expediente 108/2026 (Scotiabank, demandado Alma Melissa Salazar Gracia), entre los resultados aparecían divorcios, sucesorios, juicios de amparo y demandas de terceros sin ninguna relación con ese expediente ni ese banco — todos comparten el mismo número 108/2026 en el mismo juzgado, simplemente porque les tocó ese número de orden en algún momento. El mismo patrón se repitió en varios de los expedientes revisados ese día (24/2026, 242/2026, 299/2026, 300/2026, 325/2026, 347/2026, 363/2026, 57/2026, entre otros).

**Por qué no causó problemas en la medición de DJ-125:** `PartesCoinciden` sigue comparando contra `ParteDemandada`, así que estos acuerdos ajenos no pasaron como coincidencias reales — el ruido queda filtrado por el nombre, no por el número. Pero es ruido real que vive en `AcuerdosScrapeados`, y cualquier lógica futura que confíe más en número+juzgado que en el nombre de las partes (ej. un matching menos estricto, o un alias de banco demasiado permisivo) heredaría este riesgo de falsos positivos sin darse cuenta.

**Qué falta decidir / construir:** *(solo observación, no se investigó más a fondo — pendiente si alguien quiere profundizar)*
- Confirmar si es un patrón de TODOS los juzgados o solo de algunos (los ejemplos encontrados fueron foráneos, no de Hermosillo).
- Medir qué tan seguido genera "ruido" real en `AcuerdosScrapeados` (cuántas filas por expediente son del caso real vs. de otros trámites).
- Si el volumen de ruido crece, considerar si vale la pena dejar de guardar los acuerdos que no matchean por nombre, en vez de guardarlos todos bajo el mismo número.

---

## 4. Proceso de pull requests y ambientes (por decidir)

Contexto: en el proyecto de Acedo e Hijos, `develop` se despliega a producción y `main` recibe un PR grande cada cierto tiempo, sin revisión por historia ni pruebas automáticas en el PR. Esto permitió que pruebas en rojo pasaran sin notarse y que producción cambiara con cada push.

Para el SaaS, definir desde el inicio:
- Ramas cortas por historia, con la clave de Jira en el nombre, y un PR revisado por el otro desarrollador.
- Pruebas automáticas (backend y frontend) en cada PR, como requisito para fusionar.
- Protección de ramas: sin push directo a main (ni a develop si se conserva).
- Ambientes: main como producción y develop desplegado a un servicio de pruebas aparte.

Cuidados del ambiente de pruebas: el scraper y los correos no pueden avisar a usuarios reales (interruptor de correo, scraper en dry-run), usar datos de ejemplo en vez de datos reales de clientes, y considerar el costo del segundo servicio.

Estado: sin decidir.

> **Sub-nota — estado real de hoy:** no hay ningún workflow de CI activo en el repositorio — la carpeta `.github/workflows/` existe pero está vacía, sin un solo archivo dentro (verificado directamente en el repo). Las 345 pruebas de backend y las 84 de frontend existen y pasan, pero hoy solo se ejecutan manualmente, nunca como compuerta automática antes de fusionar o desplegar. No tuve acceso en esta sesión para confirmar por API si `develop` tiene reglas de protección de rama configuradas en GitHub o un pipeline en Railway — lo que sí se puede confirmar es el patrón observado durante el trabajo real de varias sesiones: cada `git push` a `develop` resulta en un redeploy a producción prácticamente inmediato, sin ningún paso de revisión o aprobación intermedio que se haya notado. Tampoco hay evidencia de un segundo ambiente de pruebas separado — todo el trabajo de esta sesión apuntó a una sola base de datos/API que el equipo trata como producción. Es decir: de los cuatro puntos de la lista de arriba, ninguno tiene evidencia de estar implementado — este documento es, hasta ahora, la única forma en que el tema quedó registrado. Vale la pena confirmar la configuración exacta de GitHub/Railway directamente (branch protection rules, environments) antes de dar esto por hecho al 100%.

---

*Última actualización: 8 de octubre de 2026.*
