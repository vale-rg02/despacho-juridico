# Investigación: detección automática de frases genéricas por repetición (DJ-131, Parte 2)

**Tipo:** Investigación, sin cambios de código. Complementa la Parte 1 (ver `ScraperAcuerdosService.cs`, lista `FrasesGenericasConocidas`).

**Alcance de las consultas:** todas de solo lectura (`SELECT`), corridas por el usuario contra producción el 2026-10-12. No se modificaron datos ni código a partir de esta investigación.

**Nota sobre nombres:** este documento no cita ningún nombre real de parte, litigante o cliente. Donde el hallazgo depende de un nombre real, se describe la categoría ("un nombre de persona", "una razón social") sin reproducir el valor. Los expedientes se citan solo por número cuando es necesario.

---

## 1. Resumen ejecutivo

- El universo de acuerdos actualmente ocultos por confianza baja es de **4,262** (de 4,600 acuerdos scrapeados en total) — esto es el estado normal esperado, no un conteo de errores: la mayoría de los acuerdos que ADISON publica no pertenecen a clientes de este despacho.
- Dentro de ese universo, agrupar por la frase candidata (el último segmento de `Partes` tras el guion) y exigir que se repita en **3 o más expedientes distintos** separa con precisión jerga legal de nombres reales: **0 falsos positivos medidos** en 48 frases que cruzan ese umbral, frente a **3 de 19** (≈16%) cuando el umbral se baja a 2 expedientes.
- La regla, de haber existido, habría encontrado automáticamente la frase de DJ-130 ("ACCIÓN PAGO DE PESOS") con evidencia suficiente mucho antes de los ~2 meses que tardó en reportarse humanamente — aunque no es posible reconstruir la fecha exacta con las consultas corridas (ver sección 3).
- **Recomendación: sí vale la pena implementarla, pero solo como aviso/reporte periódico, nunca como ocultamiento o promoción automática.** El valor principal no es "arreglar" los acuerdos ya ocultos (la gran mayoría están ocultos correctamente, por razones ajenas a este bug) sino detectar más rápido la próxima frase genérica nueva que el respaldo de "2+ palabras largas" vuelva a confundir con un nombre.

---

## 2. Qué se midió y con qué alcance

Tres consultas de solo lectura contra `AcuerdosScrapeados`, `Expedientes` y `Bancos` (ver `parte2_investigacion.sql`):

1. Total de acuerdos con `Oculto = true AND Confianza = 'Baja'`.
2. De ese mismo subconjunto: candidato (último segmento tras el guion, excluyendo filas con los conectores "VS"/"A BIENES DE"/"PROMOVIDO POR"/"EN CONTRA DE"), agrupado y normalizado (mayúsculas, sin acentos), con conteo de apariciones, expedientes distintos, y fecha mínima/máxima de detección — filtrado a candidatos con 2+ expedientes distintos.
3. Mismo candidato, pero cruzado contra `Expedientes`/`Bancos` para medir cuántos de los candidatos repetidos corresponden a un banco real registrado (riesgo de bloquear una referencia legítima).

**Limitación importante de esta medición, no de la regla propuesta:** el conteo de "expedientes distintos" en las consultas 2 y 3 es sobre `NumeroExpediente` solo, sin considerar `NombreJuzgado`. DJ-130 ya documentó que el mismo número de expediente (363/2026) existe en los dos juzgados Orales Mercantiles de Hermosillo — es decir, esta métrica puede inflar artificialmente la "distinción" de un candidato cuando en realidad el número se repite entre juzgados por el mismo caso o por coincidencia de numeración. Ninguno de los candidatos de riesgo (nombres reales, ver sección 4) se vio beneficiado por este efecto de forma que cambiara la conclusión, pero una implementación real de la regla debe contar expedientes distintos como el par (`NumeroExpediente`, `NombreJuzgado`), no solo el número.

**Lo que no se preservó ni se intentó reconstruir:** la muestra de 32 casos de DJ-107 (ya se documentó como no preservada); y el estado exacto de la tabla en cada fecha pasada — los 13 casos originales de DJ-130 ya se corrigieron con el `UPDATE` directo antes de esta consulta, así que la consulta 2 refleja el estado *posterior* a esa corrección, no el histórico completo.

---

## 3. ¿Habría detectado DJ-130, y cuándo?

La frase "ACCIÓN PAGO DE PESOS" aparece en la consulta 2 con **6 apariciones en 6 expedientes distintos**, detectada entre el **17 de agosto** y el **28 de septiembre de 2026** — y esto es *después* de que los 13 casos originales (reportados desde el 4 de agosto) ya fueran corregidos y por tanto ya no aparecen en este conteo (dejaron de estar `Oculto=true/Confianza='Baja'`).

