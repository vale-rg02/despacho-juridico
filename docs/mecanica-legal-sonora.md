# Mecánica del sistema legal — lo que el software necesita replicar

Documento vivo. Cada entrada nace de un caso real donde el software falló (o casi falla) por no entender correctamente cómo funciona un trámite/procedimiento judicial en Sonora. El objetivo no es documentar el bug — es documentar la mecánica legal detrás, para que el diseño del sistema la contemple desde el principio en vez de parchearla caso por caso.

**Formato de cada entrada:** qué es la mecánica, por qué el software se equivocó al no contemplarla, el caso real que la destapó, y qué falta decidir/construir.

---

## Índice

1. [Jurisdicción Voluntaria — no hay "parte demandada" adversarial](#1-jurisdicción-voluntaria--no-hay-parte-demandada-adversarial)

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

*Última actualización: 24 de agosto de 2026.*
