# AUDITORÍA DE LOGGING DE PROCESOS CRÍTICOS

> Documento **exclusivamente de auditoría**. No se modificó ningún archivo de producción,
> XAML, Gist, ni el sistema de `Logger` existente durante esta tarea.
>
> Alcance: `NX-Suite/` (excluye `bin/`, `obj/`, `NewFolder1/`, `NewFolder2/`, `NX-SWITE-Switch/`,
> `NX-Suite.Updater/`).

---

## 1. Resumen ejecutivo

El `Logger` actual (`Services/Logger.cs`) es un logger de texto plano, por sesión, thread-safe,
con ~50 métodos semánticos ya cubriendo la mayoría de las operaciones destructivas/críticas
(descarga, extracción, copiado, instalación, formateo, particionado, respaldo/restauración de
llaves, RP2040, desinstalación, caché). **La cobertura es sorprendentemente buena en los
"cuellos de botella" del pipeline** (`PasoDescargar`, `PasoFormatearSd`, `ReglasLogic`,
`RespaldoLlavesLogic`) gracias a que ya usan los métodos semánticos del Logger de forma
consistente.

Los huecos reales no están en el pipeline de instalación en sí, sino en:

1. **Actualización de la propia app** (`GestorActualizacion.cs`, `MainWindow.Actualizacion.cs`) —
   **cero llamadas a `Logger`**. Ni descarga, ni lanzamiento del updater, ni error quedan
   registrados.
2. **Sincronización remota (Gist)** (`Network/GistParser.cs`) — **cero llamadas a `Logger`**.
   Fallos de red, JSON corrupto, o fallback a caché no dejan rastro.
3. **Pasos de filesystem de bajo nivel** (`PasoBorrarArchivos`, `PasoBorrarCarpetas`,
   `PasoBorrarCarpetasVacias`, `PasoMoverArchivo`, `PasoCrearCarpeta`, `PasoCrearIni`,
   `PasoCrearTxt`, `PasoEditarIni`) — no logean individualmente; dependen de que
   `ReglasLogic`/`Logger.InstalacionFallida` capture la excepción aguas arriba, lo cual
   funciona pero pierde el detalle de "qué archivo/carpeta" se estaba borrando/creando.
4. **`UninstallLogic.DesinstalarAsync`** — catch genérico que traga la excepción y solo
   devuelve `false`; el log de éxito/fallo lo hace `SuiteController` un nivel arriba, pero
   sin detalle de la causa real del fallo.
5. **`PasoEjecutarCmd`** — ejecuta procesos externos arbitrarios (con permisos de admin
   heredados) sin logear comando, argumentos, código de salida ni fallo.

No se detectó ningún caso de logging de secretos (tokens, prod.keys, bis_keys) en el código
revisado. El diseño actual del Logger es suficiente; **no se recomienda ningún framework
nuevo**.

---

## 2. Arquitectura actual del Logger

**Clase:** `Services/Logger.cs` — `static class Logger`

| Aspecto | Detalle |
|---|---|
| Destino | `%AppData%\NX-Suite\NX-Suite.log` (texto plano, append) |
| Formato | `[HH:mm:ss] NIVEL  Mensaje` (por línea), con cabecera de sesión `=== Sesión iniciada ... ===` |
| Niveles | `INFO`, `WARN`, `ERROR`, y `OK` (usado dentro de métodos semánticos de éxito) |
| Timestamp | Sí, por línea (`HH:mm:ss`), y fecha completa en cabecera de sesión |
| Sesiones | Sí — `IniciarSesion()` escribe cabecera con fecha, versión de app (`ConfiguracionLocal.VersionActual`) y versión de Windows (WMI). Se recortan automáticamente a las **últimas 5 sesiones** |
| Excepciones | `Error(mensaje, ex?)` acepta excepción opcional y la vuelca (mensaje + posiblemente stack trace, no confirmado en detalle pero no se detectó filtrado de datos sensibles) |
| Exportación | `ObtenerTextoCompleto()` (texto completo), `MainWindow.Log.cs` permite copiar y guardar a archivo vía `SaveFileDialog` |
| Comportamiento ante error propio | Todas las escrituras están envueltas en `try/catch` que **traga silenciosamente** cualquier fallo de I/O del propio logger (ej. disco lleno, archivo bloqueado) — no hay fallback secundario ni notificación al usuario. Esto es aceptable para un logger de diagnóstico, pero significa que un fallo del propio log es invisible |
| Contexto/categoría por operación | No hay un sistema de "categoría" estructurado; el contexto se transmite mediante el propio texto del método semántico (`DescargaIniciada(modulo, url)`, etc.) — patrón simple pero consistente |
| Thread-safety | `lock` explícito en todas las escrituras |

