# Propuestas técnicas pendientes

Documento vivo para mejoras de arquitectura identificadas durante el trabajo normal, que no ameritan resolverse en el momento pero tampoco hay que perder de vista. A diferencia de `mecanica-legal-sonora.md` (mecánicas del mundo legal que el software debe replicar), esto es puramente técnico — decisiones de ingeniería, no de negocio.

## Índice (estado de un vistazo)

| # | Sección | Estado |
|---|---|---|
| 1 | Campo "Juzgado" como catálogo cerrado | Implementada parcialmente — el dropdown fijo sí existe; las dos salvaguardas pedidas (fuente única de verdad, IdUnidad) se decidieron no implementar |
| 2 | Alias de nombres de banco no escala a multi-tenant | Investigada y descartada para el alcance actual (DJ-125) — el patrón para el SaaS sigue sin implementar |
| 3 | Número de orden del juzgado reutilizado entre trámites | Pendiente — solo observación, sin investigar a fondo |
| 4 | Proceso de pull requests y ambientes | Sin decidir |
| 5 | Identificación de nombres con apoyo externo (por decidir, para el SaaS) | Sin decidir |
| 6 | Medir desde el inicio y definir cuándo se entrega (para el SaaS) | Sin decidir |
| 7 | Dos huecos de resiliencia encontrados durante DJ-139 (scraper) | Pendiente — el hueco #2 (sin reintento de notificación) ya se resolvió en el cierre de DJ-141, ver sección 8; el #1 (contador de fallos HTTP) sigue sin investigar |
| 8 | Lecciones del cierre: notificaciones, herramientas de recuperación y Jira (SaaS) | Lecciones técnicas ya aplicadas en DJ-141; decisiones de Jira del cierre sin tomar |
| 9 | DJ-134: candidatos de una palabra en el reporte diario | Implementada — 0 casos reales medidos, no se construyó lista ni umbral; se reporta el texto para revisión manual |
| 10 | DJ-132: aviso de banco faltante no distingue texto suelto de un banco real | Pendiente — dejado así a propósito, baja probabilidad |
| 11 | DJ-132: criterio alterno de matching cuando falta banco (Opción D) | En pausa — requiere investigar rutas viables antes de decidir si se implementa |
| 12 | DJ-106: índices, candado centralizado del scraper y horarios fijos de 4 BackgroundServices | Implementada (22 de octubre de 2026) |

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

## 5. Identificación de nombres con apoyo externo (por decidir, para el SaaS)

**Estado: sin decidir.**

Pregunta: ¿conviene usar código abierto o una API que identifique nombres en español para
ayudar al scraper a distinguir nombres de jerga legal en el campo Partes de ADISON?

Contexto: en Acedo e Hijos el problema real no fue reconocer nombres en general, sino que
frases de trámite (por ejemplo "ACCIÓN PAGO DE PESOS") se tomaron por nombre. Se resolvió con
una lista de frases genéricas medida contra datos reales (DJ-131) y con una detección por
repetición (DJ-133). Para ese despacho no se justifica una herramienta externa.

Por qué no es la solución principal hoy:
- ADISON publica el texto en mayúsculas; los modelos de reconocimiento de nombres se apoyan
  en la capitalización y pierden precisión.
- El lenguaje jurídico se parece poco al de los corpus con que se entrenan esos modelos.
- Un error del modelo (tomar un nombre real por jerga) ocultaría acuerdos sin una regla
  clara que lo explique, y sería más difícil de auditar que una lista.
- Enviar nombres de partes de casos reales a una API externa es un riesgo de
  confidencialidad (datos de clientes de un despacho). Un modelo local evita esto, pero
  exige otro entorno (por ejemplo Python u ONNX) junto a un backend .NET.

Dónde sí puede aportar:
- Un diccionario abierto de nombres y apellidos como señal adicional (no como decisión
  única): determinista, sin servicios externos, auditable. Útil para las palabras sueltas
  de DJ-134 ("GARANTIA" frente a un apellido real).
