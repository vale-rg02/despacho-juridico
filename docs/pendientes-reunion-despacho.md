# Pendientes para la próxima reunión con el despacho

Lista viva de preguntas/temas para plantear a Mario y demás litigantes del despacho. No es un documento técnico — son cosas que solo ellos pueden responder porque requieren su conocimiento del trámite legal día a día.

---

## Abreviaturas de ADISON (TipoAsunto)

**Contexto:** el portal de acuerdos judiciales (ADISON) clasifica cada asunto con una abreviatura — `Exp.`, `Exh.`, `C.P.`, `Cadol.`, `Cuad.`, `Toca`, `Leg.`, `Amp.`/`J.Amp.`, `Pre.`, `Req.`, `"EXP. C."` — que indica a qué serie de numeración pertenece dentro del juzgado (ver `docs/mecanica-legal-sonora.md` #2). Confirmamos con certeza razonable qué son `Exp.`, `Exh.`, `C.P.` (parece específico de juzgados Orales Penales) y `Cuad.` — el resto (`Cadol.`, `Toca`, `Leg.`, `Amp.`/`J.Amp.`, `Pre.`, `Req.`, `"EXP. C."`) los inferimos por contexto, sin confirmación directa.

**Qué preguntar:** mostrarle a los litigantes la lista completa de abreviaturas que vimos en los datos reales y preguntar directamente si las conocen y qué significa cada una — en particular las que no pudimos confirmar.

---

## ¿El despacho lleva asuntos Laborales o Penales?

**Contexto:** DJ-122 clasificó automáticamente los 83 juzgados del catálogo por materia (Civil, Mercantil, Familiar, Arrendamiento) a partir de su nombre, para filtrar el combobox de Juzgado según la Materia elegida al capturar un expediente. 36 de esos 83 juzgados quedaron sin clasificar — son Penales, Laborales, Tribunales Colegiados, Adolescentes, etc., materias que hoy no existen como opción en el catálogo `MATERIAS` del sistema (solo Civil/Mercantil/Familiar/Arrendamiento). No es un error: si el despacho nunca litiga en esas materias, no hace falta nada más.

**Qué preguntar:** ¿el despacho alguna vez lleva un asunto Laboral o Penal (aunque sea ocasional)? Si la respuesta es sí, habría que agregar esas materias al catálogo del sistema como una historia nueva — hoy, si se diera el caso, el litigante podría capturar el expediente igual dejando el campo Materia vacío (el filtro de Juzgado no se aplica y se ve el catálogo completo de la sede), pero sin el filtrado automático ni cualquier otra lógica que dependa de la Materia.

---

*Última actualización: 4 de octubre de 2026.*
