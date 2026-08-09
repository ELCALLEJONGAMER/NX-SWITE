# NX-Suite — Plan de Migración de `MainWindow` (Auditoría)

> **Estado del documento:** Propuesta para revisión. NO se ha modificado
> ningún archivo de producción. Este documento es el único artefacto
> generado por esta tarea.
>
> Rama analizada: `feat(optimizacion_de_codigo)`.
> Fecha de auditoría: 2025.

---

## 1. Resumen ejecutivo

`MainWindow` está dividida en **22 archivos `MainWindow.*.cs`** (partial
class) que suman aproximadamente **6.400 líneas** de code-behind. La
separación física por dominio (SD, Navegación, Catálogo, Detalle,
Asistido, Diagnóstico, RespaldoLlaves, Rp2040, Ajustes, etc.) ya reduce
el "archivo monolítico", pero **todos siguen siendo una sola clase**:
comparten campos privados, se llaman entre sí libremente y, en varios
casos, contienen lógica de negocio (parsing, comparación de versiones,
reglas de decisión) que debería vivir en `Core/`.

El principal problema para agentes IA no es el tamaño total, sino que:

1. Varios partials mezclan **UI + orquestación + lógica de negocio** en
   el mismo método (ej. `EscanearIncompatibilidades` en
   `MainWindow.Diagnostico.cs`, la lógica de botones inteligentes en
   `MainWindow.Detalle.cs`).
2. Existen **campos de estado compartidos** (`_catalogoModulos`,
   `_datosGist`, `_mundoSeleccionado`, `_moduloActual`,
   `FirmwareEmummcRealDetectado`, etc.) leídos y escritos desde múltiples
   partials, lo que obliga a un agente a abrir varios archivos para
   entender el ciclo de vida completo de un dato.
3. Los archivos más grandes (`MainWindow.Detalle.cs` con 1134 líneas,
   `MainWindow.RespaldoLlaves.cs` con 557, `MainWindow.Navegacion.cs`
   con 547) concentran demasiadas responsabilidades distintas.

La buena noticia: gran parte de la lógica **realmente pura** (parsing de
versiones, detección de firmware, formateo/particionado, respaldo de
llaves, limpieza de SD, reglas de pipeline) **ya vive correctamente en
`Core/` y `Hardware/`**. MainWindow, en esos casos, actúa como
coordinador: llama al servicio y actualiza controles. El trabajo de
adelgazamiento debe enfocarse en los puntos donde esa separación **no**
se cumplió todavía.

---

## 2. Estado actual de MainWindow

| Métrica | Valor |
|---|---|
| Archivos `MainWindow.*.cs` | 22 |
| Líneas totales aproximadas | ~6.400 |
| Archivo más grande | `MainWindow.Detalle.cs` (1134 líneas) |
| Archivo más pequeño | `MainWindow.Queue.cs` (29 líneas) |
| Campos de estado compartidos relevantes | ~15 (ver sección 5) |
| Servicios ya correctamente delegados | `SuiteController`, `RespaldoLlavesLogic`, `Rp2040Logic`, `LimpiezaSDLogic`, `VersionConstraintLogic`, `AnalizadorDependencias`, `ParticionadorDiscos`, `NxNandManagerLogic` |

---

## 3. Tabla de todos los partials (con líneas)

| Archivo | Líneas | Responsabilidad principal |
|---|---|---|
| `MainWindow.Detalle.cs` | 1134 | Vista de detalle de módulo: botones inteligentes, chips de versión, cache, screenshots |
| `MainWindow.RespaldoLlaves.cs` | 557 | Overlay respaldo/restauración de llaves (SD ? PC) |
| `MainWindow.Navegacion.cs` | 547 | Navegación entre mundos, filtrado, selección de vista |
| `MainWindow.AsistidoCompleto.cs` | 526 | Overlay asistido completo (formateo + instalación masiva + descarga local) |
| `MainWindow.xaml.cs` | 492 | Composición: campos compartidos, constructor, carga inicial del catálogo, helpers de overlay |
| `MainWindow.Ajustes.cs` | 446 | Overlay de Ajustes: tabs Sonido/Caché/Carpetas Protegidas/GitHub Token |
| `MainWindow.Diagnostico.cs` | 433 | Escaneo de compatibilidad/dependencias/config para "Alertas y Estado" |
| `MainWindow.SD.cs` | 404 | Combo de unidades, panel info SD, detección de firmware emuMMC |
| `MainWindow.Catalogo.cs` | 365 | Tarjetas del catálogo: hover, click, acciones rápidas, cola serial de instalación |
| `MainWindow.DependenciasOverlay.cs` | 353 | Overlay "mesa de crafteo" de dependencias |
| `MainWindow.Rp2040.cs` | 350 | Overlay de firmware RP2040/Picofly |
| `MainWindow.Log.cs` | 321 | Visor de log (sesiones, filtros, exportar) |
| `MainWindow.Asistido.cs` | 268 | Handlers de `VistaAsistida` (instalación secuencial, procesar completo) |
| `MainWindow.LimpiezaSD.cs` | 222 | Overlay limpieza de Micro SD |
| `MainWindow.News.cs` | 221 | Pantalla de inicio (noticias) y diagnóstico rápido SD |
| `MainWindow.Particionado.cs` | 189 | Overlay de particionado |
| `MainWindow.Formato.cs` | 188 | Overlay de formateo rápido FAT32 |
| `MainWindow.InstalacionDesdePc.cs` | 129 | Instalación desde carpeta local de PC |
| `MainWindow.Actualizacion.cs` | 99 | Actualizaciones de la app |
| `MainWindow.Ventana.cs` | 50 | Chrome de ventana (mover, minimizar, cerrar) |
| `MainWindow.Paneles.cs` | 48 | Paneles laterales retráctiles |
| `MainWindow.Queue.cs` | 29 | Overlay de cola |

