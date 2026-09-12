# Mecánica del sistema legal — lo que el software necesita replicar

Documento vivo. Cada entrada nace de un caso real donde el software falló (o casi falla) por no entender correctamente cómo funciona un trámite/procedimiento judicial en Sonora. El objetivo no es documentar el bug — es documentar la mecánica legal detrás, para que el diseño del sistema la contemple desde el principio en vez de parchearla caso por caso.

**Formato de cada entrada:** qué es la mecánica, por qué el software se equivocó al no contemplarla, el caso real que la destapó, y qué falta decidir/construir.

---

## Índice

1. [Jurisdicción Voluntaria — no hay "parte demandada" adversarial](#1-jurisdicción-voluntaria--no-hay-parte-demandada-adversarial)
2. [TipoAsunto en ADISON — series de numeración independientes por tipo de trámite](#2-tipoasunto-en-adison--series-de-numeración-independientes-por-tipo-de-trámite)
3. [Hipotecario/Especial Hipotecario — el banco puede aparecer solo, sin el demandado](#3-hipotecarioespecial-hipotecario--el-banco-puede-aparecer-solo-sin-el-demandado)

---

## 1. Jurisdicción Voluntaria — no hay "parte demandada" adversarial

**La mecánica:** la Jurisdicción Voluntaria es un procedimiento **no contencioso** — no hay una controversia entre dos partes, solo un *promovente* que le pide al juzgado realizar un acto (notificar a alguien, hacer constar un hecho, autorizar un trámite, etc.). No existe, en sentido procesal, una "parte demandada": nadie está siendo demandado, aunque exista una persona a la que el trámite afecta o a la que se busca notificar.

**Dónde truena en el software:** el despacho captura esa persona afectada/a notificar en el campo `ParteDemandada` del expediente, por convención (es el dato que sirve para saber "a quién le corresponde este caso"). Pero el matching de acuerdos foráneos (`PartesCoinciden` en `ScraperAcuerdosService.cs`) compara siempre el texto que publica ADISON contra ese campo. En la etapa de radicación de una Jurisdicción Voluntaria, ADISON solo nombra al promovente (ej. el banco) — el nombre de la persona capturada como "demandada" simplemente **no aparece todavía** en lo que se publica. No es un problema de ortografía ni de similitud aproximada (como el caso Corona/Coronado, ya corregido): es que el dato que se busca no está en el texto en esa etapa del trámite.

**Caso real que lo destapó (24 de agosto de 2026):**
Expediente 434/2026, Juzgado 1ro Civil de Nogales, `TipoJuicio = "Jurisdicción Voluntaria"`, `ParteDemandada = "Patricia Yanet Contreras Martínez"`.

El 20 de agosto ADISON publicó: *"JURISDICCIÓN VOLUNTARIA CIVIL - OTROS.- BBVA MEXICO SA INSTITUCION DE BANCA MULTILPLE GRUPO FINANCIERO BBVA MEXICO — SE RADICA DEMANDA."* — sin mencionar a Patricia en ningún lado. El sistema comparó ese texto contra "Patricia Yanet Contreras Martínez", no encontró coincidencia, lo clasificó `Confianza=Baja` y lo ocultó sin notificar. Mario se enteró por su cuenta y, 3.5 horas después, capturó manualmente las etapas "Radicación" y "Notificación" en el sistema — exactamente el tipo de dato invisible que este seguimiento debería haber evitado.

**Qué falta decidir / construir:** resuelto — se tomó la Opción A (comparar también contra el banco/promovente), implementada específicamente para Jurisdicción Voluntaria en `EvaluarJurisdiccionVoluntaria`. Después se confirmó que el mismo problema (ADISON nombrando solo al banco) **no es exclusivo de Jurisdicción Voluntaria** — también ocurre en Hipotecario/Especial Hipotecario normal, un trámite sí contencioso — y se generalizó ahí con su propio nivel de confianza (`Media`, distinto de la Opción A original porque en un trámite contencioso sí existe un demandado real que eventualmente puede nombrarse, así que no se le da la misma confianza automática que a Jurisdicción Voluntaria). Ver entrada #3 para la mecánica generalizada, la evidencia medida, y la solución completa.

---

## 2. TipoAsunto en ADISON — series de numeración independientes por tipo de trámite

**La mecánica:** dentro de un mismo juzgado, el número de "asunto" que publica ADISON **no es una sola serie numérica** — es varias series independientes que coexisten y se reinician cada una por su cuenta, distinguidas por el campo `TipoAsunto`. Confirmados hasta ahora: `Exp.` (Expediente — civil/familiar/mercantil), `C.P.` (Causa Penal), `Cadol.` (Causa de Adolescentes) — estos tres comparten numeración con el expediente que captura el despacho. `Exh.` (Exhorto — carpeta que abre un juzgado a petición de *otro* juzgado, para diligenciar algo puntual fuera de su jurisdicción; usa la numeración propia del juzgado que **recibe** el exhorto, no la del expediente que lo originó) y `Cuad.` (Cuadernillo — sub-expediente auxiliar abierto *dentro* de un caso para tramitar algo incidental, también con numeración propia) son series aparte, confirmadas como no relacionadas. También se observaron `Pre.`, `Req.` y `J.Amp.` en los datos crudos de ADISON, cuyo significado exacto y relación de numeración con el expediente original **no se han confirmado todavía** — `J.Amp.` casi seguro es Juicio de Amparo (un procedimiento constitucional totalmente aparte, con su propia numeración), pero no hay evidencia directa que lo confirme ni que aclare `Pre.`/`Req.`. Que un "476/2026" exista en un juzgado no dice nada sobre a cuál serie pertenece — un Expediente 476/2026 y un Exhorto 476/2026 en el mismo juzgado son carpetas completamente distintas que solo comparten el número por coincidencia.

**Dónde truena en el software:** `EjecutarScrapingAsync` en `ScraperAcuerdosService.cs` sí captura `TipoAsunto` (se guarda en `AcuerdosScrapeados.TipoAsunto`), pero el matching contra los expedientes del despacho lo ignora por completo — tanto la ruta de Hermosillo (número + juzgado, `JuzgadoCoincide`) como la foránea (`PartesCoinciden`) solo comparan el número normalizado (`NormalizarNumero`) sin considerar de qué serie viene. Esto es la misma familia de riesgo que los falsos positivos de julio 2026 ya documentados en el código (coincidencia de número entre juzgados/series distintas) — pero hasta ahora no se había identificado que `TipoAsunto` es justo la señal que permite anticipar el riesgo antes de que ocurra el falso positivo, en vez de descubrirlo después con un caso real.

**Caso real que lo destapó (28 de agosto de 2026):**
Durante la corrida del scraper de las 16:05 (hora Hermosillo) del 28 de agosto, el acuerdo con número "476/2026" del Juzgado Oral Penal de San Luis Río Colorado hizo match contra un expediente del despacho por número+año bajo la ruta foránea. Al revisar el dato crudo de ADISON directamente (fuera de la BD, porque el registro nunca llegó a guardarse por otro bug — el índice único de `AcuerdosScrapeados` truena con síntesis largas), su `TipoAsunto` real es `"Exh."`: es el número del exhorto dentro del sistema de ese juzgado, no necesariamente el expediente original del despacho. El texto asociado ("En cuadernillo formado con motivo del exhorto 719/2026. Se devuelve diligenciado.") tampoco menciona a ninguna parte del despacho — consistente con ser una carpeta ajena que solo coincidió en número.

**Qué falta decidir / construir:** decisión tomada — alcance acotado, no lista blanca: solo se trata con cautela `TipoAsunto` en `{"Exh.", "Cuad."}`, que son los dos que se confirmaron con evidencia directa como series de numeración ajenas al expediente original. El resto se dejó **deliberadamente sin trato especial**, no por descuido sino porque no se identificó un beneficio claro que lo justifique (ver `docs/pendientes-reunion-despacho.md`):
- Se ajusta `ScraperAcuerdosService.cs` (`EsSerieAuxiliar`) para que un match donde `TipoAsunto` sea `Exh.` o `Cuad.` nunca se clasifique `Confianza=Alta` por número+juzgado solamente — siempre pasa por verificación de Partes (igual que ya ocurre en la ruta foránea), y si `Partes` no trae nombre reconocible, se guarda oculto (`Baja`) en vez de confiar por default como hacía antes la ruta de Hermosillo.
- Se probó una versión más amplia (lista blanca: solo confiar en `Exp.`/`C.P.`/`Cadol.`, cautela para cualquier otra cosa) que también cubría automáticamente lo que se fuera encontrando sin tener que reconocerlo explícitamente — se descartó por ahora, valorando que no hay evidencia de que `Toca`, `Leg.`, `Amp.`/`J.Amp.`, `Pre.`, `Req.` o `"EXP. C."` (encontrados el 31 de agosto de 2026 al muestrear juzgados de Hermosillo — ver más abajo) representen el mismo riesgo real que sí se confirmó para `Exh.`/`Cuad.`.
- Muestreo real (28 de agosto de 2026, juzgados de Hermosillo): `Toca` es el 100% de lo publicado por el Tribunal Colegiado ese día (muy probablemente el expediente de una apelación, numeración propia) — el candidato más fuerte a agregarse si se decide ampliar el alcance más adelante. `Cuad.` resultó nada raro en Tribunal Laboral (8 de 25 acuerdos ese día). También se confirmó que `Exh.` y `Cuad.` **sí aparecen en juzgados de Hermosillo**, no solo en foráneos.
- Corrección a la entrada original: `C.P.` no es "el tipo penal" en general — el Penal *regular* de Hermosillo usa `Exp.`, no `C.P.`; `C.P.` parece específico de juzgados **Orales** Penales.
- Pendiente: preguntarle directamente al despacho qué significa cada abreviatura (lista completa en `docs/pendientes-reunion-despacho.md`) antes de decidir si vale la pena ampliar el alcance.

---

## 3. Hipotecario/Especial Hipotecario — el banco puede aparecer solo, sin el demandado

**La mecánica:** en la etapa de radicación de un juicio Hipotecario (publicado por ADISON bajo el rubro "Especial Hipotecario"), el texto de "Partes" a veces solo nombra al **banco promovente** — nunca al demandado — igual que ya pasaba en Jurisdicción Voluntaria (ver entrada #1), pero esta vez en un trámite contencioso normal, no en uno no contencioso. No es un caso aislado de Jurisdicción Voluntaria: es un patrón más amplio de cómo ADISON publica la radicación de cualquier trámite bancario, sin importar si es o no adversarial. Además, muchos acuerdos siguen usando el nombre histórico del banco — "Bancomer" en vez de "BBVA México" (Bancomer se fusionó/renombró a BBVA México hace años, pero el nombre viejo sigue circulando en los juzgados).

**Dónde truena en el software:** `PartesCoinciden` en `ScraperAcuerdosService.cs` solo comparaba el texto de ADISON contra `Expediente.ParteDemandada` — nunca contra `Expediente.Banco`. Cuando el juzgado y el número de expediente ya coincidían exacto pero el texto solo traía el banco, el sistema no tenía forma de reconocer eso como una coincidencia real: lo clasificaba `Confianza=Baja` y lo ocultaba, igual que si fuera ruido de un caso totalmente ajeno.

**Caso real que lo destapó (11 de septiembre de 2026):**
5 acuerdos confirmados en producción, todos en el Juzgado Primero Civil de Nogales, todos con `Banco = "BBVA México"`, juzgado y número ya coincidentes exacto contra el expediente registrado:

- Exp. 576/2017 (Sonia Lilia Reyna Ocampo): *"ESPECIAL HIPOTECARIO - ACCION HIPOTECARIA Y PAGO DE CREDITO.- - BANCOMER -"*
- Exp. 476/2026 (Karina Guadalupe Tapia Sánchez y otro), dos acuerdos: *"ESPECIAL HIPOTECARIO.- BBVA MEXICO SA.--"* y *"ESPECIAL HIPOTECARIO - OTROS.- BBVA MEXICO S.A. INSTITUCION DE BANCA MULTIPLE, GRUPO FINANCIERO BBVA MEXICO.-"*
- Exp. 401/2026 (Adrián Omar Tiznado Valenzuela y otra), dos acuerdos: *"ESPECIAL HIPOTECARIO - OTROS.- BBVA MEXICO SA INSTITUCION BANCA MULTIPLE"* y, el más extremo, *"ESPECIAL HIPOTECARIO -"* — sin mencionar siquiera al banco.

Ninguno de los cinco mencionaba al demandado por nombre; los cuatro primeros sí mencionaban al banco.

**Qué falta decidir / construir:** decisión tomada e implementada (DJ-122) — antes de decidir se midió con datos reales, no se asumió nada:
- Se confirmó que "Bancomer" sí es un alias real de "BBVA México" con evidencia (13 de 48 menciones de "Bancomer" en toda la BD corresponden a expedientes con Banco=BBVA México) — se agregó como alias específico, no genérico para cualquier banco.
- Se descartó usar "Especial Hipotecario" como señal de match por sí sola: 8 de 84 menciones en toda la BD aparecen en expedientes que ni siquiera son Hipotecario — es solo el nombre de una categoría de trámite, no identifica un caso específico.
- Se descartó confiar en "menciona al banco" sin juzgado ya confirmado: BBVA México es el banco del 78% del portafolio activo del despacho (93 de 119 expedientes bancarios) y del 100% de los expedientes activos en Primero Civil de Nogales — sin el juzgado como filtro, generaría demasiados falsos positivos (medido: <1% del ruido foráneo de juzgados ajenos también menciona BBVA, pero no es cero a escala estatal).
- Solución: nuevo nivel de confianza `Media` — solo se activa cuando el juzgado **y** el número ya coinciden exacto contra el expediente registrado (para foráneos, nueva función `JuzgadoForaneoCoincide`, que compara por conjunto de palabras clave — ordinal, materia, municipio — en vez de patrones fijos como `JuzgadoCoincide`) y el texto menciona al banco (o su alias). Es visible y genera correo (con mensaje de "¿es tuyo?", no de certeza), y el litigante confirma o descarta con los mismos botones ya construidos en DJ-99.
- Pendiente real: no se midió específicamente si este mismo patrón (banco sin nombre del demandado) también ocurre en juzgados de Hermosillo, solo en foráneos — la lógica ya se generalizó para aplicar a ambos por igual, pero falta confirmar con datos si Hermosillo también lo necesitaba o si fue una generalización preventiva sin casos reales todavía.

---

*Última actualización: 12 de septiembre de 2026.*