- El SaaS: con otros despachos y regiones, las frases genéricas no estarán medidas de
  antemano y una señal generalizable valdría más.

Criterios para evaluar cualquier opción: funciona con texto en mayúsculas; corre sin enviar
nombres a servicios externos; se integra con .NET; se mide contra los acuerdos reales
(cuántos nombres reales rechaza, lo más grave, y cuántas frases genéricas deja pasar);
licencia compatible con uso comercial (cuidado con las copyleft como GPL, y verificar la
licencia y la cobertura de los conjuntos de datos).

Opción propuesta: medir un diccionario de apellidos dentro de DJ-134 y retomar la
investigación amplia al arrancar el SaaS.

---

## 6. Medir desde el inicio y definir cuándo se entrega (para el SaaS)

**Estado: sin decidir.**

Lección del cierre de Acedo e Hijos (octubre de 2026): el proyecto se alargó porque los errores
del scraper solo se detectaban cuando el único usuario activo los notaba, y no había criterios
de salida.

Qué hacer desde el primer día del SaaS:
- Registrar un embudo por corrida y por fuente (registros devueltos, coincidencias, guardados
  visibles, ocultos, descartados con su razón), sin datos personales.
- Un reporte diario de auditoría con cola de revisión priorizada (ocultos con coincidencia
  exacta, ocultos parecidos, frases candidatas nuevas, anomalías).
- Clasificar los errores en S1, S2 y S3 desde el principio y decidir qué se corrige antes de
  entregar.
- Fijar criterios de salida medibles por tiempo y volumen (días hábiles sin falsos negativos
  confirmados, mínimo de casos reales revisados con tasa de error máxima, ausencia de
  notificaciones erróneas) y una sola prórroga acotada.
- Congelar el alcance antes de la ventana de observación: solo entra lo que causó un falso
  negativo real o una notificación errónea y se arregla con un cambio pequeño.
- Un ambiente de pruebas con gate de correo desde el principio (ver DJ-135).

Referencias: DJ-138 (plan de cierre), DJ-139, DJ-140 y DJ-143.

---

## 7. Dos huecos de resiliencia encontrados durante DJ-139 (scraper)

**Estado: pendiente — solo observación, sin investigar a fondo.**

Encontrados al diseñar la bitácora de evaluación de DJ-139 (embudo por corrida del scraper).
Ninguno de los dos se arregló ahí porque arreglarlos cambia comportamiento real, no solo
observación — quedan documentados para decidir después si se abren como historias propias.

**1. El contador de fallos consecutivos no cuenta las respuestas HTTP fallidas (solo
excepciones).** `ScraperAcuerdosService._fallosConsecutivosPorJuzgado` solo se incrementa
cuando `ScrapearJuzgadoAsync` lanza una excepción real (timeout, DNS). Una respuesta HTTP no-2xx
(403, 500, etc.) hoy se trata como "el juzgado no tuvo acuerdos ese día" — nunca incrementa el
contador, así que un juzgado que responda sistemáticamente con error **nunca** dispara la
alerta crítica de "3 fallos consecutivos", sin importar cuántos días lleve fallando.

**2. Un fallo de Resend al notificar deja el acuerdo guardado pero `NotificacionEnviada` en
`false` para siempre, sin ningún reintento.** En `EjecutarScrapingAsync`, si
`EnviarNotificacionAsync` lanza (ver DJ-135: `EmailService.EnviarAsync` lanza una excepción si
Resend falla), el acuerdo ya se guardó (`SaveChangesAsync` ya se ejecutó antes de la línea que
notifica) pero la línea que marca `NotificacionEnviada = true` nunca se alcanza — el acuerdo
queda guardado y visible, pero sin notificación, sin que nada lo reintente ni lo señale.

> **Resuelto en el cierre de DJ-141 (18 de octubre de 2026):** se midió esta categoría en
> retrospectiva (11 casos reales en 60 días, sin rastro de la causa exacta porque la bitácora
> de excepciones no existía todavía cuando pasaron) y se construyó `ReintentoNotificacionesService`
> — reintento automático + alerta al equipo si vuelve a fallar. Ver sección 8 para las lecciones
> de ese mismo cierre, incluyendo un incidente real que el reintento causó antes de corregirse.