---

## 4. Responsabilidades de cada partial (detalle)

### `MainWindow.xaml.cs`
- **UI:** 20% (constructor, blur de fondo, animaciones de overlay genéricas).
- **Orquestación:** 60% (constructor, ciclo de vida de sincronización con Gist, wiring de eventos entre controles).
- **Lógica:** 15% (inyección temporal del mundo `cfw` — TODO documentado).
- **Filesystem/red:** 5% (delegado a `_cerebro.SincronizarTodoAsync`).
- Depende de: prácticamente todos los partials (contiene los campos compartidos).
- Controles WPF usados directamente: `MenuMundos`, `ChipsFiltro`, `ArsenalRetractil`, `InfoSD`, `VistaAsistida`, `VistaHubCFW`, `CatalogoModulos`, `ListaNews`, overlays de blur.

### `MainWindow.SD.cs`
- **UI:** 40% (pintado de campos del panel derecho).
- **Orquestación:** 45% (ciclo de vida de detección de firmware async, cancelación).
- **Lógica:** 10% (`TextoEstadoFirmwareEmummc` — mapeo enum?texto, podría vivir en un helper de presentación).
- **Filesystem/red:** 5% (delegado a `_cerebro`).
- Ya delega correctamente detección real a `NxNandManagerLogic` vía `SuiteController.ObtenerFirmwareEmummcAsync`.
- Comparte `_ultimaLetraSdConocida`, `FirmwareEmummcRealDetectado`, `AtmosRealDetectado` con `MainWindow.xaml.cs`, `MainWindow.Diagnostico.cs`.

### `MainWindow.Navegacion.cs`
- **UI:** 30% (animaciones de transición entre mundos).
- **Orquestación:** 55% (selección de vista, filtrado de catálogo).
- **Lógica:** 15% (filtrado por etiquetas/orden por `AccionRapida` — lógica de negocio embebida en `RefrescarVistaActual`, parcialmente duplicada con `Core/FiltroLogic.cs`).
- Depende de `_mundoSeleccionado`, `_filtroSeleccionado`, `_datosGist`, `_catalogoModulos` (campos de `MainWindow.xaml.cs`).
- **Nota:** ya existe `Core/FiltroLogic.cs` (`FiltrarPorEtiqueta`, `FiltrarPorTexto`) pero el filtro por `EtiquetasFiltro` del mundo y el ordenamiento por `AccionRapida` se hacen inline aquí — candidato a mover a `FiltroLogic` para evitar duplicar el concepto de "filtrar catálogo".

### `MainWindow.Catalogo.cs`
- **UI:** 20% (hover, click de tarjeta).
- **Orquestación:** 55% (cola serial vía `SemaphoreSlim`, timer de animación de progreso).
- **Lógica:** 20% (resolución de dependencias antes de instalar — ya delega a `AnalizadorDependencias`, pero la orquestación completa del flujo "resolver deps ? crafteo ? instalar ? refrescar" está aquí).
- **Filesystem/red:** 5% (delegado a `_cerebro`).
- Fuertemente acoplado a `MainWindow.DependenciasOverlay.cs` (`MostrarCrafteoYInstalarAsync`) y a `_semaforoSD` (campo propio, pero usado también desde `MainWindow.Detalle.cs`).

### `MainWindow.Detalle.cs` — **el más grande**
- **UI:** 35% (pintado de textos, badges, chips, imágenes).
- **Orquestación:** 30% (instalar/borrar/actualizar desde el detalle).
- **Lógica:** 30% (**la parte más problemática**): `ActualizarBotonesDetalle` implementa una máquina de estados completa (upgrade/downgrade/instalado/no instalado/solo-detección) que decide qué botón mostrar y con qué texto — es lógica de negocio de "qué acción es válida para esta versión de este módulo", 100% portable y testeable, pero vive como método privado de MainWindow.
- **Filesystem/red:** 5%.
- Comparte `_moduloActual`, `_versionSeleccionadaDetalle`, `_semaforoSD` con `MainWindow.Catalogo.cs`.

### `MainWindow.Asistido.cs` / `MainWindow.AsistidoCompleto.cs`
- **UI:** 25% / 35%.
- **Orquestación:** 60% / 50% (bucles de instalación secuencial, actualización de cola, feedback).
- **Lógica:** 10% / 10% (selección de preset emuMMC según capacidad — regla simple, portable).
- **Filesystem/red:** 5% / 5% (delegado a `_cerebro.InstalarModuloAsync`, `ParticionadorDiscos`).
- Ya delegan bien la ejecución real del pipeline; el volumen viene de la orquestación UI (progreso, cola, textos de estado), lo cual es aceptable para un coordinador.

### `MainWindow.Diagnostico.cs`
- **UI:** 15% (poblar listas, mostrar/ocultar secciones).
- **Orquestación:** 25%.
- **Lógica:** 60% (**candidato ALTO**): `EscanearIncompatibilidades` (Fuentes A, B, C, D) es lógica de negocio pura — agrupación de causas raíz, comparación de constraints, detección de incompatibilidades — actualmente como método `static` dentro de MainWindow, ya usa `VersionConstraintLogic` (correcto) pero el propio escaneo no está extraído.
- Depende de `_catalogoModulos`, `FirmwareEmummcRealDetectado`, `AtmosRealDetectado` (de `MainWindow.SD.cs`), `AnalizadorDependencias` (Core, correcto).

