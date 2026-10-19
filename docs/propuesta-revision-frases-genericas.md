# Propuesta: cómo registrar que un candidato ya fue revisado (DJ-133, punto 5)

**Estado: implementada (DJ-137, 18 de octubre de 2026) — se eligió la Opción A', no la Opción B recomendada originalmente abajo.** Ver "Decisión final" al fondo del documento antes de leer la comparación original, que se conserva tal cual para contexto histórico.

## El problema

Sin algún registro de qué candidatos ya se revisaron, el correo periódico volvería a mostrar el mismo candidato descartado (un nombre real que coincidió por casualidad) cada vez que corra, porque el conteo es acumulado (ver `docs/investigacion-frases-genericas.md`, sección 5.1/5.2). `FrasesGenericasConocidas` ya resuelve la mitad del problema (candidatos aprobados como jerga no se repiten, `ExcluidosPorYaAprobada`) — falta la otra mitad: candidatos **descartados** (nombre real, no se agregan a la lista) tampoco deberían repetirse.

## Opción A — tabla de revisiones

Una tabla pequeña, ej. `RevisionCandidatoFraseGenerica`: `Id`, `Candidato` (normalizado, único), `Decision` ("Aprobado" | "Descartado"), `RevisadoEn`, `RevisadoPorId` (FK a `Usuario`).

**Ventajas:**
- Queda un historial consultable: quién decidió qué y cuándo, útil si alguien pregunta meses después por qué cierta frase no se reporta.
- Se puede extender después (ej. mostrarla en una pantalla de administración) sin volver a migrar.

**Desventajas:**
- Requiere migración de EF Core (tabla nueva).
- Es infraestructura para un volumen todavía bajo: 48 candidatos sobre ~2 meses de datos (ver investigación) — una tabla es más de lo que el volumen actual justifica por sí solo.

## Opción B — lista de exclusión en configuración

Una variable de entorno más, ej. `FrasesGenericas__CandidatosDescartados`, lista separada por comas (mismo estilo que `FrasesGenericas__Destinatarios`), revisada junto con `FrasesGenericasConocidas` para excluir del reporte.

**Ventajas:**
- Cero migración, cero tabla nueva — coherente con que `FrasesGenericasConocidas` en sí también vive en código/config, no en una tabla.
- Cambia sin redeploy de base de datos (aunque si vive en código como `FrasesGenericasConocidas`, sí necesita un commit revisado — ver más abajo).

**Desventajas:**
- No queda "quién" ni "cuándo" decidió — solo que se decidió.
- Si crece mucho, una variable de entorno con decenas de frases se vuelve incómoda de mantener a mano.

## Recomendación

**Opción B, pero NO como variable de entorno — como una segunda constante en código, junto a `FrasesGenericasConocidas`** (ej. `FrasesDescartadasConocidas`, incluida en el mismo archivo `ScraperAcuerdosService.cs`, con el mismo criterio de documentar el origen en comentario). Esto porque:

- El volumen actual (decenas de candidatos, no cientos) no justifica una tabla — misma lógica que ya se usó para no construir el endpoint/tabla de revisión en la Parte 2 de DJ-131 (sección 5.3 de la investigación: "no se justifica todavía por el volumen medido").
- Vivir en código (no en una variable de entorno de runtime) significa que agregar un candidato descartado pasa por un cambio revisado por una persona (commit + code review), **igual que ya se exige para `FrasesGenericasConocidas`** — no se puede "descartar" un candidato sin que alguien lo revise y lo comitee, lo cual es justo la garantía que pide el punto 5 del prompt ("un candidato aprobado como genérico debería terminar en `FrasesGenericasConocidas` mediante un cambio de código revisado por una persona, no automáticamente" — el mismo principio aplica a descartar).
- Si el volumen crece lo suficiente para que mantener dos listas en código se sienta pesado, migrar a la Opción A en ese momento — no antes.

Nada de esto se implementó originalmente: era la comparación pedida por el punto 5, para que el equipo decida antes de que se escriba el código real.

---

## Decisión final (DJ-137, 18 de octubre de 2026)

**Se implementó la Opción A' — tabla `RevisionCandidatoFraseGenerica` — no la Opción B recomendada arriba.** La recomendación original quedó invalidada por un motivo que no se había considerado al escribirla:

**Un candidato descartado es, por definición, un nombre real de persona o una razón social** (la propia descripción de DJ-137 lo dice: "revisado y confirmado como nombre real que coincidió por casualidad"). La Opción B (constante `FrasesDescartadasConocidas` en `ScraperAcuerdosService.cs`) habría puesto ese nombre real directo en un archivo de C# comiteado a git — permanente en el historial del repositorio, sin relación con quién tiene acceso a la base de datos de producción. Esto nunca se señaló en la comparación original porque el enfoque estaba en el volumen (bajo, no justifica una tabla) y en la garantía de revisión humana (que ambas opciones cumplen) — no en que el contenido mismo de una Opción B es sensible de una forma que el de `FrasesGenericasConocidas` (jerga, nunca nombres) no es.

Con esto corregido, el volumen bajo deja de ser el criterio decisivo: una tabla pequeña y aditiva (sin texto en código, con rastro de quién/cuándo/por qué) gana sobre cualquier variante de la Opción B, incluyendo una basada en hash (sin texto en git, pero tampoco nadie puede leer después por qué se descartó algo).

**Implementación real:** tabla `RevisionCandidatoFraseGenerica` (`CandidatoNormalizado` único, `Decision` Aprobado/Descartado, `Motivo` corto, `RevisadoEn`, `RevisadoPorId`), endpoint solo-admin `POST/GET /api/scraper/frases-genericas/revisiones`. `CalcularCandidatos` excluye los descartados con su propio contador `ExcluidosPorYaDescartado`, afectando tanto la vista previa como el correo diario de DJ-140. `FrasesGenericasConocidas` sigue sin cambios — "Aprobado" vía el endpoint es solo rastro informativo, nunca reemplaza el commit revisado que sigue haciendo falta para que una frase cuente de verdad como jerga conocida.

**Procedimiento para el manual de operación (DJ-142 — sin archivo propio en este repo todavía, se deja listo para mover cuando exista):**

- **Para aprobar una frase como jerga conocida:** agregar la frase a `FrasesGenericasConocidas` en `ScraperAcuerdosService.cs`, vía un cambio de código revisado por la otra persona del equipo (Valeria o Manuel) — nunca automáticamente. Opcionalmente, registrar también la decisión "Aprobado" en `POST /api/scraper/frases-genericas/revisiones` para dejar rastro de quién la revisó y cuándo, aunque ese registro no tiene efecto por sí solo.
- **Para descartar un candidato que resultó ser un nombre real:** `POST /api/scraper/frases-genericas/revisiones` con `Decision="Descartado"` — ningún commit, ningún deploy, efecto inmediato en el siguiente reporte.
- **Quién decide:** cualquier persona con acceso admin puede registrar la decisión de descarte (es una corrección de datos, no un cambio de comportamiento del matching); agregar algo a `FrasesGenericasConocidas` requiere que la otra persona del equipo revise el código antes de fusionar, igual que cualquier otro cambio al matching.