---

## 8. Lecciones del cierre: notificaciones, herramientas de recuperación y Jira (SaaS)

**Estado: lecciones técnicas ya aplicadas en el cierre de DJ-141 (18 de octubre de 2026); decisiones de Jira del propio cierre sin tomar — ver última viñeta.**

- Todo servicio que envíe correos de forma automática o por lotes debe tener desde el primer día: un tope de antigüedad, la exclusión explícita de lo que se dejó sin notificar a propósito, un tope de correos por corrida y por destinatario con alerta al equipo si se excede, y una corrida en modo de prueba que muestre cuántos correos saldrían antes de activarlo. Incidente (18 de octubre de 2026, Acedo e Hijos): el reintento de notificaciones envió 34 correos tardíos de un backfill histórico a dos litigantes porque no excluía ese backfill. Se corrigió con un tope de antigüedad y se avisó a los litigantes.
- Las herramientas de recuperación manual (promover, sugerir, reintentar) deben decidir explícitamente si notifican, dejar rastro de quién las usó y probarse primero con dryRun.
- Medir los errores con la misma definición que el criterio de salida. S1 incluye acuerdos ocultos y acuerdos detectados pero no notificados; contar solo los ocultos subestimó la tasa (3.4% frente a 8.1%).
- Un dry-run debe revisar si el registro ya existe; uno que cuenta todo como nuevo infla las vistas previas.
- Las decisiones de Jira (cambiar estatus, cerrar o crear tickets, asignar responsables) las toman las personas del equipo, no las herramientas de apoyo, salvo petición explícita. Decisiones pendientes de este cierre: actualizar el estatus de DJ-134 con lo implementado (ver sección 9), cerrar DJ-120 si el caso original quedó resuelto, crear tickets para el servicio de reintento y para los apellidos invertidos, y asignar un responsable de seguimiento a los 3 acuerdos sugeridos.

---

## 9. DJ-134: candidatos de una palabra en el reporte diario

**Estado: implementada (19 de octubre de 2026).**

Diagnóstico medido antes de decidir nada (consulta SQL replicando la lógica de
`PartesTieneNombre`/`EsPalabraUnica` contra todo el histórico de Oculto=Baja de
Hermosillo, sin aplicar el umbral de 3 expedientes): **0 candidatos de una sola
palabra** en todo el histórico — consistente con el hallazgo independiente de
DJ-137 (vista previa real de candidatos a frase genérica, 0 de cualquier tipo en
el mismo periodo).

Con 0 casos reales, ninguna de las opciones que construyen infraestructura de
antemano (lista en configuración, umbral o exclusión especial de apellidos) tenía
evidencia que la respaldara — hacerlo hoy habría sido exactamente lo que el
congelamiento de alcance de la semana 1 prohíbe (sección 8 del `CLAUDE.md`: solo
entra lo que causó un falso negativo real). Se descartó también diferir sin
ningún cambio: el hueco real no era "falta una lista de apellidos", era que
`EsPalabraUnica` (`FrasesGenericasCandidatasService.cs`) excluía estas palabras
de raíz de la cola de candidatos — nunca llegaban a revisión humana por ningún
camino, así que si el caso empezara a ocurrir mañana nadie se enteraría.