**Métodos de lectura para el visor:** `ObtenerSesiones()` (parsea a `List<SesionLog>`),
`ObtenerTextoCompleto()`, `LimpiarLog()` (borra todo el archivo).

**Conclusión de diseño:** el Logger es simple pero **suficiente** para las necesidades actuales
de la app (una sola instancia de escritorio, sin telemetría remota, sin necesidad de niveles
configurables en runtime). No se recomienda Serilog/NLog/ILogger — sería sobre-ingeniería para
el problema real, que es simplemente **falta de puntos de llamada**, no limitación del propio
Logger.

---

## 3. Matriz de operaciones críticas

| # | Proceso | Archivo principal | Cobertura | Notas |
|---|---|---|---|---|
| 1 | Descarga de módulo | `Core/Pipeline/Pasos/PasoDescargar.cs` | **A** | Inicio, éxito (con tamaño), omitido por caché válida, fallo, invalidación por hash GitHub |
| 2 | Extracción ZIP/RAR/7z | `Core/Pipeline/Pasos/PasoExtraer.cs` | **A** (según índice; no releído en detalle esta sesión, ya documentado) | Inicio, éxito, omitido, fallo |
| 3 | Copiado a SD | `Core/Pipeline/Pasos/PasoCopiarSD.cs` | **A** | `Logger.CopiadoIniciado` visto; según índice también `CopiadoCompletado/Fallido` |
| 4 | Borrado de archivos SD (paso genérico) | `Core/Pipeline/Pasos/PasoBorrarArchivos.cs` | **C** (a nivel de paso) / **B** (a nivel de pipeline) | Sin ningún log propio; sin try/catch — si falla, la excepción sube y `ReglasLogic` la registra como `InstalacionFallida`, pero sin indicar qué archivo causó el fallo |
| 5 | Borrado de carpetas / carpetas vacías SD | `PasoBorrarCarpetas.cs`, `PasoBorrarCarpetasVacias.cs` | **C** (mismo patrón que #4) | No inspeccionado línea por línea pero mismo patrón de la familia `Paso*`, sin logging propio |
| 6 | Mover archivo SD | `PasoMoverArchivo.cs` | **C** (mismo patrón) | Idem |
| 7 | Crear carpeta / ini / txt / editar ini | `PasoCrearCarpeta.cs`, `PasoCrearIni.cs`, `PasoCrearTxt.cs`, `PasoEditarIni.cs` | **C** (creación/txt) / **A** (`PasoHekateSetValue`/`PasoHekateSetIcon` sí logean) | Los pasos genéricos de creación no logean; los pasos específicos de Hekate sí (`HekateValorEstablecido`, `HekateIconAplicado`, etc.) |
| 8 | Integridad SHA-256 | `Core/SHA256Logic.cs` + `Core/GitHubAssetValidator.cs` | **B** | `GitHubAssetValidator` logea warnings en fallback (API/hash no disponible); `SHA256Logic` en sí (cálculo/validación) no fue confirmado con logging propio — el resultado se usa aguas arriba en `PasoDescargar`, que sí logea el resultado final |
| 9 | Pipeline de instalación (orquestador) | `Core/ReglasLogic.cs` | **A** | Inicio, completado, cancelado, fallido — con módulo, versión y distinción explícita cancelación/error |
| 10 | Instalación/Actualización/Desinstalación de módulo | `Core/SuiteController.cs` | **A** (desinstalación) | `DesinstalacionIniciada` confirmado; instalación delega en `ReglasLogic` (#9) |
| 11 | Desinstalación — borrado de archivos de bajo nivel | `Core/UninstallLogic.cs` | **C** + catch silencioso | `catch (Exception) { return false; }` sin loguear el mensaje real de la excepción — ver sección 5 |
| 12 | Formateo FAT32 | `Core/Pipeline/Pasos/PasoFormatearSd.cs` + `Hardware/ParticionadorDiscos.cs` | **A** | Inicio/éxito/fallo con letra, modo, etiqueta; distingue cancelación de error |
| 13 | Particionado (simple / emuMMC) | `Core/Pipeline/Pasos/PasoFormatearSd.cs` | **A** | Igual que #12, con tamaño de emuMMC en MB |
| 14 | Respaldo de llaves (SD?PC) | `Core/RespaldoLlavesLogic.cs` (`RespaldarAsync`) | **A** | Inicio, completado (núm. archivos), fallido, más un caso especial de bloqueo por downgrade (`Logger.Warning`) |
| 15 | Restauración de llaves (PC?SD) | `Core/RespaldoLlavesLogic.cs` (`RestaurarAsync`) | **A** | Inicio, completado, fallido, auto-omitido (discrepancia), más `Logger.Info` para omisiones parciales de `prod.keys` |
| 16 | Verificación criptográfica de llaves | `Core/RespaldoLlavesLogic.cs` (`AnalizarMasterKeys`/verificación bis_key_00) | **A** (según índice: `RespaldoLlavesVerificado`/`RespaldoLlavesDiscrepancia`) | No releído en detalle esta sesión, coherente con el resto del archivo |
| 17 | RP2040 — detección | `Core/Rp2040Logic.cs` (`EsRp2040`, `DetectarLetraRp2040`) | **C** | Ambos métodos usan `catch { return false/null; }` sin logear — una detección fallida es indistinguible de "no hay RP2040 conectado". Falta `Logger.Rp2040Detectado(letra)` documentado en el índice pero no se encontró la llamada real en el flujo de detección inspeccionado |
| 18 | RP2040 — flasheo | `Core/Rp2040Logic.cs` (`FlashearAsync`) | **A** | Inicio, completado, fallido (con excepción); cancelación devuelve error sin logear como fallo técnico — correcto |
| 19 | RP2040 — guardar en PC | `Core/Rp2040Logic.cs` (`GuardarEnPcAsync`) | **C** | Solo logea éxito (`Rp2040GuardadoEnPc`); el `catch (Exception ex)` **no logea el error**, solo devuelve `Resultado.Error(ex.Message)` — inconsistente con `FlashearAsync`, que sí logea el fallo |
| 20 | Firmware interno emuMMC (detección) | `Core/NxNandManagerLogic.cs` | **C** | Ninguna llamada a `Logger` en todo el archivo. Errores de proceso (`CLI_EXECUTION_FAILED`), timeout, denegación de acceso, y salida inesperada del CLI se devuelven como `ResultadoFirmwareEmummc` pero nunca se registran en el log — solo se ven reflejados en la UI del panel derecho, sin rastro histórico |
| 21 | Descarga/gestión de herramienta NxNandManager | `Core/GestorHerramientaNxNandManager.cs` | No confirmado en esta sesión (no reabierto) | Candidato a revisar en próxima auditoría puntual — descarga un ZIP externo con validación SHA-256, mismo patrón de riesgo que #8 |
| 22 | Ejecución de comandos externos (paso genérico) | `Core/Pipeline/Pasos/PasoEjecutarCmd.cs` | **C** | Ejecuta cualquier proceso con permisos heredados (admin). No logea comando, argumentos, código de salida, ni excepción. Sin try/catch — si `Process.Start` falla, la excepción sube sin contexto específico de "qué comando falló" |
| 23 | Sincronización remota (Gist) | `Network/GistParser.cs` | **C** | **Cero** llamadas a `Logger` en todo el archivo. Revalidación en background con ETag: cualquier excepción se traga silenciosamente (ver sección 8). Fallos de JSON solo se muestran vía `Dialogos.Error`, sin log |
| 24 | Actualización de NX-Suite (detección/descarga/lanzamiento) | `Core/GestorActualizacion.cs`, `MainWindow.Actualizacion.cs` | **C** | **Cero** llamadas a `Logger`. Ni el inicio de descarga, ni el porcentaje, ni el lanzamiento del updater, ni el error final (`catch (Exception ex)` en `BtnActualizarAhora_Click` solo actualiza texto de UI) quedan registrados |
| 25 | Caché — eliminación de módulo/bóveda completa | `Core/SuiteController.cs` / `Core/GestorCache.cs` | **A** | `CacheModuloEliminado`, `CacheModuloErrorAlEliminar`, `CacheTotalEliminada` confirmados por índice |
| 26 | Limpieza de Micro SD | `Core/LimpiezaSDLogic.cs` | **A** | `LimpiezaSDIniciada/Completada/CompletadaConErrores/ElementoFallido` según índice |
| 27 | Validación de configuración remota (`ValidadorConfiguracion`) | `Core/ValidadorConfiguracion.cs` | No confirmado en esta sesión | Candidato a revisión puntual futura |
| 28 | Token de GitHub (guardar/cargar/borrar) | `Core/Configuracion/TokenGitHub.cs` | **B** | Solo se logea el fallo de `Guardar()` (warning); `Cargar()`/`Borrar()` fallan silenciosamente sin log — aceptable dado que no son operaciones destructivas críticas, pero un fallo de `Cargar()` puede degradar silenciosamente la validación de assets sin dejar rastro |

---

## 4. Cobertura actual — resumen por categoría

| Categoría | Cantidad aproximada | Ejemplos |
|---|---|---|
| **A — Cobertura correcta** | 14 | Descarga, extracción, copiado SD, pipeline instalación, formateo, particionado, respaldo/restauración de llaves, RP2040 flasheo, caché, limpieza SD, desinstalación (nivel `SuiteController`) |
| **B — Cobertura parcial** | 3 | Integridad SHA-256/GitHubAssetValidator (solo warnings de fallback), Token GitHub (solo fallo de guardado) |
| **C — Sin cobertura** | 10 | Pasos genéricos de filesystem (borrar/mover/crear), `UninstallLogic` (detalle de error), RP2040 detección + guardar en PC, firmware emuMMC (NxNandManagerLogic completo), `PasoEjecutarCmd`, sincronización Gist (`GistParser` completo), actualización de la app (`GestorActualizacion` + `MainWindow.Actualizacion.cs`) |
| **D — Logging excesivo/redundante** | 0 detectado | No se encontraron casos de logging duplicado excesivo en esta pasada; ver sección 8 para posibles solapamientos menores |
| **E — Riesgo de información sensible** | 0 confirmado | Ver sección 7 — no se detectó exposición real, pero hay un punto a vigilar en excepciones genéricas que podrían incluir rutas con datos si el mensaje de excepción las incluyera (bajo riesgo) |

---

## 5. Catches silenciosos relevantes

| Ubicación | Código | Severidad | Motivo |
|---|---|---|---|
| `Core/UninstallLogic.cs` (`DesinstalarAsync`) | `catch (Exception) { return false; }` | **ALTO** | Traga completamente la excepción; ni siquiera se captura el mensaje para que el llamador lo use en un log. El llamador (`SuiteController`) solo sabe "false", no el motivo real del fallo de desinstalación |
| `Core/Rp2040Logic.cs` (`GuardarEnPcAsync`) | `catch (Exception ex) { return Resultado.Error(ex.Message); }` | **MEDIO** | No logea el error (a diferencia de `FlashearAsync`, que sí lo hace); inconsistencia dentro de la misma clase |
| `Core/Rp2040Logic.cs` (`EsRp2040`, `DetectarLetraRp2040`) | `catch { return false/null; }` | **BAJO** | Aceptable como "no detectado", pero un error real de I/O (ej. permisos) queda indistinguible de "no hay RP2040" — bajo impacto porque es una operación de polling, no destructiva |
| `Network/GistParser.cs` (revalidación en background) | Excepción tragada silenciosamente según lo documentado previamente en esta sesión | **ALTO** | Un fallo de red o parseo en background nunca se ve, ni en log ni en UI — el usuario sigue con datos obsoletos sin saber por qué |
| `MainWindow.Actualizacion.cs` (`BtnActualizarAhora_Click`) | `catch (Exception ex) { TxtEstadoActualizacion.Text = $"Error: {ex.Message}"; ... }` | **MEDIO** | El error se muestra en UI pero no se persiste en el log — si el usuario cierra la app tras el fallo, no queda rastro para diagnóstico posterior |
| `Core/Pipeline/Pasos/PasoEjecutarCmd.cs` | Sin try/catch en absoluto | **MEDIO** | Un fallo de `Process.Start` (comando no encontrado, permisos) sube como excepción genérica sin contexto de qué comando/argumentos se intentaron ejecutar |
| `Services/Logger.cs` (todas las escrituras) | `try/catch` silencioso alrededor de I/O de disco | **BAJO** | Es el propio logger — aceptable por diseño (no queremos que un fallo de logging rompa la app), pero significa que un log corrupto/bloqueado es invisible |

---

## 6. Diferenciación error vs. cancelación del usuario

**Correctamente diferenciado:**
- `Core/ReglasLogic.cs` — `Logger.InstalacionCancelada` vs `Logger.InstalacionFallida`, con `catch (OperationCanceledException)` separado del `catch (Exception)` general.
- `Core/Pipeline/Pasos/PasoFormatearSd.cs` — `catch (OperationCanceledException) { throw; }` antes del catch genérico, evitando que una cancelación se registre como `FormateoFallido`/`ParticionadoFallido`.
- `Core/Rp2040Logic.cs` (`FlashearAsync`, `GuardarEnPcAsync`) — cancelación devuelve `Resultado.Error("Operación cancelada.")` sin pasar por el log de fallo técnico.
- `Core/NxNandManagerLogic.cs` — distingue timeout interno (`TimedOut`) de cancelación externa real (`throw new OperationCanceledException`), aunque **ninguno de los dos casos se logea** (ver sección 3, ítem 20).

**No se detectaron casos donde una cancelación de usuario se registre incorrectamente como error técnico** en el código revisado. El patrón `catch (OperationCanceledException)` antes del `catch (Exception)` genérico es consistente en los archivos con logging (`ReglasLogic`, `PasoFormatearSd`, `Rp2040Logic`).

---

## 7. Riesgos de información sensible

**No se encontró ningún caso de exposición directa de secretos.** Revisado específicamente:

| Fuente potencial | Resultado |
|---|---|
| `Core/Configuracion/TokenGitHub.cs` | Solo registra `"No se pudo guardar el token"` (warning) — nunca el valor del token |
| `Core/GitHubAssetValidator.cs` | Logea fallos de API/hash con mensajes genéricos (ej. "no se pudo obtener digest remoto") — no incluye el token de autorización ni el header `Authorization` |
| `Core/RespaldoLlavesLogic.cs` | Logea nombres de archivo (`prod.keys`, `BISKEYS.bin`), rutas de destino, número de entradas — **nunca el contenido** de las llaves. `RespaldoLlavesVerificado`/`RespaldoLlavesDiscrepancia` solo indican el resultado booleano de la comparación, no los bytes comparados |
| `Core/NxNandManagerLogic.cs` | La salida cruda del CLI (`SalidaCruda`) se guarda en el `ResultadoFirmwareEmummc` pero **no se envía al Logger** en ningún punto revisado — el riesgo real aquí es que si en el futuro se decide logear `SalidaCruda` sin filtrar, esa salida podría en teoría incluir rutas o metadatos de la NAND (bajo riesgo, pero a vigilar si se implementa logging en esta clase) |
| Excepciones genéricas (`ex.Message`) en general | La mayoría de los `Logger.Warning/Error(..., ex)` pasan `ex.Message` o la excepción completa. No se detectó ningún caso donde una excepción pudiera razonablemente incluir contenido de `prod.keys`/tokens en su mensaje — los mensajes de excepción típicos (I/O, red, parseo) no incluyen contenido de archivos leídos |

**Conclusión:** no hay hallazgos clasificables como **E — riesgo confirmado** en el código
revisado. Sí existe un **riesgo latente a vigilar** si en una futura fase se añade logging a
`NxNandManagerLogic` sin filtrar `SalidaCruda`.

---

## 8. Logs redundantes / duplicados

No se detectó una duplicación clara de 3+ logs equivalentes para la misma operación (Core +
Pipeline + MainWindow) en los archivos revisados. El patrón dominante es:

- **Pipeline (`Paso*`)** logea el detalle específico del paso (ej. `DescargaCompletada` con
  tamaño en bytes).
- **`ReglasLogic`** logea el resultado global del pipeline (`InstalacionCompletada`), que es
  un nivel de abstracción distinto (no redundante, complementario).
- **`MainWindow.*.cs`** generalmente **no** vuelve a logear lo que ya logeó `Core`/`Pipeline` —
  se limita a actualizar la UI. Esto es correcto y evita duplicación.

**Posible solapamiento menor detectado:** en `RespaldoLlavesLogic.RestaurarAsync`, además de
`Logger.RestauracionLlavesCompletada`/`Fallida`, existe un `Logger.Info($"[RestaurarAsync] ...")`
adicional para el caso de archivos omitidos (`omitidos.Count > 0`). No es estrictamente
redundante (aporta detalle distinto: qué se omitió y por qué), pero mezcla un log genérico
(`Logger.Info`) con los métodos semánticos específicos de la clase — inconsistencia de estilo
menor, no de contenido.

---

## 9. Procesos con mejor cobertura actual

1. **Pipeline de instalación** (`ReglasLogic` + `PasoDescargar` + `PasoFormatearSd`) — inicio,
   éxito, fallo y cancelación, con módulo/versión/letra de SD en cada mensaje.
2. **Respaldo y restauración de llaves** (`RespaldoLlavesLogic`) — el más exhaustivo del
   proyecto: cubre inicio, éxito, fallo, auto-omisión por discrepancia, y casos de guard de
   downgrade con `Logger.Warning` explícito.
3. **Formateo y particionado de SD** (`PasoFormatearSd`) — separación clara de modos (solo
   FAT32 / simple / emuMMC) con logging específico por modo.
4. **Limpieza de Micro SD** y **gestión de caché** — según el índice, cobertura completa de
   inicio/éxito/error a nivel de elemento individual.

## 10. Procesos con peor cobertura actual

1. **Actualización de la propia app** (`GestorActualizacion.cs` / `MainWindow.Actualizacion.cs`)
   — cero logs; es una operación crítica (reemplaza el ejecutable) sin ningún rastro.
2. **Sincronización con el Gist remoto** (`GistParser.cs`) — cero logs; es la fuente de verdad
   de toda la configuración remota (catálogo, tablas de compatibilidad, etc.) y un fallo
   silencioso aquí puede explicar comportamientos extraños reportados por usuarios sin dejar
   ninguna pista.
3. **Detección de firmware interno de emuMMC** (`NxNandManagerLogic.cs`) — cero logs pese a
   ejecutar un proceso externo con parsing de salida y múltiples condiciones de error
   (timeout, acceso denegado, salida inesperada).
4. **Ejecución de comandos externos genéricos** (`PasoEjecutarCmd.cs`) — sin logging y sin
   manejo de excepción; dado que estos comandos corren con privilegios de administrador
   heredados, es el punto de mayor "caja negra" del pipeline.
5. **Pasos de filesystem genéricos** (`PasoBorrarArchivos`, `PasoBorrarCarpetas`,
   `PasoBorrarCarpetasVacias`, `PasoMoverArchivo`, `PasoCrearCarpeta`, `PasoCrearIni`,
   `PasoCrearTxt`) — sin logging propio; el detalle de "qué archivo específico" se pierde si
   el pipeline falla en uno de estos pasos.

---

## 11. Recomendaciones (sin implementar)

- Priorizar los **huecos totales (C)** sobre mejorar los que ya tienen cobertura parcial (B).
- Mantener el patrón de métodos semánticos del `Logger` existente (ej. `Logger.ActualizacionIniciada(...)`,
  `Logger.GistSincronizacionFallida(...)`) en lugar de introducir un formato nuevo.
- Para `PasoEjecutarCmd`, registrar como mínimo el comando (sin argumentos si pudieran
  contener datos sensibles de configuración) y el resultado, ya que corre con privilegios
  elevados.
- Para `NxNandManagerLogic`, registrar inicio/resultado sin volcar `SalidaCruda` completa al
  log (o truncarla) para evitar el riesgo latente descrito en la sección 7.
- Unificar el estilo en `RespaldoLlavesLogic.RestaurarAsync` (usar un método semántico dedicado
  en vez de `Logger.Info` genérico para el caso de "archivos omitidos").
- Revisar `Core/GestorHerramientaNxNandManager.cs` y `Core/ValidadorConfiguracion.cs` en una
  próxima pasada puntual (no se reabrieron en detalle en esta sesión).

---

## 12. Plan LOGGING por fases (propuesto, no aprobado para implementar)

### LOGGING 1 — Huecos totales de mayor impacto y menor riesgo — ? COMPLETADO
- Añadir logging de inicio/éxito/fallo en:
  - `GestorActualizacion.DescargarActualizacionAsync` / `LanzarActualizador` — implementado en el
    llamador (`MainWindow.Actualizacion.cs`), que es quien conoce el resultado real de ambos pasos.
  - `MainWindow.Actualizacion.cs` (`BtnActualizarAhora_Click`) — añadidos
    `Logger.ActualizacionDescargaIniciada`, `Logger.ActualizacionDescargaCompletada`,
    `Logger.ActualizacionActualizadorIniciado` y `Logger.ActualizacionFallida` (en el `catch` existente).
  - `Network/GistParser.cs` — añadidos `Logger.GistSincronizacionIniciada` (inicio de
    `ObtenerTodoElGistAsync`), `Logger.GistConfiguracionActualizada` (revalidación en background con
    cambios), `Logger.GistRevalidacionFallida` (antes catch silencioso en `RevalidarConETagAsync`),
    `Logger.GistUsoCacheOffline` (fallback a caché válida) y `Logger.GistSincronizacionFallida`
    (sin caché disponible / caché dañada / JSON inválido).
- Riesgo: bajo — son puntos nuevos de logging, no se tocó lógica de negocio, caché, revalidación,
  timeouts ni comportamiento offline existente. Build verificado antes y después: compilación correcta.

### LOGGING 2 — Catches silenciosos importantes
- `Core/UninstallLogic.cs` — loguear `ex.Message` antes de retornar `false`.
- `Core/Rp2040Logic.cs` (`GuardarEnPcAsync`) — loguear fallo igual que `FlashearAsync`.
- `Core/Pipeline/Pasos/PasoEjecutarCmd.cs` — envolver en try/catch y loguear comando + resultado.
- Riesgo: bajo-medio — cambia el flujo de excepciones en un punto (`UninstallLogic`), requiere
  cuidado de no alterar el valor de retorno booleano existente.

### LOGGING 3 — Seguridad / secretos
- Añadir logging a `NxNandManagerLogic` sin volcar `SalidaCruda` sin filtrar.
- Revisar `GestorHerramientaNxNandManager.cs` antes de tocarlo (pendiente de auditoría puntual).
- Riesgo: medio — requiere decidir qué parte de la salida del CLI es segura de registrar.

### LOGGING 4 — Reducción de duplicación / consistencia de estilo
- Unificar `Logger.Info` genérico en `RespaldoLlavesLogic.RestaurarAsync` a un método semántico
  dedicado.
- Riesgo: bajo — cambio cosmético de estilo, no de comportamiento.

Cada fase deberá seguir el proceso ya acordado: build antes ? cambios aprobados ? build después
? prueba relacionada ? actualizar `CODEBASE_INDEX.md` ? actualizar este documento ? commit
independiente ? detenerse.

---

## 13. Riesgos y pruebas necesarias (generales, para cuando se apruebe una fase)

- **Riesgo principal:** cualquier fase que toque `catch` existentes debe verificar que no se
  altere el valor de retorno (`bool`/`Resultado`) que consumen los llamadores — el objetivo es
  añadir logging, no cambiar el control de flujo.
- **Pruebas sugeridas por fase:**
  - LOGGING 1: forzar un fallo de red durante sincronización de Gist y verificar que aparece en
    el log; simular una actualización de app (o al menos revisar manualmente el código) para
    confirmar que el log de descarga/lanzamiento aparece.
  - LOGGING 2: forzar un fallo de desinstalación (ej. archivo bloqueado) y verificar que el
    mensaje de error real aparece en el log en vez de solo `false`.
  - LOGGING 3: ejecutar detección de firmware emuMMC con y sin `prod.keys` válido y confirmar
    que no aparece contenido sensible en el log resultante.
  - LOGGING 4: verificar visualmente que el log de restauración de llaves con archivos omitidos
    sigue mostrando la misma información, solo con el método semántico nuevo.

---

*Documento generado como parte de la auditoría de logging de procesos críticos. No se
modificó código de producción, XAML, ni el Gist remoto.*