Con los datos disponibles se puede afirmar con confianza:
- Para el 17 de agosto (13 días después de la primera aparición conocida, el 4 de agosto) la frase ya tenía suficiente repetición entre expedientes distintos para cruzar un umbral de 3.
- Esto es semanas, no los ~2 meses que tardó el reporte humano de Mario (8 de octubre).

Lo que **no** se puede afirmar con los datos que se corrieron: la fecha exacta en que se habría cruzado el umbral de 3 expedientes distintos por primera vez, porque eso requiere una consulta adicional ordenada cronológicamente por expediente (y la de este reporte está contaminada por la corrección ya aplicada). Si se quiere ese dato preciso, se puede correr una cuarta consulta — no se corrió aquí para no inventar una cifra que no se midió.

---

## 4. Riesgos medidos (no supuestos)

### 4.1 Nombres/entidades reales que se repiten entre expedientes — **riesgo real, medido**

En el umbral de 2 expedientes distintos aparecieron **3 candidatos que son nombres reales de personas o de una razón social** (dos nombres de persona distintos, una razón social), de un total de 19 candidatos en ese umbral — una tasa de falsos positivos de **≈16%**.

En el umbral de 3 expedientes distintos, de 48 candidatos, **ninguno** es un nombre real — todos son terminología procesal (tipos de juicio, trámites, estados del acuerdo). Tasa de falsos positivos: **0% medido**.

Esto confirma empíricamente lo que DJ-130/DJ-131 Parte 1 ya intuían: un nombre real de persona o empresa, aunque litigue más de una vez, casi nunca lo hace en 3 o más casos distintos capturados en la misma ventana de observación — mientras que la jerga del tipo de juicio sí, por construcción.

### 4.2 Bancos que se repiten legítimamente — **riesgo real, pero distinguible**

BBVA México y Scotiabank aparecen asociados a prácticamente todas las frases genéricas de mayor volumen (son los litigantes institucionales más frecuentes del despacho). Esto no es un problema para la regla: cuando el candidato repetido es el **nombre del banco mismo** (ej. la razón social de BBVA México aparece como candidato en 8 expedientes), se cruza contra exactamente **1** banco — el suyo. En cambio, las frases genéricas verdaderas (ej. "GARANTÍA", "JUICIO ORAL") se repiten a través de **2 o 3 bancos distintos**, porque describen el tipo de trámite, no a quién pertenece.

**Señal de exclusión propuesta:** si el candidato coincide con el nombre (o alias, ver `MencionaBancoOAlias`) de un banco registrado y su conteo de bancos distintos es 1, es una referencia legítima a ese banco — excluir de la lista de candidatos a frase genérica, no porque "sea banco" sino porque el patrón de repetición es el de una identidad única, no el de jerga transversal.

### 4.3 Partes actoras recurrentes no bancarias — **riesgo real, pero ya cubierto por el umbral**

La razón social encontrada en el umbral de 2 (no es un banco registrado) es evidencia directa de este riesgo: una misma entidad demandada o demandante que litiga más de una vez. Pero en los datos medidos, **no alcanzó el umbral de 3** — es decir, el umbral de 3+ expedientes ya la excluye sin necesidad de una lista de exclusión adicional. No hay evidencia suficiente en este corte para justificar una lista de entidades no-bancarias a excluir; si en el futuro una entidad real cruza el umbral de 3, se recomienda tratarla igual que el caso Bancomer: agregarla a una lista de exclusión solo con evidencia real, nunca de antemano.

### 4.4 Número de expediente reutilizado entre juzgados — **riesgo medido indirectamente, ya conocido**

Ver la limitación de la sección 2: el mismo número de expediente (ej. 363/2026) ya se confirmó repetido entre los dos juzgados Orales Mercantiles de Hermosillo (hallazgo de DJ-130). Cualquier implementación real de esta regla debe deduplicar por (`NumeroExpediente`, `NombreJuzgado`), no por número de expediente solo, para no inflar artificialmente la repetición de una frase ni, en el caso contrario, diluir la repetición real de un nombre que coincide en dos casos genuinamente distintos.

### 4.5 Riesgo adicional no pedido explícitamente, pero visible en los datos: candidatos de una sola palabra

Varias de las frases que cruzan el umbral de 3 son palabras sueltas y comunes del español jurídico: "GARANTIA" (la de mayor volumen, 128 apariciones/63 expedientes), "OTRO", "AMPARO", "CUADERNO", "ORAL", "OTROS", "REQUISITORIA". Son seguras de quitar por repetición, pero al ser una sola palabra común, tienen más probabilidad teórica de coincidir por casualidad con un fragmento de un nombre real que una frase de 3-4 palabras como "ACCIÓN PAGO DE PESOS". No se midió ningún caso real donde esto haya ocurrido, pero se recomienda tratarlas con un umbral más estricto que las frases multi-palabra (ver sección 5.1) en vez de agregarlas con el mismo criterio automático.

---

