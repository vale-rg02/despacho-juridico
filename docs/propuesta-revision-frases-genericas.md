# Propuesta: cómo registrar que un candidato ya fue revisado (DJ-133, punto 5)

**Estado: propuesta, sin aplicar.** Ninguna migración ni cambio de esquema está en el working tree — esto es solo la comparación pedida, para decidir antes de implementar.

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

Nada de esto se implementó: es la comparación pedida por el punto 5, para que el equipo decida antes de que se escriba el código real.