**Decisión final:** no tratarlas automáticamente (siguen sin poder ocultarse,
promoverse ni notificarse distinto por ser de una palabra), pero sí mostrarlas
en el reporte diario de auditoría (`ReporteAuditoriaScraperService`) para que el
equipo técnico decida caso por caso. Primer intento: solo un número
("N excluidos por palabra suelta, revisar") — insuficiente, porque sin el texto
nadie puede decidir si es jerga o un apellido real. Se corrigió a mostrar el
texto completo, en una sección aparte y claramente marcada como revisión manual
(nunca mezclada con los candidatos normales de 2+ palabras), bajo la misma
excepción reconocida de privacidad que ya cubre a esos candidatos normales
("el reporte diario de auditoría muestra el texto de los candidatos... solo
puede ir a destinatarios del equipo técnico" — sección 3 del `CLAUDE.md`).

**Lección:** un conteo sin texto parece la opción "más privada" pero en la
práctica no sirve para nada si el objetivo es que una persona revise — la
excepción de privacidad del reporte diario ya existe justamente para este tipo
de caso, y aplica igual a un candidato de una palabra que a uno de dos.

---

## 10. DJ-132: el aviso de banco faltante no distingue texto suelto de un banco real

**Estado: pendiente — dejado así a propósito, baja probabilidad de que importe en la práctica.**

El aviso no bloqueante agregado en `SelectorBancoOParticular.jsx` (DJ-132, Hipotecario/Oral
Mercantil sin banco ni parte actora particular) decide si mostrarse mirando solo si la casilla de
Banco tiene **algún texto escrito** (`!banco`), no si ese texto corresponde a un banco real del
catálogo (`bancosOpciones`).

**Caso donde falla:** un litigante escribe un fragmento en la casilla de banco (ej. "BBV") sin
llegar a seleccionar la opción de la lista ni a terminar de escribir el nombre completo, y guarda
así. El aviso no aparece (hay texto), pero al guardar el backend busca un banco que se llame
exactamente ese texto, no lo encuentra, y guarda el expediente con `BancoId = null` de todas
formas (`bancos.find(b => b.nombre === form.banco)?.id ?? null`, en `NuevoExpediente.jsx` y
`EditarExpediente.jsx`). El expediente queda exactamente en la situación que el aviso buscaba
prevenir, sin que nadie lo haya visto.

**Por qué se dejó así:** es un aviso informativo (S3, no bloquea guardar ni corrompe datos) — en
el peor caso falla en avisar justo en el caso donde más serviría, pero no genera ningún daño
nuevo. Es poco probable en el uso normal (la mayoría selecciona de la lista desplegable en vez de
teclear texto suelto sin elegir nada), y no se midió ningún caso real de esto ocurriendo.

**Corrección pendiente, si se retoma:** comparar `banco` contra `bancosOpciones` (o el booleano que
ya usa `ComboboxCatalogo` para saber si el valor actual es una opción válida) en vez de solo
chequear que no esté vacío.

---

## 11. DJ-132: criterio alterno de matching cuando falta banco (Opción D)

**Estado: en pausa — no decidida, no descartada. Requiere investigación antes de retomarla.**

Del listado de 4 opciones que planteó DJ-132 para expedientes sin banco ni parte actora
particular (ver sección 2 del ticket y la Fase 2 de su medición), la Opción D era la única que
resolvía automáticamente los acuerdos ya afectados (ej. los 5 casos reales encontrados en la
medición) sin esperar a que el despacho complete el dato: un criterio de matching alterno para
cuando el expediente no tiene banco capturado.

**Por qué no se implementó ni se investigó a fondo:** a diferencia de B y C (aviso no bloqueante,
implementadas) y A (campaña de datos, no es tarea técnica), D sí es un cambio real de
comportamiento de matching — exige su propio commit con casos reales (regla de la sección 6 del
`CLAUDE.md`) y, sin saber a cuál banco específico pertenece el expediente, se parece
estructuralmente al riesgo ya documentado en la sección 3 de este mismo documento (número de
orden del juzgado reutilizado entre trámites ajenos): cualquier regla que confíe en "¿el acuerdo
menciona algún banco del catálogo?" sin poder verificar contra el banco correcto de ese
expediente específico corre el riesgo de confundir un caso ajeno con el caso real, sobre todo
dado que BBVA México por sí solo ya es el 78% del portafolio activo (dato de DJ-122).

**Decisión explícita:** no se prioriza ni se descarta — queda pendiente de una investigación
previa (qué rutas de matching alterno son viables sin reintroducir ese riesgo) antes de decidir
si entra siquiera como historia. No hay fecha ni compromiso de retomarla.

---

## 12. DJ-106: índices, candado centralizado del scraper y horarios fijos de 4 BackgroundServices

**Estado: implementada (22 de octubre de 2026), versión ligera con tope de un día, per plan de cierre.**

Diagnóstico (solo lectura, `EXPLAIN` contra producción + carga local con SQLite y datos sintéticos, nunca contra producción) en tres áreas: costo del polling de `/no-vistos`, consultas del reporte diario, y solapamiento de tareas programadas. Ninguna de las tres mostró un riesgo urgente al volumen actual (~5,248 acuerdos) — todas las consultas medidas en sub-2ms — pero se encontraron 4 huecos estructurales de bajo riesgo, aprobados para corregir ya:

1. **Índice compuesto en `AcuerdosScrapeados` (`Oculto`, `Confianza`, `FechaDetectado`).** Ninguna de las tres tablas clave del scraper tenía índice secundario — todas las consultas hacían *seq scan* completo. Barato hoy, pero `AcuerdosScrapeados` es la única de las tres sin retención (a diferencia de `BitacoraEvaluacionScraper`, 180 días, y `BitacoraDescarteItemizado`, 60 días) — crece sin tope. Un solo índice compuesto cubre tanto `/no-vistos` (solo filtra por `Oculto`, usa el prefijo izquierdo) como el reporte diario (filtra por los tres campos). Migración aditiva.

2. **Candado centralizado del scraper.** El `SemaphoreSlim` que evita que el ciclo automático y una corrida manual le peguen a ADISON a la vez cubría solo 2 de 5 rutas reales — `/api/scraper/ejecutar`, `/ejecutar-rango` y `/colisiones-juzgado/{id}/reintentar` llamaban a `EjecutarScrapingAsync` directo, sin candado. Se agregó `EjecutarScrapingConCandadoAsync` como único punto de entrada real (los 5 llamadores de producción ahora pasan por ahí) — `EjecutarScrapingAsync` sigue público porque 5 archivos de prueba lo llaman directo (sin concurrencia real en pruebas, no hay riesgo).

3. **Margen del reporte diario vs. ciclo del scraper.** 18:30 (solo 25 min después de que cierra la ventana del scraper a las 18:05) ampliado a 19:00 (55 min de colchón), por si un ciclo se alarga por reintentos/timeouts.

4. **4 BackgroundServices anclados a la hora de arranque de la app, no a hora fija.** `ReintentoNotificacionesService`, `RecordatorioAcuerdosPendientesService`, `RevisionFechasService` y `RecordatorioCitasService` usaban `do/while(timer.WaitForNextTickAsync())` — su horario real dependía de a qué minuto exacto arrancó/redesplegó la app, no de un horario elegido. Dos de los cuatro (`RevisionFechasService`, `RecordatorioCitasService`) envían correo real a litigantes, así que el riesgo no era solo "podría coincidir con el scraper" sino "podría tocarle correo real a las 3am" según cuándo cayera el último deploy. Horarios fijos Hermosillo, elegidos para no coincidir entre sí ni con el scraper (00:05–18:05) ni con el reporte (19:00):
   - `RecordatorioAcuerdosPendientesService`: 19:00 (decisión explícita del equipo, mismo bloque que el reporte)
   - `RevisionFechasService`: 08:00 (envía correo real — horario laboral)
   - `RecordatorioCitasService`: 08:15 (envía correo real — horario laboral)
   - `ReintentoNotificacionesService`: sigue cada hora (no se cambia a una vez al día — su propósito es reintentar rápido), pero ahora anclado a la hora en punto (`ProximaCorridaAlineada`) en vez de arrastrar el offset del deploy. El primer intento inmediato al arrancar se conserva a propósito (documentado en el propio código: resuelve de inmediato cualquier backlog tras un deploy).

**Hallazgo colateral, no parte de esta historia:** al revisar por qué el correo del reporte diario seguía mostrando Cola 2b sin agrupar (el fix de la sección de arriba, DJ-140, ya estaba comiteado y pusheado desde el 20 de octubre), se confirmó que el último deploy de producción (19 de octubre, 16:46 MST) es **anterior** a ese commit — nada de lo pusheado desde entonces (DJ-132, DJ-140, y esta misma historia) está corriendo en producción todavía. No es un bug de código; es que el despliegue automático no se disparó o se pausó. Pendiente de que el equipo redespliegue manualmente.

**Pruebas:** 536 → 539 (3 nuevas para `ProximaCorridaAlineada`), sin regresión. Migración `DJ106_IndiceAcuerdosOcultoConfianzaFecha` generada y revisada (puramente aditiva).

**Qué NO cubre:** no se midió con tráfico concurrente real contra producción (prohibido para esta tarea); no se investigó el costo de un futuro canal push/websocket; la observabilidad del scraper (bitácora/purga) se revisó a fondo y se confirmó que ya está correctamente aislada — no requirió cambios.

---

## 13. DJ-144: PartesCoinciden fallaba con apellidos invertidos

**Estado: implementada (22 de octubre de 2026).**

Pendiente desde el cierre de DJ-141 (sección 8, "crear tickets... para los apellidos invertidos"). El propio código ya tenía, sin conectar, una función que resolvía esto (`CoincidenMismasPalabrasOrdenDistinto`, comentada como "PROPUESTA DJ-apellidos-invertidos" desde antes de este ticket) con un caso real documentado ahí mismo: exp. 43/2024, "Pedro Nuñez Mendez" vs "PEDRO MENDEZ NUÑEZ" — similitud 0.6667 contra el umbral 0.8 de `PartesCoinciden`, que compara el nombre completo como una sola cadena contigua y por eso es sensible al orden.

**Medición antes de decidir** (nombres inventados, ninguno real): 9 casos sintéticos corriendo `PartesCoinciden` (la función real) y la propuesta sin conectar, vía reflection contra el ensamblado real. Los 3 casos de apellidos invertidos puros fallaban con la función real y pasaban con la propuesta; los 2 controles negativos (personas distintas) daban `False` en ambas, sin diferencia.

**Hallazgo al wirear la propuesta:** romper la suite expuso un bug latente en `CoincidenMismasPalabrasOrdenDistinto` que nunca se había detectado porque nunca corrió contra datos reales — usaba `Contains` de subcadena cruda en vez de límite de palabra, así que "CORONA" contaba como presente dentro de "CORONADO" (son personas distintas; cuando sí son la misma, esa variación de ortografía ya la cubre `SimilitudMaximaSubcadena` con su propio umbral). Esto rompió 2 pruebas existentes (`VariacionDeOrtografia_DaPuntajeAltoSinSerUno`, `PartesCoinciden_UmbralMasAlto_YaNoAceptaLaVariacionDeOrtografia`) — la segunda porque el nuevo criterio ignoraba `umbralSimilitud` por completo. Se corrigió a exigir límite de palabra (`\b...\b`) antes de wirear nada a producción.

**Cambio:** `CoincidenMismasPalabrasOrdenDistinto` ahora se llama dentro de `NombreIndividualCoincide` (en `PartesCoinciden`) y de `PuntajeIndividual` (en `PuntajeMaximoSimilitud`, para que el reporte de Cola 2b de DJ-140 no marque como "casi coincide" algo que ya cuenta como match real).

**Qué NO cubre:** el caso compuesto donde ADEMÁS de invertir los apellidos falta una palabra completa del nombre (ej. se omitió un segundo nombre) — `CoincidenMismasPalabrasOrdenDistinto` exige el conjunto completo de palabras, así que esto sigue dando `False`. Es un problema distinto (nombre incompleto, no solo orden); si se decide atacar, va en un ticket aparte.

**Pruebas:** 540 → 547 (7 nuevas), sin regresión.

---

*Última actualización: 22 de octubre de 2026.*
