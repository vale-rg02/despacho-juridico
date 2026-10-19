# Instrucciones del proyecto: Despacho Jurídico (Acedo e Hijos)

Estas reglas aplican a toda sesión de trabajo en este repositorio, sea cual sea el prompt. Si una instrucción choca con una de ellas, se pregunta antes de actuar. Todo se escribe en español.

## Contexto breve

Sistema de gestión de expedientes de un despacho jurídico de Sonora, con un scraper que consulta ADISON (sitio de terceros con los acuerdos de los juzgados) y avisa a los litigantes por correo y dentro de la misma página (notificaciones in-app, no solo correo). Backend .NET (DespachoJuridico.API) con EF Core y Postgres, frontend React con Vitest, correo por Resend, despliegue en Railway. Verifica este contexto contra el código antes de apoyarte en él.

## 1. Permisos y autorizaciones

- NO hacer commit sin permiso explícito del usuario en el turno actual. Dejar los cambios en el working tree.
- NO crear, editar ni cerrar tickets de Jira ni cambiar su estatus sin que se pida explícitamente. Las decisiones de Jira (estatus, cierre, creación, asignación, sprints) son del equipo.
- NO asumir el estado de Jira, del código ni de los datos: verificar el estado real antes de referenciar. Verificar que un número de ticket exista antes de citarlo.
- Cuando el prompt pida una propuesta, mostrarla y esperar confirmación antes de implementar.
- Ninguna migración ni cambio de esquema sin mostrar primero la propuesta y esperar confirmación. Las migraciones son aditivas. Si una migración reemplaza una columna por una tabla, copia los datos existentes dentro de la misma migración.

## 2. Datos de producción

- Solo lectura por defecto. Todo UPDATE o DELETE exige: conteo previo, dry-run, confirmación explícita y, para borrados, respaldo de las filas antes de borrar. Se ejecuta en una transacción con el conteo verificado antes y después.
- Las herramientas de recuperación manual (promover, sugerir, reintentar) son solo para administradores, se prueban primero con dryRun, deciden explícitamente si notifican y dejan rastro de quién las usó.
- Un fix de código no reclasifica lo ya guardado. Si el cambio afecta datos existentes, medir primero con datos reales y evaluar una corrección retroactiva con dry-run.
- Un dry-run debe revisar si el registro ya existe; uno que cuenta todo como "nuevo" infla las vistas previas.

## 3. Privacidad

- Sin nombres de partes, de clientes ni texto del campo Partes en el repositorio: ni en código, pruebas, documentos, comentarios, mensajes de commit ni logs. En pruebas usar cadenas inventadas y genéricas. Citar expedientes por número y juzgado, y acuerdos por Id.
- Un candidato a frase genérica que se descarta es, por definición, un nombre real o una razón social: nunca se guarda como constante en el repositorio (se guarda en la base).
- Excepción reconocida: el reporte diario de auditoría muestra el texto de los candidatos a frase genérica, porque quien revisa lo necesita. Solo puede ir a destinatarios del equipo técnico.
- Nunca imprimir ni guardar secretos (claves de API, cadenas de conexión, credenciales) en salidas, logs ni documentos.

## 4. Fechas y horas

- Toda fecha y hora, y toda etiqueta o nombre que lleve una fecha (migraciones, documentos, commits, bitácoras), va en hora de Hermosillo (America/Hermosillo, UTC-7, sin horario de verano), no en UTC. Este error ya ocurrió dos veces: el scraper que consultaba un día antes y una migración etiquetada con el 19 de octubre cuando en Hermosillo era el 18.
- Una fecha sin hora no se convierte entre zonas: se muestra tal cual. En el frontend usar la fecha local del navegador, no toISOString.

## 5. Correo

- Nunca enviar correos reales desde desarrollo ni pruebas. EmailService no envía fuera de Production. Para una prueba de equipo se usa Email:DestinatariosPermitidosEnPruebas, solo con direcciones del equipo. Las pruebas automáticas usan un HttpMessageHandler falso, sin red.
- Todo servicio que envíe correos de forma automática o por lotes lleva desde el primer día:
  - un tope de antigüedad;
  - la exclusión explícita de lo que se dejó sin notificar a propósito (notificar=false y backfills históricos);
  - un tope de correos por corrida y por destinatario, con alerta al equipo si se excede;
  - una corrida en modo de prueba que muestre cuántos correos saldrían antes de activarlo.
  Incidente del 18 de octubre de 2026: un reintento de notificaciones envió 34 correos tardíos de un backfill histórico por no excluirlo.

## 6. Scraper y ADISON

- ADISON es un sitio de terceros: respetar el ritmo de peticiones y no superar el volumen diario que ya tolera sin errores (experimento DJ-104). Las pruebas contra ADISON son dry-run (dryRun=true, notificar=false), acotadas por juzgado (idsUnidad), fuera del horario del ciclo automático y respetando el semáforo que evita correr juntos un ciclo automático y uno manual. Detenerse ante un 429, un 403, 5xx repetidos o un timeout, y reportarlo.
- El ciclo automático solo consulta la fecha de hoy. La publicación tardía de ADISON existe pero su impacto medido es bajo.
- El número de expediente es el "número de orden" del juzgado y se reutiliza entre trámites ajenos. Siempre validar por número más juzgado y verificar Partes o Banco.
- No cambiar el comportamiento del matching ni la visibilidad, la confianza o la notificación de un acuerdo si el prompt no lo pide. Un cambio real de matching va en un commit aparte, con casos reales.
- Nada se oculta, descarta ni promueve por tiempo ni por una regla automática de un reporte: los reportes de auditoría solo avisan.
- Una falla al escribir una bitácora, un reporte u otra observabilidad nunca debe romper ni retrasar el ciclo del scraper: se captura, se registra en el log y el ciclo continúa.

## 7. Pruebas y evidencia

- Correr la suite completa antes de proponer un commit y reportar el número de pruebas antes y después, sin regresión. Probar el cableado real con la base de datos, no solo funciones puras. En frontend, verificar en el navegador.
- Cada afirmación en un resumen o documento debe estar respaldada por una consulta, un log o una línea de código. Si no se pudo verificar, decirlo.
- Verificar la premisa de un ticket antes de actuar sobre ella: varios tickets partían de supuestos falsos.

## 8. Alcance durante el plan de cierre

- Congelamiento de alcance: solo entra lo que causó un falso negativo real o una notificación errónea y se arregla con un cambio pequeño. Lo demás va a la lista posterior a la entrega o al SaaS.
- Severidades. S1: un acuerdo real no se notificó o quedó oculto. S2: se notificó un acuerdo de un caso ajeno o una notificación que no debía enviarse. S3: error cosmético o de uso. Se corrigen antes de la entrega solo S1 y S2.

## 9. Cómo entregar el trabajo

- Terminar con un resumen breve: archivos tocados, decisiones tomadas con su razón, pruebas antes y después y lo que NO cubre.
- Dejar los cambios sin commit. Los mensajes de commit, cuando el usuario los pida, van en español y en estilo corto.
- Documentar las decisiones en docs/ (por ejemplo docs/propuestas-tecnicas.md) cuando cambian una regla o dejan una lección, sin nombres de partes.
