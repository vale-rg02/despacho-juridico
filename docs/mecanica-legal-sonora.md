# Mecánica del sistema legal — lo que el software necesita replicar

Documento vivo. Cada entrada nace de un caso real donde el software falló (o casi falla) por no entender correctamente cómo funciona un trámite/procedimiento judicial en Sonora. El objetivo no es documentar el bug — es documentar la mecánica legal detrás, para que el diseño del sistema la contemple desde el principio en vez de parchearla caso por caso.

**Formato de cada entrada:** qué es la mecánica, por qué el software se equivocó al no contemplarla, el caso real que la destapó, y qué falta decidir/construir.

---

## Índice

1. [Jurisdicción Voluntaria — no hay "parte demandada" adversarial](#1-jurisdicción-voluntaria--no-hay-parte-demandada-adversarial)
2. [TipoAsunto en ADISON — series de numeración independientes por tipo de trámite](#2-tipoasunto-en-adison--series-de-numeración-independientes-por-tipo-de-trámite)

---

## 1. Jurisdicción Voluntaria — no hay "parte demandada" adversarial

**La mecánica:** la Jurisdicción Voluntaria es un procedimiento **no contencioso** — no hay una controversia entre dos partes, solo un *promovente* que le pide al juzgado realizar un acto (notificar a alguien, hacer constar un hecho, autorizar un trámite, etc.). No existe, en sentido procesal, una "parte demandada": nadie está siendo demandado, aunque exista una persona a la que el trámite afecta o a la que se busca notificar.

**Dónde truena en el software:** el despacho captura esa persona afectada/a notificar en el campo `ParteDemandada` del expediente, por convención (es el dato que sirve para saber "a quién le corresponde este caso"). Pero el matching de acuerdos foráneos (`PartesCoinciden` en `ScraperAcuerdosService.cs`) compara siempre el texto que publica ADISON contra ese campo. En la etapa de radicación de una Jurisdicción Voluntaria, ADISON solo nombra al promovente (ej. el banco) — el nombre de la persona capturada como "demandada" simplemente **no aparece todavía** en lo que se publica. No es un problema de ortografía ni de similitud aproximada (como el caso Corona/Coronado, ya corregido): es que el dato que se busca no está en el texto en esa etapa del trámite.

**Caso real que lo destapó (24 de agosto de 2026):**
Expediente 434/2026, Juzgado 1ro Civil de Nogales, `TipoJuicio = "Jurisdicción Voluntaria"`, `ParteDemandada = "Patricia Yanet Contreras Martínez"`.

El 20 de agosto ADISON publicó: *"JURISDICCIÓN VOLUNTARIA CIVIL - OTROS.- BBVA MEXICO SA INSTITUCION DE BANCA MULTILPLE GRUPO FINANCIERO BBVA MEXICO — SE RADICA DEMANDA."* — sin mencionar a Patricia en ningún lado. El sistema comparó ese texto contra "Patricia Yanet Contreras Martínez", no encontró coincidencia, lo clasificó `Confianza=Baja` y lo ocultó sin notificar. Mario se enteró por su cuenta y, 3.5 horas después, capturó manualmente las etapas "Radicación" y "Notificación" en el sistema — exactamente el tipo de dato invisible que este seguimiento debería haber evitado.

**Qué falta decidir / construir:** *(pendiente — no se ha diseñado la solución)*
- Opción A: cuando `Expediente.TipoJuicio == "Jurisdicción Voluntaria"`, comparar también contra el nombre del banco/promovente (si el despacho empezara a capturarlo), no solo contra `ParteDemandada`.
- Opción B: para este tipo de trámite, relajar la verificación de Partes y confiar más en número+juzgado, asumiendo el riesgo de algún falso positivo — evaluar cuántos casos de Jurisdicción Voluntaria tiene el despacho antes de decidir si el riesgo vale la pena.
- Falta contar cuántos expedientes activos del despacho son de este tipo, para dimensionar el impacto real.

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

*Última actualización: 31 de agosto de 2026.*