### `MainWindow.RespaldoLlaves.cs`
- **UI:** 35% (pintado de tarjetas, feedback, badges).
- **Orquestación:** 55% (flujo completo SD?PC?SD, selección de respaldo, refrescos post-operación).
- **Lógica:** 10% (ya delega el análisis y ejecución real a `RespaldoLlavesLogic` — **separación correcta**).
- Es grande por la cantidad de estados de UI a sincronizar (dos paneles, feedback, certificado), no por lógica de negocio embebida.

### `MainWindow.Rp2040.cs`
- **UI:** 30%.
- **Orquestación:** 55% (P/Invoke para cerrar ventanas del Explorer, Topmost temporal).
- **Lógica:** 15% (ya delega detección/flasheo a `Rp2040Logic` — correcto).
- **Nota:** el P/Invoke de `FindWindow`/`SendMessage` está duplicado conceptualmente con `Hardware/CazadorVentanas.cs` — candidato a unificar en `Hardware/`, aunque es un problema menor de duplicación, no de arquitectura.

### `MainWindow.Ajustes.cs`
- **UI:** 40% (tabs, listas).
- **Orquestación:** 50% (carga/guardado de preferencias vía `Servicios.Preferencias`, `GestorPreferencias` — correcto).
- **Lógica:** 10%.
- Ya delega bien a `Core/Configuracion/GestorPreferencias.cs` y `TokenGitHub.cs`.

### `MainWindow.LimpiezaSD.cs`, `MainWindow.Formato.cs`, `MainWindow.Particionado.cs`
- Todos siguen el mismo patrón correcto: overlay lee la SD del panel derecho,
  llama a `LimpiezaSDLogic` / `ParticionadorDiscos`, actualiza UI con el
  progreso. **Separación ya adecuada** — candidatos BAJOS.

### `MainWindow.DependenciasOverlay.cs`
- **UI:** 30% (animaciones "mesa de crafteo").
- **Orquestación:** 60% (suscripción a `INotifyPropertyChanged` de módulos, `TaskCompletionSource` para esperar resolución interactiva).
- **Lógica:** 10%.
- Complejo por el patrón de espera interactiva, pero es orquestación de UI legítima (no hay reglas de negocio nuevas aquí, ya usa `ResultadoDependencia`/`AnalizadorDependencias`).

### `MainWindow.Log.cs`, `MainWindow.News.cs`, `MainWindow.InstalacionDesdePc.cs`, `MainWindow.Actualizacion.cs`, `MainWindow.Ventana.cs`, `MainWindow.Paneles.cs`, `MainWindow.Queue.cs`
- Todos con **UI/orquestación dominante (70-90%)** y lógica mínima o nula.
- `MainWindow.InstalacionDesdePc.cs` tiene una función de validación (`EsPaqueteAtmosphereValido`) que es lógica simple y portable, pero de bajo impacto.
- Candidatos **BAJOS**: ya actúan como coordinadores.

---

## 5. Dependencias cruzadas relevantes

### Campos de estado compartidos entre partials (los más relevantes)

| Campo | Definido en | Usado desde |
|---|---|---|
| `_catalogoModulos` | `xaml.cs` | SD, Navegacion, Catalogo, Detalle, Diagnostico, Ajustes, Asistido, AsistidoCompleto, News |
| `_datosGist` | `xaml.cs` | SD, Navegacion, Catalogo, RespaldoLlaves (indirecto vía Gist config), AsistidoCompleto |
| `_moduloActual` | `xaml.cs` | Detalle, Catalogo, News |
| `_mundoSeleccionado` | `xaml.cs` | Navegacion, SD, Catalogo |
| `FirmwareEmummcRealDetectado` / `AtmosRealDetectado` | `SD.cs` | Diagnostico |
| `_semaforoSD` | `Catalogo.cs` | Detalle (instalación desde el detalle usa el mismo semáforo) |
| `_depsActuales`, `_moduloPrincipal` | `DependenciasOverlay.cs` | Catalogo (`MostrarCrafteoYInstalarAsync`) |
| `_ultimaLetraSdConocida` | `SD.cs` | `xaml.cs` (cierre de diálogos tras desconexión) |
| `_cargandoCatalogoInicial` | `xaml.cs` | SD, Navegacion, News |

### Llamadas directas entre partials (ejemplos representativos)

```
MainWindow.Catalogo.cs
  ? MainWindow.DependenciasOverlay.cs (MostrarCrafteoYInstalarAsync)
  ? MainWindow.SD.cs (ActualizarListaUnidadesAsync)
  ? MainWindow.Navegacion.cs (RefrescarVistaActual)

MainWindow.Diagnostico.cs
  ? MainWindow.SD.cs (FirmwareEmummcRealDetectado, AtmosRealDetectado)
  ? Core.AnalizadorDependencias
  ? Core.VersionConstraintLogic

MainWindow.SD.cs
  ? Core.SuiteController (ObtenerInfoPanel, ObtenerFirmwareEmummcAsync)
  ? Core.ModeloSwitchTable / MasterKeyTable

MainWindow.AsistidoCompleto.cs
  ? MainWindow.SD.cs (InfoSD.ComboDrives)
  ? Core.ParticionadorDiscos (vía SuiteController)
  ? MainWindow.RespaldoLlaves.cs (respaldo preventivo antes de formatear)

MainWindow.RespaldoLlaves.cs / MainWindow.Formato.cs / MainWindow.Particionado.cs
  ? todos llaman a RespaldoLlavesLogic.Analizar + RespaldarAsync antes de
    operaciones destructivas (documentado en CODEBASE_INDEX.md)
```

