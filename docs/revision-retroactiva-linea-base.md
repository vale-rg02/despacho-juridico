# Revisión retroactiva — línea base de falsos negativos y falsos positivos (DJ-141)

**Estado: COMPLETO (18 de octubre).** Partes A, B y C completas. Las 8 recuperaciones de S1 ya se ejecutaron en producción — ver "Estado de las recuperaciones". Quedan fuera de esta historia, para seguimiento aparte: el fix del algoritmo de apellidos invertidos (candidato identificado, no implementado), [DJ-120](https://manueldjah.atlassian.net/browse/DJ-120) (notificación nunca enviada, ticket separado) y las dos notas "a futuro" de la sección de recuperaciones.

Regla seguida en todo el documento: nunca se cita texto de `Partes` ni nombres de clientes — solo Id de acuerdo/expediente, número de expediente, juzgado, fecha, categoría y puntaje de similitud.

---

## Resumen ejecutivo

- **Tasa de falsos negativos estimada (Cola 1 + Cola 2 combinadas):** 1.9% a 30 días, 3.4% a 60 días (cota inferior — ver advertencia en Parte A).
- **Tasa de S2 estimada:** 0% observado, pero el intervalo de confianza real es 0%–8.8% (n=40, Clopper-Pearson 95%).
- **8 candidatos reales de S1** encontrados en Cola 1 (Hermosillo), **los 8 ya recuperados en producción** (3 `Sugerir`, 1 `Promover` con correo, 4 `Promover` sin correo). **2 de los 8 (25%) son exhorto** — sobrerrepresentados frente al ~8.6% de tasa base.
- **0 candidatos de S1** en Cola 2 (foráneos) de 2,132 revisados.
- **1 hallazgo de algoritmo nuevo:** el matching no prueba el orden invertido de apellidos (caso real confirmado).
- **4 expedientes con captura duplicada** encontrados en toda la base (no es bug de scraper, es limpieza de datos).
- **2 bugs de `JuzgadoCoincide`** encontrados y corregidos aparte durante este mismo trabajo (commit `80635a7`, separado de esta historia por ser cambio de matching real) — sin impacto retroactivo en esta ventana porque no hay expedientes reales en esos dos juzgados.
- **Fallas completas de scrapeo descartadas como causa:** 0 de 3,108 corridas fallaron en 60 días.
- **Categoría no auditada, descubierta por cruce con Jira:** acuerdos correctamente detectados cuya notificación nunca se envió — no cubierta por Parte A ni B. El ticket [DJ-120](https://manueldjah.atlassian.net/browse/DJ-120) de esta categoría sigue abierto 39+ días.
- **3 candidatos de exhorto (1999, 1888, 1541) confirmados como ruido real** (materia distinta, patrón DJ-98) — no eran casos mal clasificados.
- **Parte C (publicación tardía): completa.** Mecanismo confirmado real (4to Familiar Hermosillo sumó 26 resultados nuevos a una fecha ya pasada), pero bajo impacto medido — 0 misses reales en la pasada hacia atrás (10 días hábiles × 29 juzgados de Hermosillo). En el camino se encontró y corrigió un bug real que inflaba cualquier conteo de dry-run del sistema (no revisaba si el acuerdo ya existía) — afectaba también `/auditoria-preview`.

---

## Parte A — Revisión retroactiva de acuerdos ocultos

### Metodología

Se consultó `AcuerdosScrapeados` en producción (solo lectura) filtrando `Oculto=true, Confianza='Baja'`, separado en Hermosillo (Cola 1, ventana de 60 días) y foráneos (Cola 2, ventana de 30 días — se empezó en 60 días pero el volumen obligó a acotar, como ya preveía el plan original). Para Cola 1 se clasificó cada candidato aplicando la misma lógica de `JuzgadoCoincide` ya corregida. Para Cola 2 se calculó el puntaje real de similitud (`PuntajeMaximoSimilitud`) con un script corrido localmente contra producción, que nunca expuso texto de `Partes` — solo Id, número, juzgado y puntaje.

### Cola 1 — Hermosillo (60 días, 61 candidatos crudos)

| Clasificación | Cantidad |
|---|---|
| Ruido conocido (materia distinta — mismo patrón de DJ-98) | 49 |
| Colisión real de número confirmada (mismo expediente, dos clientes distintos bajo el mismo número+juzgado) | 4 |
| **Propuesta de S1** | **8** |

**Los 8 propuestos como S1** (acuerdo ya guardado, solo oculto por falta de nombre en `Partes` — recuperable con `Promover`):

| Id acuerdo | Expediente | Juzgado | Fecha acuerdo | Antigüedad (al 18-oct) | Causa exacta |
|---|---|---|---|---|---|
| 6640 | 704/2025 | 2do Oral Mercantil Hermosillo | 09-oct | 9 días | ADISON no publicó ningún nombre, solo el nombre del actor |
| 5059 | 130/2026 | 3ro Civil Hermosillo | 22-sep | 26 días | ADISON no publicó ningún nombre |
| 4665 | 78/2026 | 2do Oral Mercantil Hermosillo (corregido el 18-oct; se había anotado "1ro" por error) | 14-sep | 34 días | ADISON solo nombró al actor, no al demandado registrado — **es exhorto** (confirmado el 18-oct, no se había anotado antes) |
| 4586 | 67/2026 | 1ro Oral Mercantil Hermosillo | 11-sep | 37 días | ADISON no publicó ningún nombre |
| 4506 | 462/2026 | 2do Oral Mercantil Hermosillo | 11-sep | 37 días | ADISON no publicó ningún nombre |
| 3746 | 462/2026 | 2do Oral Mercantil Hermosillo | 08-sep | 40 días | ADISON no publicó ningún nombre |
| 3354 | 72/2026 | 2do Oral Mercantil Hermosillo | 02-sep | 46 días | ADISON no publicó ningún nombre (solo origen del exhorto) |
| 3189 | 400/2026 | 2do Oral Mercantil Hermosillo | 11-ago (detectado 01-sep) | 58 días | ADISON no publicó ningún nombre — **además detectado 21 días después de la fecha del acuerdo (ver Parte C)** |

**La colisión real confirmada** (4 acuerdos, mismo expediente, excluidos de S1): un expediente del despacho (juzgado "3ro Civil Hermosillo") comparte número con un caso de otro despacho/cliente totalmente ajeno, en el mismo juzgado exacto — confirmado comparando el `ParteDemandada` registrado contra el texto real de `Partes` de cada uno de los 4 acuerdos, que corresponde a un asunto de pago de honorarios sin ninguna relación con el cliente del despacho. Es evidencia directa de que número+juzgado, aunque coincidan los dos, no siempre garantiza que sea el mismo caso.

### Repaso adicional por `EsExhorto` (agregado el 18-oct)

Al re-correr la consulta de Cola 1 separando por `EsExhorto`, salieron **5 candidatos de exhorto de 58 totales** (53 no-exhorto + 5 exhorto — el total de 58 en vez de 61 es por el corrimiento natural de la ventana de 60 días entre la corrida original y esta, no un error de filtro):

| Id acuerdo | Expediente | Juzgado | Fecha detectado | ¿Ya estaba en los 8 S1? |
|---|---|---|---|---|
| 4665 | 78/2026 | 2do Oral Mercantil Hermosillo | 14-sep | Sí — corregido arriba |
| 3354 | 72/2026 | 2do Oral Mercantil Hermosillo | 02-sep | Sí — ya estaba anotado como exhorto |
| 1999 | 325/2026 | 2do Tribunal Laboral Hermosillo | 25-ago | **No — confirmado ruido** (expediente real es "2do Oral Mercantil Hermosillo", Oral Mercantil; ADISON lo cruzó con un juzgado Laboral — materia distinta, patrón DJ-98) |
| 1888 | 137/2025 | 3er Tribunal Laboral Hermosillo | 24-ago | **No — confirmado ruido** (expediente real es "1ro Oral Mercantil Hermosillo", Oral Mercantil) |
| 1541 | 137/2025 | 3er Tribunal Laboral Hermosillo | 19-ago | **No — confirmado ruido** (mismo expediente que 1888, mismo motivo) |

**2 de los 8 S1 (25%) son exhorto**, contra ~8.6% de tasa base entre los 58 candidatos — los exhortos están sobrerrepresentados entre los falsos negativos reales, consistente con que ADISON tiende a publicar menos datos de `Partes` en exhortos. Los 3 candidatos restantes (1999, 1888, 1541) se confirmaron como ruido real (materia distinta, patrón DJ-98) el 18-oct — no eran casos mal clasificados.

### Cola 2 — Foráneos (30 días, 2,132 candidatos crudos)

Solo **1 de 2,132** cayó dentro de la banda de similitud 0.65-0.80 (puntaje 0.65, justo en el borde). Al revisarlo, resultó ser una coincidencia de apellido común ("Ibarra") entre dos personas distintas — no el mismo cliente. **0 propuestas de S1** de Cola 2.

### Duplicados de captura encontrados (hallazgo aparte, no es bug de scraper)

Al revisar las colisiones de número+juzgado contra la propia tabla `Expedientes`, se encontraron **4 expedientes capturados dos veces** en la base (mismo cliente, variante de ortografía del nombre entre las dos capturas):

| Número | Juzgado | Ids duplicados |
|---|---|---|
| 149/2026 | 3ro Civil Hermosillo | 1077, 1233 (uno Cerrado) |
| 300/2026 | 1ro Oral Mercantil Hermosillo | 1242, 1243 (ambos Abierto) |
| 68/2026 | Arrendamiento Hermosillo | 1217, 1226 (ambos Abierto) |
| 726/2023 | 2do Oral Mercantil Hermosillo | 1181, 1249 (ambos Abierto) |

Con al menos 2 de los 4 pares ambos "Abierto", el scraper evalúa el mismo acuerdo real dos veces por cada copia — contribuye ruido extra, no afecta la clasificación de S1 encontrada arriba. Se revisó toda la base (no solo Hermosillo) y no aparecieron duplicados adicionales en juzgados foráneos.

### Fallas completas de scrapeo (agregado el 18-oct — causa descartada)

Se revisó si alguna corrida automática falló por completo (`ConsultaExitosa = false` en `BitacoraEvaluacionScraper`), lo que habría significado un día entero sin datos para ese juzgado sin reintento. **0 fallas de 3,108 corridas en los últimos 60 días.** Esta causa queda descartada — no contribuye a los falsos negativos de esta ventana.

### Tasa estimada de falsos negativos

- **A 30 días (ventanas consistentes):** 2 S1 (los únicos de los 8 detectados en los últimos 30 días) / (99 Alta + 3 Media + 2 S1) = **1.9%**
- **A 60 días** (asumiendo que Cola 2 se mantiene igual de rara en los 30 días adicionales — supuesto, no medido en esa ventana completa): 8 S1 / (214 Alta + 14 Media + 8 S1) = **3.4%**

**Advertencia:** es una cota inferior. No captura la clase de acuerdos que nunca llegaron a guardarse en absoluto (ver Parte C).

---

## Parte B — Línea base de S2 (notificaciones erróneas)

### Descartes confirmados por litigante (60 días)

**0 casos reales** — el único registro encontrado tenía número de expediente "0", claramente un dato de prueba, no una notificación real descartada.

### Confianza Media sin confirmar ni descartar (60 días)

14 en total:
- **2 recientes** (2-3 días, recordatorio automático aún no enviado) — normal, sin señal de alerta.
- **12 atrasados** (27-59 días, con recordatorio ya enviado y aún sin resolver) — concentrados en un mismo litigante (10 de los 12). Al revisar el texto de cada uno: **13 de los 14 son correctos por diseño** (el texto de ADISON solo menciona al banco, nunca al demandado registrado — exactamente para lo que se diseñó "Media"). Es un patrón de backlog de revisión, no un problema de datos.
- **1 hallazgo real de algoritmo:** un acuerdo donde el nombre completo del demandado SÍ aparece en el texto de ADISON, pero con los dos apellidos en **orden invertido** respecto a como está capturado en `ParteDemandada` (ej. "Apellido1 Apellido2" vs "Apellido2 Apellido1"). El algoritmo de comparación de texto no prueba la combinación invertida, así que nunca sube a "Alta" aunque sea con alta probabilidad el mismo cliente. Expediente 1042/2014, acuerdo Id 4663, litigante distinto a los otros 13 (Id 2, no Id 5).

### Muestra de 40 acuerdos Alta notificados (60 días)

Se revisó si algún (número, juzgado) se repetía bajo un `ExpedienteId` distinto dentro de la muestra — señal directa de una notificación ya enviada a un caso ajeno. **0 de 40** mostró esa señal; todos los números repetidos en la muestra correspondían siempre al mismo expediente real.

### Tasa estimada de S2

**0% observado, pero con intervalo de confianza real de 0%–8.8%** (Clopper-Pearson 95%, n=40, 0 hallazgos — corregido el 18-oct; "≈0%" sin este margen subestimaba la incertidumbre). No se puede descartar una tasa real de S2 de hasta casi 9% con esta muestra.

### Categoría no cubierta: acuerdo detectado pero notificación nunca enviada (agregada el 18-oct)

Parte A y B cubren "oculto por confianza baja" y "notificado a caso ajeno" — ninguna de las dos cubre el tramo donde el acuerdo SÍ se clasificó bien (Alta) pero el correo de notificación nunca salió. Un cruce contra los reportes reales de litigantes en Jira (Epic [DJ-113](https://manueldjah.atlassian.net/browse/DJ-113)) muestra que esta categoría existe y ninguno de sus casos coincide con los 8 S1 de Parte A:

| Ticket | Litigante | Expediente(s) | Fecha | Estado |
|---|---|---|---|---|
| [DJ-121](https://manueldjah.atlassian.net/browse/DJ-121) | Mario | 428/26, 793/25, 401/26, 476/26 | 10-sep | Resuelto — el reporte confirma que el acuerdo SÍ apareció en la lista de acuerdos (detección correcta), solo el correo no llegó |
| [DJ-130](https://manueldjah.atlassian.net/browse/DJ-130) | Mario | 363/2026 | 08-oct | Resuelto |
| [DJ-120](https://manueldjah.atlassian.net/browse/DJ-120) | Carlos | 159/2025 | 09-sep | **Abierto — "En revisión", sin resolución 39+ días después** |
| [DJ-117](https://manueldjah.atlassian.net/browse/DJ-117) | Mario | 242/26 | 29-ago | Resuelto |
| [DJ-116](https://manueldjah.atlassian.net/browse/DJ-116) | Mario | 534/25 | 29-ago | Resuelto |

Esta revisión retroactiva (DJ-141) no mide esta categoría — ninguna de las consultas de Parte A/B la cubre. Queda fuera de alcance de este documento, pero **DJ-120 sigue abierto** y merece seguimiento aparte, independiente de esta historia.

---

## Parte C — Prueba de publicación tardía de ADISON (EN PROGRESO)

**Hipótesis:** el ciclo automático solo consulta la fecha de hoy, nunca vuelve a preguntar fechas pasadas — si ADISON publica un acuerdo días después de su fecha real, el scraper nunca lo ve.

### Indicio ya encontrado en la Parte A

El acuerdo Id 3189 (expediente 400/2026, Cola 1) tiene `FechaAcuerdo` 11 de agosto pero `FechaDetectado` 1 de septiembre — **21 días de diferencia**. Es evidencia preliminar consistente con la hipótesis, encontrada de paso, no como parte de la medición sistemática de esta sección.

### Prueba hacia adelante (punto 2 del plan) — estado

Se confirmó que la bitácora de D=16 de octubre solo tiene 7 corridas automáticas ese día (de 12:05pm a 6:05pm hora de Hermosillo), no las ~19 esperadas de un día completo — **explicación identificada, no es una anomalía**: DJ-139 (el código que escribe esta bitácora) se desplegó ese mismo día a medio día, así que la bitácora solo capturó las corridas posteriores al deploy. La última corrida real del día (18:05 hora de Hermosillo) sí quedó completa y es la que se usa para comparar.

**Línea base recolectada para D=16 de octubre** (conteo `Devueltos` de la última corrida real del día, acotado a los 12 juzgados de los expedientes activos del despacho):

| IdUnidad | Juzgado | Devueltos (última corrida, 16-oct) |
|---|---|---|
| 152 | 1ro Civil Hermosillo | 94 |
| 153 | 2do Civil Hermosillo | 69 |
| 154 | 3ro Civil Hermosillo | 71 |
| 156 | 2do Familiar Hermosillo | 138 |
| 158 | 1ro Mercantil Hermosillo | 90 |
| 173 | 1ro Oral Mercantil Hermosillo | 47 |
| 174 | Arrendamiento Hermosillo | 16 |
| 296 | 4to Familiar Hermosillo | 79 |
| 300 | 2do Oral Mercantil Hermosillo | 54 |
| 133 | Juzgado Mixto Cananea | 33 |
| 183 | Juzgado Mixto Magdalena | 37 |
| 188 | Juzgado 1ro Civil Nogales | 46 |

**Reconsulta completada (18 de octubre, vía `POST /api/scraper/ejecutar?fecha=2026-10-16&dryRun=true`):**

| IdUnidad | Juzgado | Devueltos (16-oct, ciclo automático) | Devueltos (16-oct, reconsultado 18-oct) | Diferencia |
|---|---|---|---|---|
| 152 | 1ro Civil Hermosillo | 94 | 94 | — |
| 153 | 2do Civil Hermosillo | 69 | 69 | — |
| 154 | 3ro Civil Hermosillo | 71 | 71 | — |
| 156 | 2do Familiar Hermosillo | 138 | 138 | — |
| 158 | 1ro Mercantil Hermosillo | 90 | 90 | — |
| 173 | 1ro Oral Mercantil Hermosillo | 47 | 47 | — |
| 174 | Arrendamiento Hermosillo | 16 | 16 | — |
| 296 | 4to Familiar Hermosillo | 79 | **105** | **+26** |
| 300 | 2do Oral Mercantil Hermosillo | 54 | 54 | — |
| 133 | Juzgado Mixto Cananea | 33 | 33 | — |
| 183 | Juzgado Mixto Magdalena | 37 | 37 | — |
| 188 | Juzgado 1ro Civil Nogales | 46 | 46 | — |

**Hallazgo confirmado:** 11 de los 12 juzgados dieron exactamente el mismo conteo dos días después — pero "4to Familiar Hermosillo" (IdUnidad 296) creció de 79 a 105. **Esto confirma el mecanismo de la hipótesis: ADISON sí sigue agregando resultados a una fecha ya publicada**, no es estático. Se revisó el embudo completo de ese juzgado para ver si los 26 adicionales tocaban algo real: `evaluaciones=105, descartadoSinCandidato=100, descartadoNumeroJuzgadoNoCoincide=5, guardadoVisibleAlta=0, guardadoVisibleMedia=0, guardadoOcultoBaja=0` — cuadra exacto (100+5=105) y **ninguno de los 105 (ni los originales ni los 26 nuevos) coincidió con un expediente real del despacho**. Conclusión para esta fecha/juzgado: el mecanismo de publicación tardía es real, pero en este caso específico no causó ningún falso negativo para el despacho — es ruido de casos ajenos que ADISON sigue sumando a ese día.

### Pasada hacia atrás (punto 1 del plan)

**Metodología:** reconsultar en dry-run los 10 días hábiles del 5 al 18 de octubre, los 29 juzgados de Hermosillo completos (misma lista que Parte A), vía `POST /api/scraper/ejecutar?fecha=X&dryRun=true` (se intentó primero con `/ejecutar-rango` pero esa ruta metía 5 días en una sola petición HTTP y el proxy de Railway la cortaba con 502 antes de terminar — se cambió a una llamada por día).

**Bug real encontrado y corregido en el camino (18-oct):** el primer intento dio resultados que no cuadraban (48 "nuevos" en 10 días, mucho más que los 8 de toda la Parte A). La causa: en `EjecutarScrapingAsync`, el chequeo de "¿ya existe este acuerdo?" vivía *después* del `if (dryRun) {...; continue;}` — así que en dry-run ese chequeo nunca corría, y **cualquier acuerdo que coincidiera contaba como "nuevo guardado" aunque llevara semanas ya capturado**. Esto afecta cualquier vista previa de dry-run del sistema, no solo esta prueba (incluye `/auditoria-preview`, de uso diario del equipo). Se corrigió moviendo el chequeo de `yaExiste` antes del branch de `dryRun` ([ScraperAcuerdosService.cs:608](backend/DespachoJuridico.API/Services/ScraperAcuerdosService.cs:608)), con una prueba de regresión nueva. Desplegado y verificado: al repetir la misma pasada después del fix, los 48 "nuevos" originales pasaron a contarse correctamente como `yaExistian` (6, 7, 3, 5, 8, 0, 5, 5, 3, 6 — idéntico al conteo viejo), y `nuevosEncontrados` dio **0 en los 10 días**.

**Resultado final de la pasada hacia atrás:** 0 misses reales en 10 días hábiles × 29 juzgados de Hermosillo.

### Conclusión de Parte C

El mecanismo de publicación tardía de ADISON **es real** — confirmado en la prueba hacia adelante (4to Familiar Hermosillo sumó 26 resultados nuevos a una fecha ya pasada) — pero **de bajo impacto medido**: ni en ese caso puntual ni en los 10 días de la pasada hacia atrás se encontró un solo caso real del despacho perdido por esta causa. No se descarta que exista en otras ventanas no probadas, pero no hay evidencia de que sea una fuente significativa de falsos negativos para este despacho hoy.

---

## Causas priorizadas (evaluadas contra la regla de congelamiento)

| Causa | Tipo de cambio | ¿Entra por congelamiento? |
|---|---|---|
| El diseño exige nombre de parte en `Partes` para confianza Alta, incluso con número+juzgado exacto coincidiendo | Decisión de diseño, no bug — causó los 8 S1 de Cola 1 | **No** — cualquier cambio (ej. confiar en número+juzgado solo) es un cambio real de comportamiento con riesgo de S2 ya demostrado (la colisión real de 150/2023 tenía número+juzgado exactos también). Requiere decisión explícita del equipo, no un "cambio pequeño". El camino ya construido (`Promover` caso por caso) es la mitigación actual. |
| El algoritmo no prueba el orden invertido de apellidos al comparar nombres | Cambio chico y acotado (agregar una comparación adicional en `PartesCoinciden`) | **Sí, candidato real** — 1 caso real confirmado, cambio pequeño, bajo riesgo |
| 4 expedientes con captura duplicada en la base | Limpieza de datos, no cambio de código | No aplica la regla de congelamiento (no es código) — recomendación operativa: fusionar o cerrar la copia sobrante |
| Expediente "Pausado" sigue en el grupo de matching activo (solo "Cerrado" se excluye) | Posible cambio de diseño | Documentado para decisión futura, fuera de alcance de esta historia (ver hallazgo del expediente 150/2023 original, no confundir con la colisión de número — son hallazgos distintos) |
| Bugs de `JuzgadoCoincide` (Violencia de Género, Tribunal Unitario) | Ya corregidos | Ya resuelto, comiteado aparte (`80635a7`) por ser cambio de matching real, no observación. Sin impacto retroactivo medido en esta ventana. |

---

## Estado de las recuperaciones

Para destrabar esto se construyeron dos capacidades nuevas en `AcuerdosController` (18-oct), ambas con `dryRun=true` por defecto:

- **`PATCH /api/acuerdos/{id}/sugerir`** — hermano de `Promover`, pero sube el acuerdo a `Confianza=Media` en vez de `Alta`. Reusa el correo de "posible acuerdo, confirma o descarta" que ya existía para cualquier Media (DJ-122) — el litigante decide, no se le dice que es un hecho confirmado. Se le corrigió un bug antes de desplegarlo: sin el fix, `RecordatorioAcuerdosPendientesService` habría mandado un segundo correo de seguimiento al día siguiente (cuenta los días desde `FechaDetectado`, que seguía con la fecha original de hace semanas) — ya resuelto, con prueba de regresión.
- **`PATCH /api/acuerdos/{id}/promover?notificar=false`** — para cuando el litigante ya está al tanto del caso por otro medio (ej. un acuerdo posterior sobre el mismo expediente que ya se le notificó). Agrega el acuerdo a la base y lo marca `Confianza=Alta` sin mandar ningún correo. `NotificacionEnviada` se queda en `false` (refleja la realidad — no salió correo — y no hay ningún proceso que lo reintente después).

**Ejecutadas las 8 en producción el 18-oct** (vista previa con `dryRun=true` confirmada contra destinatario esperado, luego ejecución real — las 8 regresaron `200`):

| Id | Expediente | Camino | Motivo | Resultado |
|---|---|---|---|---|
| 6640 | 704/2025 | `Sugerir` | — | Sugerido, correo enviado a Carlos Arturo Acedo Jr. |
| 4665 | 78/2026 | `Sugerir` | Duda genuina sobre si es correcto (señalada al revisar el contenido real) | Sugerido, correo enviado a Mario Acedo |
| 3354 | 72/2026 | `Sugerir` | — | Sugerido, correo enviado a Carlos Arturo Acedo Jr. |
| 4506 | 462/2026 | `Promover` (con correo) | De los dos acuerdos nuevos de este expediente, este es el que se notifica | Promovido, correo enviado a Mario Acedo |
| 3746 | 462/2026 | `Promover?notificar=false` | Mismo expediente que 4506 — litigante ya al tanto por ese otro acuerdo, no se duplica el correo | Promovido sin notificar |
| 5059 | 130/2026 | `Promover?notificar=false` | Litigante ya está al tanto del status del expediente por un acuerdo posterior ya notificado y registrado | Promovido sin notificar |
| 4586 | 67/2026 | `Promover?notificar=false` | Litigante ya enterado por otro medio | Promovido sin notificar |
| 3189 | 400/2026 | `Promover?notificar=false` | Litigante ya enterado por otro medio | Promovido sin notificar |

Las 8 quedaron visibles en sus expedientes y fuera de Cola 1. Los 3 `Sugerir` ahora dependen de que el litigante los confirme o descarte desde el sistema — no se les da seguimiento aparte en este documento, siguen el flujo normal de Media (incluye el recordatorio pasivo a partir de 3 días si no se resuelven).

### Pendientes a futuro (agregados el 18-oct, fuera de alcance de esta historia)

- **Mecanismo para "litigante ya enterado por otro acuerdo"**: los 4 casos de `notificar=false` de arriba son un parche puntual (un parámetro en `Promover`), no una solución general. Falta investigar y definir un mecanismo propio del scraper para detectar este patrón automáticamente (acuerdo oculto cuyo expediente ya tuvo un acuerdo posterior visible/notificado) en vez de decidirlo a mano caso por caso.
- **Plantilla de correo para recuperaciones tardías**: `Promover` y `Sugerir` reusan la misma plantilla que una detección del día — para acuerdos que se notifican con semanas de retraso (como varios de los 8 de aquí), falta definir si conviene una plantilla distinta que deje claro que es una recuperación tardía y no un hallazgo de hoy.

---

## Cómo repetir cada medición (para el manual DJ-142)

**Candidatos Cola 1 (Hermosillo):**
```sql
SELECT a."Id" AS acuerdo_id, a."NumeroExpediente", a."IdUnidad", a."NombreJuzgado" AS juzgado_adison,
       e."Juzgado" AS juzgado_expediente, a."FechaAcuerdo", a."FechaDetectado", a."TipoAsunto", a."EsExhorto"
FROM "AcuerdosScrapeados" a JOIN "Expedientes" e ON e."Id" = a."ExpedienteId"
WHERE a."Oculto" = true AND a."Confianza" = 'Baja'
  AND a."IdUnidad" IN (152,153,154,155,156,157,158,159,160,161,174,175,276,277,296,905,173,300,162,163,164,166,205,171,178,208,322,332,333)
  AND a."FechaDetectado" >= now() - interval '60 days'
ORDER BY a."FechaDetectado" DESC;
```
Clasificar cada fila comparando `juzgado_adison` contra `juzgado_expediente` con la lógica de `JuzgadoCoincide` (código real, no replicar a mano).

**Candidatos Cola 2 (foráneos) — conteo y similitud:** ver conteo con la misma consulta cambiando `NOT IN` por la lista de Hermosillo. Para el puntaje real de similitud, usar un script C# que llame `ScraperAcuerdosService.PuntajeMaximoSimilitud(ParteDemandada, Partes)` por cada fila, conectado con una variable de entorno a la base de producción (nunca compartir la cadena de conexión ni el texto de `Partes` fuera de la terminal local) — filtrar a la banda `Auditoria:BandaSimilitudMinima`/`Maxima` configurada.

**Duplicados de captura (toda la base):**
```sql
SELECT "NumeroExpediente", "Juzgado", count(DISTINCT "Id") AS copias, array_agg("Id") AS ids
FROM "Expedientes" GROUP BY "NumeroExpediente", "Juzgado" HAVING count(DISTINCT "Id") > 1;
```

**Descartes y Media pendiente (Parte B):**
```sql
-- Descartes
SELECT a."Id", a."NumeroExpediente", a."NombreJuzgado", a."Confianza", a."FechaDetectado", e."UsuarioAsignadoId"
FROM "AcuerdosScrapeados" a JOIN "Expedientes" e ON e."Id" = a."ExpedienteId"
WHERE a."DescartadoManualmente" = true AND a."FechaDetectado" >= now() - interval '60 days';

-- Media sin resolver
SELECT a."Id", a."NumeroExpediente", a."NombreJuzgado", a."FechaDetectado", e."UsuarioAsignadoId", a."RecordatorioPendienteEnviado"
FROM "AcuerdosScrapeados" a JOIN "Expedientes" e ON e."Id" = a."ExpedienteId"
WHERE a."Confianza" = 'Media' AND a."DescartadoManualmente" = false AND a."FechaDetectado" >= now() - interval '60 days';

-- Muestra de Alta
SELECT a."Id", a."NumeroExpediente", a."NombreJuzgado", a."IdUnidad", a."FechaDetectado", e."UsuarioAsignadoId", e."Juzgado" AS juzgado_expediente
FROM "AcuerdosScrapeados" a JOIN "Expedientes" e ON e."Id" = a."ExpedienteId"
WHERE a."Confianza" = 'Alta' AND a."NotificacionEnviada" = true AND a."FechaDetectado" >= now() - interval '60 days'
ORDER BY random() LIMIT 40;
```

**Prueba de publicación tardía (Parte C, prueba hacia adelante):**
```sql
-- Última corrida real del día D, por juzgado
SELECT DISTINCT ON ("IdUnidad") "IdUnidad", "NombreJuzgado", "FechaConsultada", "CorridaId", "FechaHoraCorrida", "Devueltos"
FROM "BitacoraEvaluacionScraper"
WHERE "FechaConsultada" = '<fecha D>' AND "Origen" = 'Automatica'
ORDER BY "IdUnidad", "FechaHoraCorrida" DESC;
```
Comparar contra una reconsulta dry-run real a ADISON para la misma fecha D, corrida fuera del horario del ciclo automático (después de 18:05 o antes de 00:05 hora de Hermosillo), con pausa real entre peticiones.

---

*Última actualización: 18 de octubre de 2026 — documento en progreso, Parte C pendiente de completar.*
