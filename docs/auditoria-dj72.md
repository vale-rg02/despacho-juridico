# Auditoría de código y tablas huérfanas (DJ-72)

**Fecha:** 24 de agosto de 2026 · **Alcance:** diagnóstico puro — no modifica código ni datos.

Inventario completo de endpoints, tablas y componentes del frontend, cruzados contra sus consumidores reales en el código actual — buscando otros casos del patrón que dejó viva la tabla `Exhortos` semanas después de que el frontend dejara de usarla.

Versión con formato (severidad por color, KPIs): https://claude.ai/code/artifact/3a9a85af-5b0e-4102-a4e9-9f2b100befec

**Resumen:** ~50 rutas auditadas · 10/10 tablas en uso, 0 huérfanas · 0 hallazgos de severidad Alta · 2 de severidad Media · 4 de severidad Baja.

---

## 1. Componentes de frontend — el hallazgo que sí repite el patrón

Trazando el grafo de imports desde `App.jsx`, dos componentes quedaron completamente fuera: nadie los importa, en ninguna página.

### Navbar.jsx + PanelAlertas.jsx — Severidad Media

Cabecera y panel de alertas completos, en el estilo visual anterior al rediseño (Tailwind gris plano vs. la paleta navy/dorado y tipografía `DM Mono` actuales). `Topbar.jsx` reimplementó las mismas alertas de vencimiento con su propia función `AlertaItem` inline — es exactamente el patrón de Exhortos: la implementación vieja nunca se borró tras el reemplazo. Sin riesgo de datos: ambas llaman los mismos endpoints (`GET /notificaciones`, `PATCH .../atendido`) que `Topbar` ya usa activamente, así que nada queda invisible — es código confuso, no un agujero de datos.

- **Huérfano desde:** 2026-07-01 · commit `08d779c`
- **Reemplazado por:** `Topbar.jsx` (creado 2026-06-14)
- **Última página que los usaba:** `DetalleExpediente.jsx`

### Badge.jsx — Severidad Baja

Pill de prioridad/estado (`Normal`, `Urgente`, `Abierto`…) de la misma generación visual que `Navbar`. Las páginas rediseñadas (`Expedientes.jsx`, `DetalleExpediente.jsx`) ahora rotulan prioridad/estado con estilos inline propios en vez de importar este componente. Puramente cosmético — sin lógica ni datos detrás.

- **Huérfano desde:** 2026-07-01 · commit `08d779c`
- **Reemplazado por:** pills inline en cada página

---

## 2. Endpoints sin consumidor confirmado

De ~50 rutas en los 13 controllers, dos nunca aparecen en ninguna llamada `api.get/post/put/patch/delete` del frontend — ni ahora ni en el historial de git. A diferencia de la sección 1, estos no reemplazaron nada: se construyeron y el frontend nunca los conectó.

### POST /api/etapas-catalogo — Severidad Media

Crea entradas nuevas en el catálogo de etapas (los pasos tipo "Demanda", "Contestación", "Sentencia" por tipo de juicio). En la práctica el catálogo completo vive hardcodeado en `DbSeeder.cs` — no hay ninguna pantalla de administración que dependa de este endpoint, y nunca la hubo desde que se creó. Riesgo real si alguien lo invoca directo (Postman, script): el resto del sistema asume que el catálogo es el sembrado fijo, así que una entrada creada por aquí puede quedar en un estado que ningún flujo espera.

- **Sin consumidor desde:** su creación · 2026-06-12
- **Catálogo real vive en:** `Data/DbSeeder.cs`

### GET /api/usuarios/{id} — Severidad Baja

Trae un usuario individual. `Perfil.jsx` siempre pide la lista completa (`GET /usuarios`) y edita sobre esos datos ya cargados — nunca pide uno solo. Solo lectura, sin escritura ni efectos secundarios: cero riesgo de datos, solo una ruta que nadie llama.