No se detectaron **dependencias circulares** graves entre partials (el
grafo es mayormente unidireccional: overlays específicos ? SD/Navegacion
? Core), pero sí un acoplamiento fuerte por campos de estado compartidos
en lugar de parámetros explícitos, lo que dificulta extraer cualquier
partial sin tocar `xaml.cs`.

---

## 6. Qué ya está correctamente extraído (no tocar)

Estos componentes ya cumplen el patrón deseado — MainWindow solo los
llama y actualiza UI:

| Servicio Core/Hardware | Consumido desde | Evaluación |
|---|---|---|
| `VersionConstraintLogic` | `MainWindow.Diagnostico.cs` | ? Correcto — lógica pura ya extraída |
| `LimpiezaSDLogic` | `MainWindow.LimpiezaSD.cs` | ? Correcto |
| `RespaldoLlavesLogic` | `MainWindow.RespaldoLlaves.cs`, `.Formato.cs`, `.Particionado.cs`, `.AsistidoCompleto.cs` | ? Correcto |
| `Rp2040Logic` | `MainWindow.Rp2040.cs` | ? Correcto |
| `DownloadLogic`, `ZipLogic`, `SHA256Logic` | Pipeline (`Core/Pipeline/Pasos/`), no MainWindow | ? Correcto, ni siquiera acoplado a MainWindow |
| `ReglasLogic` | `SuiteController.InstalarModuloAsync` | ? Correcto |
| `NxNandManagerLogic` | `MainWindow.SD.cs` vía `SuiteController.ObtenerFirmwareEmummcAsync` | ? Correcto |
| `AnalizadorDependencias` | `MainWindow.Catalogo.cs`, `.Diagnostico.cs` | ? Correcto |
| `SuiteController` / `SuiteControllerFacade` | Prácticamente todos los partials, vía `_cerebro` | ? Correcto — único punto de entrada a la lógica de negocio de red/SD |
| `ParticionadorDiscos` | `MainWindow.Formato.cs`, `.Particionado.cs`, `.AsistidoCompleto.cs` (vía `SuiteController`) | ? Correcto |
| `GestorPreferencias`, `TokenGitHub` | `MainWindow.Ajustes.cs` | ? Correcto |

**No se propone duplicar ni tocar ninguno de estos servicios.**

---

## 7. Hotspots de contexto para agentes IA

Ordenados de mayor a menor impacto estimado en consumo de tokens:

### ?? Hotspot 1 — `MainWindow.Detalle.cs` (1134 líneas)
1. **Por qué consume tanto contexto:** mezcla pintado de UI con una máquina
   de estados de decisión (`ActualizarBotonesDetalle`) que un agente debe
   leer completa para entender "qué botón se muestra cuándo". Cualquier
   cambio en reglas de instalación/actualización/downgrade obliga a leer
   el archivo entero porque la lógica y el binding de controles están
   entrelazados.
2. **Separación que reduciría contexto:** extraer `ActualizarBotonesDetalle`
   (y funciones de apoyo `EsUpgrade`/`EsDowngrade`) a una clase pura
   `Core/DetalleModuloLogic.cs` que reciba `ModuloConfig` + `ModuloVersion`
   seleccionada + `haySd` y devuelva un DTO (`AccionesDetalleModulo`) con
   qué botones mostrar y sus textos. `MainWindow.Detalle.cs` solo
   consumiría ese DTO para pintar.
3. **Archivos que debería necesitar leer un agente después:** para
   modificar "qué botón se muestra" ? solo `Core/DetalleModuloLogic.cs`
   (+ tests si existieran). Para cambios visuales ? solo
   `MainWindow.Detalle.cs` + `MainWindow.xaml`.

### ?? Hotspot 2 — `MainWindow.Diagnostico.cs` (433 líneas, alta densidad lógica)
1. **Por qué consume tanto contexto:** `EscanearIncompatibilidades` es una
   función de 200+ líneas con 4 fuentes de detección (A/B/C/D) que un
   agente debe leer entera para entender o modificar cualquier regla de
   compatibilidad, aunque el resultado solo se use para pintar 3 listas.
2. **Separación que reduciría contexto:** mover `EscanearIncompatibilidades`
   y el agrupamiento de "causas raíz" de dependencias a
   `Core/DiagnosticoCompatibilidadLogic.cs` (pura, sin WPF), que ya
   reutiliza `VersionConstraintLogic` y `AnalizadorDependencias`.
   `MainWindow.Diagnostico.cs` quedaría con solo `ActualizarDiagnosticoSD`
   (llamar al servicio + poblar `ItemsSource`).
3. **Archivos que debería necesitar leer un agente después:** para
   modificar reglas de compatibilidad ? solo
   `Core/DiagnosticoCompatibilidadLogic.cs` + `Core/VersionConstraintLogic.cs`.