## 5. Diseño propuesto (sin implementar)

### 5.1 Umbral recomendado

- **3 o más expedientes distintos** (contados como pares `NumeroExpediente`+`NombreJuzgado`, por la razón 4.4) como umbral base para candidatos de 2+ palabras.
- Para candidatos de **una sola palabra** (ej. "GARANTIA"), exigir un umbral más alto (propuesta: 5+ expedientes distintos) antes de sugerirlos, dado el riesgo adicional de 4.5. No hay evidencia de que esto sea necesario — es una precaución, no una medida.
- Ventana de tiempo: los datos no mostraron un caso donde limitar por ventana temporal (ej. "solo últimos 60 días") cambiara el resultado — el candidato de riesgo con mayor dispersión temporal (5 semanas) solo llegó a 2 expedientes, por debajo del umbral de 3 de cualquier forma. No se recomienda como filtro obligatorio, pero sí como dato informativo en el reporte (ver 5.3) para que quien revise pueda notar un patrón sospechosamente concentrado en pocos días (más típico de jerga que se repite por un lote de acuerdos similares) vs. disperso en meses.
- **Sin ventana por ahora:** cada corrida del reporte recalcula el conteo sobre todo el historial acumulado desde que empezó a scrapearse, no solo sobre lo nuevo. Esto es válido con los ~2 meses de datos actuales, pero implica que el riesgo de que un nombre real cruce el umbral de 3 por pura coincidencia crece mientras más años se acumulen sin revisar esto — no es medible todavía por falta de historial suficiente. Revisar de nuevo cuando haya un año o más de datos.

### 5.2 Acción al detectar

**Solo aviso/reporte — nunca ocultar ni promover automáticamente.** La regla identifica candidatos; una persona del equipo decide si es jerga (se agrega a `FrasesGenericasConocidas`, siguiendo exactamente el proceso ya usado para la lista aprobada en Parte 1) o si es un nombre real que coincide por casualidad (se descarta, sin cambios). El criterio exacto de aprobación debe seguir siendo el mismo que ya se usó aquí: evidencia real medida, nunca una lista inventada de antemano.

**Excluir del reporte las frases ya aprobadas.** Como el conteo es acumulado (sin ventana, ver 5.1), una frase ya agregada a `FrasesGenericasConocidas` (ej. "JUICIO ORAL") seguiría cruzando el umbral de 3 expedientes para siempre, porque su historial nunca desaparece. Sin este filtro, el reporte la mostraría como "candidato nuevo" cada vez que corra. El reporte debe comparar contra la lista ya aprobada y solo mostrar candidatos que todavía no están en ella.

### 5.3 Dónde viviría la revisión

| Opción | Ventajas | Desventajas |
|---|---|---|
| **Resumen periódico** (ej. correo o reporte mensual con la lista de candidatos nuevos) | Cero trabajo de UI nueevo; reusa cualquier mecanismo de reporte que ya exista | Fácil de ignorar; no hay un lugar donde "aprobar" quede registrado; se puede acumular sin revisarse |
| **Extender `GET /api/scraper/registros`** con un filtro o pestaña de "candidatos repetidos" | Vive donde el equipo ya revisa acuerdos ocultos; acción de aprobar/descartar queda cerca del dato | Requiere trabajo de backend (el query de candidatos) y de frontend (la vista) |
| **Tabla/endpoint dedicado** (ej. `FraseGenericaCandidata`) con estado aprobado/descartado | Más limpio a largo plazo; el flujo de aprobación de esta misma investigación (mandar la lista, aprobar A, descartar B) se volvería un flujo repetible en la app | Más implementación (migración + endpoint + UI mínima); no se justifica todavía por el volumen medido (48 candidatos en ~2 meses, no es un volumen que sature una tabla simple) |

**Recomendación:** empezar con la segunda opción (extender `GET /api/scraper/registros`) — el equipo ya revisa ese endpoint, y el costo adicional es solo exponer el candidato y su conteo, no construir un flujo nuevo de aprobación. Si el volumen de candidatos nuevos crece lo suficiente para que una tabla dedicada se justifique, migrar entonces.

---

## 6. Qué no cubre esta investigación

- No determina la fecha exacta en que la regla habría cruzado el umbral para "ACCIÓN PAGO DE PESOS" (ver sección 3) — requeriría una consulta adicional no corrida en esta investigación.
- No mide bancos, entidades o nombres recurrentes fuera de la tabla `Bancos` existente (ej. compañías de cobranza no registradas como banco) más allá del único caso encontrado por evidencia directa.
- No propone ni mide una implementación para la distinción `NumeroExpediente`+`NombreJuzgado` de la sección 4.4 — solo señala que es necesaria.
- No es una propuesta de código: el umbral, la ventana y la ubicación de la revisión (sección 5) son recomendaciones a discutir con el equipo antes de convertirse en una historia de implementación.