- **Sin consumidor desde:** su creación

---

## 3. Tablas del DbContext

Las 10 tablas del `AppDbContext`, cruzadas contra los endpoints ya confirmados con consumidor. Ninguna quedó huérfana.

| DbSet | Leída/escrita por | Estado |
|---|---|---|
| Usuarios | UsuariosController, AuthController | en uso |
| Bancos | BancosController | en uso |
| Expedientes | ExpedientesController, AcuerdosController, ScraperAcuerdosService | en uso |
| EtapasCatalogo | EtapasCatalogoController (GET), DbSeeder | en uso |
| HistorialEtapas | ExpedientesController | en uso |
| Notificaciones | NotificacionesController | en uso |
| BitacoraCambios | ExpedientesController (/bitacora) | en uso |
| AcuerdosScrapeados | AcuerdosController, ScraperController, ScraperAcuerdosService | en uso |
| Citas | CitasController | en uso |
| ExpedienteAccesos | ExpedientesController (/accesos) | en uso |

**Referencia — el caso que originó esta auditoría:** la tabla `Exhortos` y el modelo `Exhorto.cs` ya no existen en el código: se retiraron por completo en el commit `1749ee7` ("retirar sistema legacy de Exhortos, ya reemplazado por AcuerdosScrapeados") y la migración `EliminarTablaExhortosLegacy` borró la tabla en BD. Ese incidente puntual está cerrado — se confirma aquí como línea base, no como pendiente.

---

## 4. Confirmado por diseño — no son hallazgos

Estos endpoints tampoco tienen consumidor en el frontend, pero por razones distintas a los de la sección 2: son herramientas operativas que nunca debieron tener UI. Se documentan para que DJ-73 no los trate por error como huérfanos.

### ScraperController — 4 rutas

`registros`, `ejecutar`, `ejecutar-rango`, `reevaluar-ocultos`. El scraping real corre solo por `ScraperAcuerdosService` (BackgroundService programado); estas rutas son de operación manual — se llaman directo por API, como se hizo para desocultar los acuerdos de los exp. 127/2023, 434/2026 y 477/2026. El historial de git confirma que nunca hubo, ni un día, una pantalla que las llamara.

### POST /api/migracion/excel — Severidad Baja

Herramienta de la importación inicial desde Excel. Se autobloquea en producción (`if (!_env.IsDevelopment()) return NotFound()`) — no es un descuido, es un candado explícito. Candidato a retirar el día que ya no se necesiten más importaciones, no antes.

### GET /api/prueba-fechas — Severidad Baja

Calculadora de días hábiles para probar `ICalculadorFechasService` a mano. No lee ni escribe en BD. Limpieza cosmética si algún día se quiere reducir superficie de API, sin urgencia.

---

## Metodología

1. **Endpoints:** se extrajeron todas las rutas (`[HttpGet]`/`[HttpPost]`/`[HttpPut]`/`[HttpPatch]`/`[HttpDelete]`) de los 13 controllers y se buscó cada una como llamada `api.*(...)` en `frontend/src/services/*.js` y directamente en páginas/componentes.
2. **Tablas:** los 10 `DbSet` de `AppDbContext` se cruzaron contra los controllers ya confirmados con consumidor activo en el paso 1.
3. **Frontend:** grafo de imports armado a mano desde `App.jsx` hacia `pages/` y `components/`; todo lo no alcanzable se marcó huérfano.
4. **Fechas de orfandad:** `git log -S"<import o ruta>"` sobre cada archivo/string para ubicar el commit exacto donde se dejó de referenciar.
5. **Ningún cambio de código ni de base de datos** — todo el trabajo fue lectura de repositorio e historial de git.

Las decisiones de qué resolver y en qué orden quedaron en [DJ-73](https://manueldjah.atlassian.net/browse/DJ-73).

---

*Guardado el 26 de agosto de 2026.*