### ?? Hotspot 3 — Campos de estado compartidos en `MainWindow.xaml.cs`
1. **Por qué consume tanto contexto:** al ser la clase parcial "raíz" que
   declara todos los campos privados compartidos (`_catalogoModulos`,
   `_datosGist`, `_mundoSeleccionado`, etc.), cualquier tarea que toque
   "el catálogo" o "el mundo seleccionado" obliga a un agente a revisar
   este archivo para entender el ciclo de vida completo del dato, además
   del partial específico.
2. **Separación que reduciría contexto:** introducir una clase de estado
   explícita (ej. `EstadoAppMainWindow` o similar, un simple contenedor
   sin lógica) NO es prioritario por ahora — cambiarla generaría alto
   riesgo (ver Fase 6 más abajo) para un beneficio incierto. Alternativa
   de bajo riesgo: documentar en `CODEBASE_INDEX.md` un mapa "campo ?
   quién lo escribe / quién lo lee" (ya iniciado en la sección 5 de este
   documento) para que el agente no necesite grep manual.
3. **Archivos que debería necesitar leer un agente después:** sin cambios
   estructurales aún, pero con el mapa de campos documentado, un agente
   puede saltar directo a los 2-3 partials relevantes sin abrir el resto.

### ?? Hotspot 4 — `MainWindow.Navegacion.cs` (547 líneas)
1. **Por qué consume tanto contexto:** `RefrescarVistaActual` combina
   filtrado por mundo, filtrado por chip, filtrado por texto y ordenamiento
   por `AccionRapida` en una sola función — duplica parcialmente el
   concepto de `Core/FiltroLogic.cs`, obligando a un agente a decidir cuál
   de los dos lugares modificar.
2. **Separación que reduciría contexto:** mover el filtrado por
   `EtiquetasFiltro` del mundo y el criterio de ordenamiento a
   `Core/FiltroLogic.cs` (ya existe y ya se usa parcialmente aquí),
   dejando `RefrescarVistaActual` como una composición de llamadas a
   `FiltroLogic`.
3. **Archivos que debería necesitar leer un agente después:** para
   cambiar el orden o criterio de filtrado ? solo `Core/FiltroLogic.cs`.

### ?? Hotspot 5 — `MainWindow.RespaldoLlaves.cs` / `MainWindow.AsistidoCompleto.cs` (grandes pero ya delegados)
1. **Por qué consume tanto contexto:** su tamaño viene de la cantidad de
   estados de UI a sincronizar entre dos paneles/overlays, no de lógica
   de negocio embebida (ya delegan a `RespaldoLlavesLogic` /
   `SuiteController`). Un agente que solo quiera tocar la regla de
   negocio (ej. "cuándo se considera al día un respaldo") ya puede ir
   directo a `RespaldoLlavesLogic.cs` sin leer el overlay completo — el
   problema aquí es más bien el **tamaño físico** que la mezcla de
   responsabilidades.
2. **Separación que reduciría contexto:** posible partición interna del
   archivo por sub-región (panel SD vs panel PC) en dos partials nuevos
   (`MainWindow.RespaldoLlaves.SD.cs` / `.PC.cs`) — bajo beneficio,
   prioridad baja.
3. **Archivos que debería necesitar leer un agente después:** para
   cambios de regla de negocio, ya solo `Core/RespaldoLlavesLogic.cs`
   (sin cambios necesarios). Para cambios de UI del overlay, el archivo
   ya está razonablemente autocontenido.

---

## 8. Lógica con valor para NX-SWITE-Switch (C++)

| Lógica | Ubicación actual | Portabilidad Switch |
|---|---|---|
| `VersionConstraintLogic` (parsing/evaluación de constraints) | `Core/VersionConstraintLogic.cs` | **ALTA** — ya pura, sin WPF |
| `EscanerIncompatibilidades` (Fuentes A/B/C/D) | `MainWindow.Diagnostico.cs` (embebida) | **ALTA** una vez extraída — concepto de "compatibilidad entre módulos instalados" es igual de válido en el homebrew Switch |
| `AnalizadorDependencias` | `Core/AnalizadorDependencias.cs` | **ALTA** — ya pura |
| `ActualizarBotonesDetalle` (selección de acción: instalar/actualizar/degradar) | `MainWindow.Detalle.cs` (embebida) | **MEDIA-ALTA** una vez extraída — la regla "qué acción es válida para esta versión" es portable aunque el "botón" no exista en Switch |
| `SHA256Logic` | `Core/SHA256Logic.cs` | **ALTA** — ya pura |
| `DetectorVersionesLogic`, `SDMonitorLogic` | `Core/` | **ALTA** — detección de módulos instalados es igual de necesaria en C++ |
| `ModeloSwitchTable`, `MasterKeyTable` | `Core/` | **ALTA** — tablas de datos puras |
| `FiltroLogic` | `Core/FiltroLogic.cs` | **MEDIA** — concepto de filtrar catálogo es válido, pero el modelo de datos (Gist JSON) es específico de esta app |
| `GistParser` / modelos (`GistData`, `ModuloConfig`) | `Network/`, `Models/` | **MEDIA** — el formato JSON del Gist podría reutilizarse como fuente de catálogo en Switch, pero el parser en sí es C#/System.Text.Json |
| `RespaldoLlavesLogic` (comparación de prodkeys, verificación bis_key) | `Core/RespaldoLlavesLogic.cs` | **MEDIA** — el concepto (comparar entradas de prod.keys, verificar bis_key_00) es portable, pero el acceso a filesystem de Windows no |
| `ParticionadorDiscos`, `NxNandManagerLogic` | `Hardware/`, `Core/` | **NINGUNA** — dependen de diskpart/WinAPI/CLI externo específico de Windows |
| Overlays, animaciones, ViewModels de `VistaAsistida` | `UI/` | **NINGUNA** — WPF puro |

