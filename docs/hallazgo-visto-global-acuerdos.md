# Hallazgo: "Visto" de un acuerdo es global, no por usuario

**Fecha:** 7 de octubre de 2026 · **Origen:** descubierto durante la verificación manual de DJ-127 (popup en tiempo real), probando con un colaborador en un expediente ajeno. **Alcance:** diagnóstico — no se modificó código ni datos.

**Resumen:** `AcuerdosScrapeados.Visto` es una sola columna booleana por acuerdo, compartida por todos los usuarios con acceso a ese expediente. Cuando el expediente tiene más de una persona con acceso (titular + colaborador(es), o varios colaboradores), el primero que abre la página del expediente marca el acuerdo como visto **para todos los demás también** — aunque ellos nunca lo hayan visto. Afecta a los tres avisos "activos durante sesión" (punto en la lista DJ-91, campana DJ-126, popup DJ-127); no afecta el correo, que se envía una sola vez a la creación sin depender de `Visto`.

---

## 1. Cómo se reprodujo

1. Un litigante (A) se agrega como colaborador de un expediente cuyo titular es otro usuario (B).
2. A registra un acuerdo manual en ese expediente, o simplemente abre la página del expediente después de que llega un acuerdo nuevo.
3. Al cargar/recargar la página, `DetalleExpediente.jsx` marca automáticamente como visto cualquier acuerdo no-visto que encuentre ahí (lógica de DJ-91, pensada para "ya lo viste en pantalla, bájale el punto de nuevo").
4. Ese marcado llama a `PATCH /api/acuerdos/{id}/visto`, que pone `Visto = true` en la fila — sin distinguir que quien lo vio fue A, no B.
5. B nunca abrió esa página. Su consulta de `GET /api/acuerdos/no-vistos` ya no trae ese acuerdo, porque el filtro es `!a.Visto` a secas — B pierde el aviso de algo que nunca vio.

## 2. Dónde vive el problema

- **Modelo:** `Visto` es `boolean` plano en `AcuerdosScrapeados` (sin relación a `Usuario`) — confirmado en el esquema de la tabla.
- **Se marca global en:**
  - [`DetalleExpediente.jsx:323-327`](../frontend/src/pages/DetalleExpediente.jsx) — auto-marca al cargar la página, sea quien sea quien la abra.
  - [`AcuerdosController.cs:296`](../backend/DespachoJuridico.API/Controllers/AcuerdosController.cs) (`PATCH /api/acuerdos/{id}/visto`) — pone `Visto = true` sin registrar para quién.
- **Se consume como si fuera personal en:**
  - [`AcuerdosController.cs:46`](../backend/DespachoJuridico.API/Controllers/AcuerdosController.cs) (`GET /api/acuerdos/no-vistos`) — filtra `!a.Visto`, alimentando a la vez el punto (DJ-91), la campana (DJ-126) y el popup (DJ-127).

No es un problema nuevo de DJ-127 — existe desde que se introdujo `Visto` (commit `8b8a4e5`, DJ-91). DJ-127 no lo causó, solo lo hizo más visible al probarlo con un colaborador real.

## 3. A quién afecta de verdad

El bug **solo se manifiesta si un expediente tiene más de una persona con acceso activo** (titular + colaborador, o varios colaboradores) y cada una espera enterarse por su cuenta de los acuerdos nuevos. Con un solo litigante por expediente (el caso normal hoy) el problema es invisible: esa única persona siempre es "la que lo vio", así que el comportamiento coincide con lo esperado por pura coincidencia.

Revisé la base de datos local: de 244 expedientes, **0 tienen colaboradores registrados** (`ExpedienteAccesos` vacía). Esto es un proxy de desarrollo, no el dato real de producción — **vale la pena que confirmes cuántos expedientes en producción ya tienen colaboradores asignados** antes de decidir la prioridad, porque eso determina si esto es un problema teórico o uno que ya le está pasando a alguien ahora mismo sin que lo hayan notado.

## 4. Qué pasaría si se arregla (diseño propuesto)

La única forma correcta de arreglarlo es dejar de usar un booleano compartido y rastrear "visto" por pareja (acuerdo, usuario):

- **Nueva tabla** `AcuerdoVistoPorUsuario(AcuerdoId, UsuarioId, FechaVisto)` — migración aditiva de EF Core.
- `GET /api/acuerdos/no-vistos` cambia de `!a.Visto` a `!a.VistosPor.Any(v => v.UsuarioId == usuarioIdActual)`.
- `PATCH /api/acuerdos/{id}/visto` deja de pisar un flag y en su lugar inserta (o ignora si ya existe) una fila `(acuerdoId, usuarioIdActual)`.
- El auto-marcado de `DetalleExpediente.jsx` sigue funcionando igual en el frontend — el cambio es transparente para esa parte, porque de cualquier forma ya manda el PATCH con la sesión del usuario que está viendo la página.
- Los endpoints que devuelven `Visto` en la respuesta (`GetByExpediente`, `GetAcuerdoPorId`) pasan de exponer un booleano global a exponer "visto por mí".
- Pruebas a tocar: `GetNoVistosTests.cs` (el caso central), y revisar que `RegistrarAcuerdoManualTests`/`ConfirmarAcuerdoTests`/`DescartarAcuerdoTests` no asuman el booleano viejo.

Importante no confundir esto con `Oculto`, `DescartadoManualmente` y `Confirmado` — esos sí deben seguir siendo globales: son decisiones sobre el acuerdo mismo ("esto no aplica a este caso", "esto no es nuestro"), correctas para todos los que lo vean. El único campo mal modelado es `Visto`, porque conceptualmente es "¿yo ya lo vi?", no "¿ya se vio?".

## 5. ¿Vale la pena arreglarlo ahora?

Con los datos que tengo (0 expedientes con colaboradores en desarrollo), es una migración de tamaño moderado — toca una tabla nueva, 3 endpoints y las pruebas asociadas — para un escenario que, al día de hoy, probablemente no le esté pasando a nadie en producción porque casi ningún expediente real tiene colaboradores activos todavía. Mi recomendación: **documentarlo y no priorizarlo todavía**, pero si confirmas que en producción ya hay expedientes con 2+ colaboradores usándose activamente, esto deja de ser teórico y valdría la pena moverlo a la fila de tareas pronto — mientras más colaboradores reales acumulen acuerdos "vistos por otro" sin darse cuenta, más silenciosamente se les escapan acuerdos de sus propios casos.