**Conclusión:** la lógica de mayor valor para portar (constraints de
versión, análisis de dependencias, análisis de incompatibilidades,
selección de acción de detalle) es precisamente la que hoy está **menos
desacoplada de WPF** (Diagnostico y Detalle). Extraerla a `Core/` no solo
reduce contexto IA sino que es un prerequisito real para la portabilidad.

---

## 9. Arquitectura objetivo propuesta

Sin introducir capas innecesarias. Cambios mínimos y justificados:

```
Core/
??? (ya existente, sin cambios estructurales)
??? DiagnosticoCompatibilidadLogic.cs   [NUEVO] — extrae EscanearIncompatibilidades + agrupación de causas raíz
??? DetalleModuloLogic.cs               [NUEVO] — extrae ActualizarBotonesDetalle (decisión pura de acciones)
??? FiltroLogic.cs                      [YA EXISTE] — absorbe el filtrado por EtiquetasFiltro + orden por AccionRapida de Navegacion.cs

MainWindow.*.cs
??? (sin nuevos archivos por ahora — no se justifica dividir más partials
?    hasta ver el efecto de extraer la lógica de negocio arriba)
```

**Justificación de por qué NO se proponen más carpetas (`Features/`, etc.):**

- La organización actual por partial+dominio (`SD`, `Navegacion`,
  `Catalogo`, `Diagnostico`, `RespaldoLlaves`, `Rp2040`...) ya cumple el
  objetivo de "localización por dominio" razonablemente bien.
- El problema real no es "dónde vive el archivo" sino "qué tipo de código
  contiene". Introducir `Features/Diagnostics/` sin mover la lógica pura
  fuera de WPF no reduciría contexto — solo movería el mismo problema a
  otra carpeta.
- Una vez completadas las Fases 1-3 (ver abajo), se puede reevaluar si
  agrupar partials en subcarpetas (`MainWindow/SD/*.cs`,
  `MainWindow/Diagnostico/*.cs`) aporta valor real. **No se decide ahora.**

---

## 10. Plan de migración por fases

### FASE 1 — ? COMPLETADA

> Implementada en rama `feat(optimizacion_de_codigo)`. Ver `Core/DiagnosticoCompatibilidadLogic.cs`
> y la entrada correspondiente en `CODEBASE_INDEX.md`. Corrección aplicada respecto a la
> propuesta original: los métodos reciben `IReadOnlyCollection<ModuloConfig>`/
> `IEnumerable<ModuloConfig>` en lugar de `List<ModuloConfig>`/`ObservableCollection`,
> para que `Core` no dependa de tipos de colección orientados a UI.

**Archivo o dominio:** `MainWindow.Diagnostico.cs` ? `EscanearIncompatibilidades` + agrupación de causas raíz de dependencias.

**Problema actual:** ~200 líneas de lógica pura de detección de
incompatibilidades (4 fuentes) viven como método `static` privado dentro
de MainWindow, mezcladas con el resto del archivo que sí es UI.

**Qué debería salir de MainWindow:** el método `EscanearIncompatibilidades`
completo y el bloque de agrupación por `causasRaiz` en `ActualizarDiagnosticoSD`.

**Destino sugerido:** `Core/DiagnosticoCompatibilidadLogic.cs` (clase estática, sin dependencias de WPF).

**Archivos/clases nuevas sugeridas:**
- `Core/DiagnosticoCompatibilidadLogic.cs`
  - `EscanearIncompatibilidades(List<ModuloConfig>, string? firmwareReal, string? atmosReal) : List<HallazgoIncompatibilidad>`
  - `AgruparDependenciasRotas(List<ModuloConfig>, ObservableCollection<ModuloConfig>) : List<HallazgoDependencia>`

**Dependencias afectadas:** `MainWindow.Diagnostico.cs` (queda como orquestador: llama al servicio y puebla `ItemsSource`). Ninguna dependencia externa nueva — reutiliza `VersionConstraintLogic` y `AnalizadorDependencias` ya existentes.

**RIESGO:** BAJO (extracción mecánica de una función pura, sin cambio de comportamiento).

**BENEFICIO DE MANTENIMIENTO:** ALTO.

**REDUCCIÓN DE CONTEXTO IA:** ALTA (hotspot #2 resuelto).

**PORTABILIDAD SWITCH:** ALTA.

**Criterio de finalización:** `MainWindow.Diagnostico.cs` reducido a
poblar listas + llamar al nuevo servicio; build limpio; comportamiento
idéntico verificado manualmente (mismos hallazgos mostrados con los
mismos módulos de prueba).

---

### FASE 2

**Archivo o dominio:** `MainWindow.Detalle.cs` ? `ActualizarBotonesDetalle`.

**Problema actual:** máquina de estados de ~150 líneas que decide qué
botón mostrar (instalar/actualizar/degradar/borrar) mezclada con el
pintado directo de propiedades `Visibility`/`Content` de los controles.

**Qué debería salir de MainWindow:** la lógica de decisión (upgrade/downgrade/versión instalada/solo detección), no el pintado de controles en sí.

**Destino sugerido:** `Core/DetalleModuloLogic.cs`, que devuelva un DTO simple, ej.:
```csharp
public record AccionesDetalleModulo(
    bool MostrarInstalar, string TextoInstalar,
    bool MostrarActualizar, string TextoActualizar, bool EsDegradacion,
    bool MostrarBorrar, bool MostrarAbrirUbicacion);
```

> **Nota de corrección arquitectónica (pendiente de aplicar en FASE 2, NO implementada aún):**
> `Core/DetalleModuloLogic.cs` NO debe devolver textos localizados de botones
> (ej. `"ACTUALIZAR A v1.2.3"`). Debe devolver estado/intención semántica
> (por ejemplo un enum `Instalar | Actualizar | Degradar | Reinstalar` +
> la versión objetivo como dato crudo), dejando que la capa de UI/localización
> transforme ese estado en el texto final del botón. Esto es necesario para
> preparar el futuro sistema multiidioma y facilitar la portabilidad a
> NX-SWITE-Switch. El DTO de ejemplo de arriba debe revisarse conforme a esto
> antes de implementar FASE 2.**

**Archivos/clases nuevas sugeridas:** `Core/DetalleModuloLogic.cs` con `DeterminarAcciones(ModuloConfig modulo, ModuloVersion? versionSeleccionada, bool haySd) : AccionesDetalleModulo`.

**Dependencias afectadas:** `MainWindow.Detalle.cs` (consume el DTO y solo pinta). Sin impacto en otros partials.

**RIESGO:** MEDIO (la función actual tiene varias ramas; hay que preservar exactamente el mismo comportamiento visual, incluyendo textos dinámicos como `"ACTUALIZAR A v{version}"`).

**BENEFICIO DE MANTENIMIENTO:** ALTO.

**REDUCCIÓN DE CONTEXTO IA:** ALTA (hotspot #1 resuelto).

**PORTABILIDAD SWITCH:** MEDIA-ALTA.

**Criterio de finalización:** Build limpio; probar manualmente los 5 escenarios (no instalado, instalado igual versión, upgrade, downgrade, solo-detección) y confirmar botones/textos idénticos a antes del cambio.

---

### FASE 3

**Archivo o dominio:** `MainWindow.Navegacion.cs` ? `RefrescarVistaActual` (filtrado + orden).

**Problema actual:** filtrado por `EtiquetasFiltro` del mundo y orden por `AccionRapida` duplican conceptualmente `Core/FiltroLogic.cs`.

**Qué debería salir de MainWindow:** el filtro por etiquetas del mundo y el criterio de ordenamiento (hoy inline con `OrderBy` + `switch`).

**Destino sugerido:** ampliar `Core/FiltroLogic.cs` con:
- `FiltrarPorEtiquetasMundo(IEnumerable<ModuloConfig>, List<string>? etiquetasBase)`
- `OrdenarPorPrioridadAccion(IEnumerable<ModuloConfig>)`

**Archivos/clases nuevas sugeridas:** ninguna nueva — se amplía `Core/FiltroLogic.cs` existente.

**Dependencias afectadas:** `MainWindow.Navegacion.cs` (queda como composición de llamadas a `FiltroLogic`).

**RIESGO:** BAJO.

**BENEFICIO DE MANTENIMIENTO:** MEDIO.

**REDUCCIÓN DE CONTEXTO IA:** MEDIA (hotspot #4 resuelto).

**PORTABILIDAD SWITCH:** MEDIA.

**Criterio de finalización:** Build limpio; catálogo se ve idéntico (mismo orden, mismos módulos visibles) en al menos 2 mundos distintos probados manualmente.

---

### FASE 4 (opcional, evaluar tras 1-3)

**Archivo o dominio:** Mapa de campos de estado compartidos (`_catalogoModulos`, `_datosGist`, etc.) documentado formalmente.

**Problema actual:** un agente necesita grep manual para saber quién lee/escribe un campo compartido.

**Qué debería salir de MainWindow:** nada se mueve de código — es una tarea de documentación, no de refactor.

**Destino sugerido:** ampliar `CODEBASE_INDEX.md` con la tabla de la sección 5 de este documento (una vez aprobada).

**Archivos/clases nuevas sugeridas:** ninguna.

**Dependencias afectadas:** ninguna (solo documentación).

**RIESGO:** BAJO.

**BENEFICIO DE MANTENIMIENTO:** MEDIO.

**REDUCCIÓN DE CONTEXTO IA:** MEDIA (evita exploración general).

**PORTABILIDAD SWITCH:** NINGUNA.

**Criterio de finalización:** Sección añadida a `CODEBASE_INDEX.md` y verificada contra el código real.

---

### FASE 5 (opcional, solo si Fases 1-4 muestran buen ROI)

**Archivo o dominio:** Reagrupar `MainWindow.RespaldoLlaves.cs` en sub-partials por panel (SD / PC).

**Problema actual:** tamaño físico grande (557 líneas) aunque ya bien delegado.

**Qué debería salir de MainWindow:** nada de lógica — solo reorganización física del archivo en 2.

**Destino sugerido:** `MainWindow.RespaldoLlaves.SD.cs` / `MainWindow.RespaldoLlaves.PC.cs` (mismos namespace y clase parcial).

**RIESGO:** BAJO (solo mover código, sin cambiar comportamiento) pero se pospone porque el beneficio es menor que en las fases 1-3.

**BENEFICIO DE MANTENIMIENTO:** BAJO-MEDIO.

**REDUCCIÓN DE CONTEXTO IA:** BAJA (el archivo ya delega bien; dividirlo no cambia qué necesita leer un agente para modificar reglas de negocio).

**PORTABILIDAD SWITCH:** NINGUNA.

**Criterio de finalización:** Build limpio; comportamiento idéntico.

---

## 11. Orden recomendado

1. **FASE 1** — `DiagnosticoCompatibilidadLogic` (mayor reducción de contexto, riesgo bajo, prerequisito de portabilidad).
2. **FASE 2** — `DetalleModuloLogic` (segundo hotspot más grande, riesgo medio, alto valor).
3. **FASE 3** — `FiltroLogic` ampliado (bajo riesgo, cierra la duplicación conceptual).
4. **FASE 4** — Documentación de campos compartidos en `CODEBASE_INDEX.md` (sin riesgo, se puede hacer en paralelo con cualquier fase).
5. **FASE 5** — Reagrupar `RespaldoLlaves` (opcional, evaluar después).

No se recomienda tocar `MainWindow.AsistidoCompleto.cs`,
`MainWindow.RespaldoLlaves.cs` (más allá de Fase 5 opcional),
`MainWindow.Rp2040.cs`, `MainWindow.Ajustes.cs`, `MainWindow.LimpiezaSD.cs`,
`MainWindow.Formato.cs`, `MainWindow.Particionado.cs`,
`MainWindow.Log.cs`, `MainWindow.News.cs`, `MainWindow.Ventana.cs`,
`MainWindow.Paneles.cs`, `MainWindow.Queue.cs`,
`MainWindow.InstalacionDesdePc.cs`, `MainWindow.Actualizacion.cs` — ya
actúan correctamente como coordinadores de UI/servicios.

---

## 12. Riesgos

| Riesgo | Fase afectada | Mitigación |
|---|---|---|
| Cambiar sutilmente el texto/orden de un botón en Detalle | Fase 2 | Probar manualmente los 5 escenarios documentados antes/después; comparar screenshots si es posible |
| Introducir una regresión en el escaneo de incompatibilidades (Fuente D depende de campos de `MainWindow.SD.cs`) | Fase 1 | Pasar `FirmwareEmummcRealDetectado`/`AtmosRealDetectado` como parámetros explícitos, no como acceso a campo — ya es el patrón actual (son parámetros de `EscanearIncompatibilidades`) |
| Duplicar temporalmente lógica de filtrado mientras se migra a `FiltroLogic` | Fase 3 | Migrar y eliminar el código inline en el mismo commit, no dejar ambas versiones convivan |
| Cualquier fase requiere tocar `MainWindow.xaml.cs` porque declara los campos compartidos | Todas | Ninguna fase de este plan requiere tocar `xaml.cs` — todas las extracciones propuestas reciben los datos necesarios como parámetros de método, no acceden a campos compartidos desde el nuevo código en `Core/` |
| Regresión de compilación por partial mal referenciado | Todas | Build antes y después de cada fase (regla obligatoria, ver sección 13) |

---

## 13. Reglas de ejecución de las futuras fases

(Reiteradas del encargo original, para que este documento sea autocontenido)

1. Alcance pequeño por fase.
2. Mover una responsabilidad concreta a la vez.
3. Mantener comportamiento idéntico (sin cambios visuales salvo necesarios).
4. Compilar antes del cambio.
5. Compilar después del cambio.
6. Corregir únicamente errores relacionados con esa fase.
7. Actualizar `CODEBASE_INDEX.md` al finalizar cada fase.
8. Realizar pruebas manuales relevantes al área tocada.
9. No avanzar automáticamente a la siguiente fase.
10. Esperar aprobación explícita del usuario antes de continuar.
11. Preferir un commit independiente por fase.

---

## 14. Recomendación sobre `CODEBASE_INDEX.md`

El archivo actual (~900 líneas) mezcla:
- Mapa de proyecto (útil para todos los agentes, debe ser corto).
- Documentación detallada de cada clase/método (extensa, se sigue acumulando).
- Historial de cambios narrativo por rama (`*Actualizado: 2025 — rama ...*`), que crece indefinidamente y aporta poco valor de navegación a un agente nuevo.

**Recomendación (no ejecutar todavía):**
- Separar en `docs/codebase/CORE.md`, `docs/codebase/UI.md`,
  `docs/codebase/PIPELINE.md`, `docs/codebase/HARDWARE.md` tal como
  sugiere el encargo, dejando `CODEBASE_INDEX.md` como un mapa corto
  (estructura de carpetas + tabla de "para esto, lee esto").
- El historial narrativo de ramas (`*Actualizado: 2025 — rama X*`) podría
  moverse a un `CHANGELOG_TECNICO.md` separado — no es un mapa de
  navegación, es una bitácora, y hoy ocupa una porción significativa del
  archivo que todo agente debe leer primero.
- Esta separación **sí reduciría contexto de forma medible**, porque hoy
  cualquier agente que sigue la instrucción "lee `CODEBASE_INDEX.md`
  primero" paga el costo completo de ~900 líneas incluso para tareas que
  solo tocan, por ejemplo, `Hardware/`.
- **No se ejecuta en esta tarea** por restricción explícita del encargo.
  Se recomienda abordarlo como una fase independiente (Fase 0 o Fase 6)
  una vez validadas las fases 1-3 de este plan, para no mezclar dos tipos
  de cambio en el mismo commit.

---

*Fin del documento. Ningún archivo de producción fue modificado como parte de esta auditoría.*
