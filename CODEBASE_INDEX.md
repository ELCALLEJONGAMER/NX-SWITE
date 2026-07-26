# NX-Suite — Índice de Código para Agentes de IA

> ?? **INSTRUCCIÓN OBLIGATORIA PARA AGENTES DE IA**
> Este archivo **debe mantenerse actualizado**. Si se mueve, edita, elimina o añade cualquier archivo,
> clase, método o propiedad pública, **actualiza este índice en el mismo commit/PR**.
> Los agentes deben leer este archivo primero y limitar sus búsquedas a las rutas aquí indicadas.
> No buscar en `bin/`, `obj/`, `NewFolder1/`, `NewFolder2/`.

---

## ?? Estructura de proyectos

| Proyecto | Ruta | Descripción |
|---|---|---|
| `NX-Suite` | `NX-Suite/NX-Suite.csproj` | App WPF principal (.NET 8, `net8.0-windows`) |
| `NX-Suite.Updater` | `NX-Suite.Updater/NX-Suite.Updater.csproj` | Ejecutable de actualización autónomo (self-contained) |

---

## ??? Mapa de carpetas (NX-Suite)

```
NX-Suite/
??? Core/                   # Toda la lógica de negocio (sin UI)
?   ??? Config/             # Modelos de configuración (local, remota, sonidos)
?   ??? Controles/          # (vacío, reservado)
?   ??? Models/             # Modelos de datos del dominio
?   ??? Pipeline/           # Sistema de pasos de instalación
?   ?   ??? Pasos/          # Implementaciones de cada IPasoPipeline
?   ??? *.cs                # Servicios, lógica, controladores
??? Hardware/               # Acceso a disco, particionado, notificaciones
?   ??? Native/             # P/Invoke y wrappers nativos
??? UI/
?   ??? Controles/          # UserControls reutilizables y ViewModels asociados
?   ?   ??? VistaAsistida/  # ViewModels del modo asistido
?   ??? Converters/         # IValueConverter de WPF
?   ??? Dialogos.cs         # Helpers de diálogos
?   ??? Estilos/            # XAML de estilos globales (colores, botones, tarjetas…)
?   ??? VentanaDependencias.*
?   ??? VentanaPersonalizacion.*
?   ??? VentanaSplash.*
??? Assets/                 # Imágenes de recursos embebidos
??? Services/               # Servicios transversales (Logger)
??? MainWindow.*.cs         # Partial classes de MainWindow (una por dominio funcional)
???   MainWindow.Ajustes.cs      # Tabs: Sonido, Caché, Carpetas Protegidas
???   MainWindow.LimpiezaSD.cs   # Overlay "Limpiar Micro SD": proteger/desproteger inline, confirmar con hold-to-confirm
??? MainWindow.xaml
??? App.xaml / App.xaml.cs
??? HexToBrushConverter.cs
```

---

## ?? Services — Logger

### `Services/Logger.cs` — `static class Logger`
Sistema de log por sesiones. Ruta: `%AppData%\NX-Suite\NX-Suite.log` (junto a `preferencias.json`).

**Gestión de sesiones:**
- `IniciarSesion()` — llamado en `App.xaml.cs` al arrancar. Escribe cabecera con fecha/hora, versión de la app y versión de Windows (WMI). Recorta automáticamente a las últimas **5 sesiones**.
- Thread-safe mediante `lock` en todas las escrituras.

**Métodos genéricos:**
| Método | Nivel | Descripción |
|---|---|---|
| `Info(mensaje)` | `INFO ` | Información general |
| `Warning(mensaje)` | `WARN ` | Advertencia |
| `Error(mensaje, ex?)` | `ERROR` | Error con excepción opcional |

**Métodos semánticos — Descarga:**
| Método | Descripción |
|---|---|
| `DescargaIniciada(modulo, url)` | Inicio de descarga |
| `DescargaCompletada(modulo, bytes)` | Éxito con tamaño formateado |
| `DescargaOmitida(modulo, archivo)` | Caché válida, se omite |
| `DescargaFallida(modulo, url, ex)` | Error con detalle de excepción |

**Métodos semánticos — Extracción:**
| Método | Descripción |
|---|---|
| `ExtraccionIniciada(modulo, archivoZip)` | Inicio de descompresión |
| `ExtraccionCompletada(modulo, archivos)` | Éxito con núm. de archivos |
| `ExtraccionOmitida(modulo, carpeta)` | Ya extraído, se omite |
| `ExtraccionFallida(modulo, archivoZip, ex)` | Error |

**Métodos semánticos — Copiado a SD:**
| Método | Descripción |
|---|---|
| `CopiadoIniciado(modulo, destino)` | Inicio de copia |
| `CopiadoCompletado(modulo, archivos)` | Éxito con núm. de archivos |
| `CopiadoFallido(modulo, ex)` | Error |

**Métodos semánticos — Pipeline / instalación:**
| Método | Descripción |
|---|---|
| `InstalacionIniciada(modulo, version, letraSD)` | Inicio del pipeline |
| `InstalacionCompletada(modulo, version)` | Éxito |
| `InstalacionFallida(modulo, version, error)` | Error |
| `InstalacionCancelada(modulo, version)` | Cancelada por el usuario |

**Métodos semánticos — Formateo / particionado:**
| Método | Descripción |
|---|---|
| `FormateoIniciado(letraSD, modo, etiqueta)` | Inicio de formato FAT32 |
| `FormateoCompletado(letraSD)` | Éxito |
| `FormateoFallido(letraSD, ex)` | Error |
| `ParticionadoIniciado(letraSD, modo, emuMB)` | Inicio de particionado |
| `ParticionadoCompletado(letraSD)` | Éxito |
| `ParticionadoFallido(letraSD, ex)` | Error |

**Métodos semánticos — Hekate / Personalización:**
| Método | Descripción |
|---|---|
| `HekateIconAplicado(ini, tipo, secciones)` | Icono aplicado a N secciones |
| `HekateIconSinCambios(ini, tipo)` | Ningún match de sección |
| `HekateValorEstablecido(ini, seccion, clave, valor)` | Valor escrito en `.ini` |
| `HekateArchivoNoEncontrado(ini)` | Archivo `.ini` no existe en SD |

**Métodos semánticos — Desinstalación:**
| Método | Descripción |
|---|---|
| `DesinstalacionIniciada(modulo, letraSD)` | Inicio |
| `DesinstalacionCompletada(modulo)` | Éxito |
| `DesinstalacionFallida(modulo)` | Error |

**Métodos semánticos — Caché:**
| Método | Descripción |
|---|---|
| `CacheModuloEliminado(modulo)` | Caché borrada desde Ajustes |
| `CacheModuloErrorAlEliminar(modulo)` | Error al borrar |
| `CacheTotalEliminada()` | Bóveda completa borrada |

**Métodos semánticos — Limpieza SD:**
| Método | Descripción |
|---|---|
| `LimpiezaSDIniciada(letraSD, elementos)` | Inicio con núm. de elementos a borrar |
| `LimpiezaSDCompletada(letraSD)` | Éxito |
| `LimpiezaSDCompletadaConErrores(letraSD, errores)` | Completada con errores |
| `LimpiezaSDElementoFallido(nombre, error)` | Error por elemento individual |

**Métodos semánticos — RP2040 / Picofly:**
| Método | Descripción |
|---|---|
| `Rp2040Detectado(letra)` | Chip detectado al conectar |
| `Rp2040FlasheoIniciado(letra, urlFirmware)` | Inicio de flasheo |
| `Rp2040FlasheoCompletado(letra)` | Éxito |
| `Rp2040FlasheoFallido(letra, ex)` | Error con excepción |
| `Rp2040GuardadoEnPc(rutaDestino)` | Firmware guardado en PC |

**Métodos semánticos — Respaldo de llaves:**
| Método | Descripción |
|---|---|
| `RespaldoLlavesIniciado(serial, rutaDestino)` | Inicio de copia |
| `RespaldoLlavesCompletado(serial, archivos, rutaDestino)` | Éxito con número de archivos |
| `RespaldoLlavesFallido(serial, error)` | Error |
| `RespaldoLlavesVerificado(serial)` | bis_key_00 coincide |
| `RespaldoLlavesDiscrepancia(serial)` | bis_key_00 no coincide — advertencia |

**Integracón:**
- `App.xaml.cs` — `IniciarSesion()` al arrancar
- `PasoDescargar`, `PasoExtraer`, `PasoCopiarSD` — logs de descarga, extracción y copiado
- `PasoFormatearSd` — logs de formateo y particionado
- `PasoHekateSetIcon`, `PasoHekateSetValue` — logs de personalización Hekate
- `ReglasLogic` — logs de instalación completa/fallida/cancelada
- `SuiteController` — logs de desinstalación y limpieza de caché
- `LimpiezaSDLogic` — logs de limpieza SD
- `MainWindow.Detalle.cs` — logs de eliminación de caché por versión desde la vista de detalle

**Métodos de lectura para el visor:**
| Método | Descripción |
|---|---|
| `ObtenerSesiones()` | Parsea el log y devuelve `List<SesionLog>` ordenada más reciente primero |
| `ObtenerTextoCompleto()` | Devuelve el contenido completo del log como `string` |
| `LimpiarLog()` | Borra todo el contenido del archivo de log (thread-safe) |

**Modelos del visor** (en `Services/Logger.cs`):
- `SesionLog` — `Fecha`, `Lineas` (`List<LineaLog>`), `Titulo` (formateado), `TieneErrores`
- `LineaLog` — `Nivel`, `Mensaje`, `TextoCompleto`

---

## ?? Core — Servicios y lógica principal

### `Core/Servicios.cs` — `static class Servicios`
Punto de acceso global (singleton lazy) a los servicios principales.
| Propiedad | Tipo | Descripción |
|---|---|---|
| `Sonidos` | `GestorSonidos` | Servicio de audio |
| `Iconos` | `GestorIconos` | Caché de iconos remotos |
| `Cola` | `GestorQueue` | Cola de tareas de instalación |
| `Actualizacion` | `ServicioActualizacion` | Estado de actualización disponible |
| `Reemplazar(...)` | método | Sustituye instancias en tests |

---

### `Core/SuiteController.cs` — `class SuiteController : ISuiteController`
Controlador principal de la aplicación. Orquesta red, SD y pipeline.
| Miembro | Descripción |
|---|---|
| `GistActualizadoEnBackground` | Evento `Action<GistData>` — pasa a través de `GistParser.GistActualizadoEnBackground`. |
| `SincronizarTodoAsync(urlGist, letraSD[, ct])` | Descarga y sincroniza el catálogo remoto (Gist) |
| `ObtenerUnidadesRemoviblesAsync()` | Lista unidades SD detectadas |
| `ObtenerInfoPanel(unidad, modulos)` | Datos para el panel derecho. Detecta `MasterKeyMaxima` de `prod.keys` vía `DetectarMasterKeyMaxima()`; obtiene `FirmwareCompatible`/`AtmosphereDesde` de `MasterKeyTable.BuscarSoloRemota()`. Puebla también `DiscoFisico` y `RutaProdKeys` en `InfoPanelDerecho` para uso posterior por `ObtenerFirmwareEmummcAsync`. **Síncrono, no invoca NxNandManager.** |
| `ObtenerFirmwareEmummcAsync(info, ct)` | Detecta el firmware interno de la emuMMC RAW del disco físico de `info` vía `NxNandManagerLogic`. Pensado para llamarse **después** de `ObtenerInfoPanel` (no bloquea el resto del panel). Devuelve `KeysMissing` de inmediato si `info.HayProdkeys == false`. |
| `DetectarMasterKeyMaxima(rutaProdkeys)` | (privado) Lee `prod.keys` y devuelve el nombre de la master key de mayor índice presente, sin consultar la tabla de compatibilidad. |
| `InstalarModuloAsync(...)` | Ejecuta pipeline de instalación (sobrecarga con `CancellationToken`). |
| `DesinstalarModuloAsync(modulo, letraSD)` | Desinstala módulo de la SD. |
| `LimpiarCacheModulo(modulo)` | Borra caché local del módulo. |
| `LimpiarTodaLaBoveda()` | Borra toda la bóveda de caché. |
| `ActualizarEstadoCacheCatalogo(catalogo)` | Sincroniza estado instalado en el catálogo |
| `ObtenerPesoCacheZips()` | Peso total en bytes de los ZIPs en caché |
| `ObtenerPesoCacheExtraccion()` | Peso total en bytes del contenido extraído en caché |
| `FiltrarPorEtiqueta(modulos, etiqueta)` | Filtro por etiqueta |
| `FiltrarPorTexto(modulos, busqueda)` | Filtro por texto libre |
| `RefrescarEstadosSinRedAsync(modulos, letraSD)` | Refresca estados sin conectarse a internet |
| `AnalizarLimpiezaSD(letraSD, protegidos)` | Devuelve qué se borraría vs. qué se conservaría, sin escribir nada |
| `LimpiarMicroSDAsync(letraSD, protegidos, progreso, ct)` | Borra todo lo no protegido de primer nivel de la SD |

---

### `Core/SuiteControllerFacade.cs` — `class SuiteControllerFacade : ISuiteController`
Decorador/fachada de `SuiteController`. Misma interfaz, úsalo para pruebas o throttling. Delega `GistActualizadoEnBackground` a `_inner`.

---

### `Network/GistParser.cs` — `class GistParser`
Descarga y parsea el JSON del Gist remoto. Estrategia **Stale-While-Revalidate con ETag**.
| Miembro | Descripción |
|---|---|
| `GistActualizadoEnBackground` | `event Action<GistData>?` — se dispara cuando la revalidación condicional (`If-None-Match`) detecta HTTP 200 (Gist cambió), tras guardar el nuevo JSON y ETag en disco. Los suscriptores deben volver al hilo UI antes de tocar controles. |
| `ObtenerTodoElGistAsync(urlGistRaw)` | 1) Si hay caché ? devuelve inmediatamente y revalida en background. 2) Sin caché ? descarga completa. 3) Sin red y sin caché ? aviso al usuario. |

**Flujo de actualización en background:**
```
Gist cambia ? HTTP 200 ? guarda disco ? GistActualizadoEnBackground(nuevaData)
  ? MainWindow.OnGistActualizadoEnBackground (hilo UI)
  ? AplicarConfiguracionRemota ? MasterKeyTable.AplicarRemota + ModeloSwitchTable.AplicarRemota
  ? panel derecho repoblado con datos frescos sin reiniciar
```

---

### `Core/SuiteController.cs` — `class SuiteController : ISuiteController`
Contrato público del controlador. **Punto de referencia para extensiones**. Incluye `event Action<GistData>? GistActualizadoEnBackground` — propaga el evento homónimo de `GistParser`.

---

### `Core/ServicioActualizacion.cs` — `class ServicioActualizacion : INotifyPropertyChanged`
| Propiedad/Método | Descripción |
|---|---|
| `HayActualizacion` | `bool` — hay actualización disponible |
| `VersionActual`, `VersionRemota`, `UrlDescarga`, `NotasVersion` | Metadatos de versión |
| `Evaluar(versionRemota, urlDescarga, notas)` | Compara y actualiza estado |

---

### `Core/Configuracion/PreferenciasUsuario.cs`
| Sección | Tipo | Descripción |
|---|---|---|
| `Sonido` | `SeccionSonido` | Switches por evento y volumen |
| `LimpiezaSD` | `SeccionLimpiezaSD` | `EntradasProtegidas List<string>` — carpetas/archivos protegidos del borrado (default: `emuMMC`, `Nintendo`, `roms`) |
| Método | Descripción |
|---|---|
| `EsVersionNueva(actual, remota)` | Compara versiones semánticas |
| `DescargarActualizacionAsync(...)` | Descarga el instalador de actualización |
| `LanzarActualizador(...)` | Lanza `NX-Suite.Updater.exe` |

---

### `Core/GestorCache.cs` — `class GestorCache`
Gestiona las rutas y operaciones de caché local (Gist, iconos, sonidos, zips).
Al borrar la caché de un módulo también elimina los archivos sidecar `<archivo>.version`.

---

### `Core/GestorHerramientaNxNandManager.cs` — `static class GestorHerramientaNxNandManager`
Descarga, verifica (SHA-256) y cachea por versión el CLI de NxNandManager, usado para leer
el firmware interno de la emuMMC. El asset publicado en el Gist es un **ZIP** (no el ejecutable
directo) que incluye además dependencias nativas necesarias en tiempo de ejecución (p.ej.
`dokan1.dll`). Almacena en `%AppData%\NX-Swite\Tools\NxNandManager\<version>\`
(rutas centralizadas en `ConfiguracionLocal`).
| Miembro | Descripción |
|---|---|
| `ObtenerRutaEjecutableAsync(ct)` | Lee la configuración desde `ConfiguracionRemota.Tools` (sección `"tools"` del Gist). Si el ejecutable final ya existe, lo reutiliza sin red. Si no: normaliza la URL (convierte `github.com/.../blob/...` a `raw.githubusercontent.com/...`), descarga el ZIP, valida su SHA-256, lo extrae a una carpeta temporal dentro de la versión, localiza recursivamente el ejecutable indicado por `CLI_NX_NAND_MANAGER_EXECUTABLE` y **mueve TODO el contenido de esa carpeta** (ejecutable + DLLs vecinas como `dokan1.dll`) de forma atómica al destino final. Limpia siempre el ZIP temporal y la carpeta de extracción en el `finally`. Lanza `HerramientaNoDisponibleException` si la config remota está incompleta, la descarga falla, el hash no coincide, la extracción falla o el ZIP no contiene el ejecutable esperado. |

**`HerramientaNoDisponibleException`** (mismo archivo): excepción específica para mapear fallos de descarga/validación al estado `ToolValidationFailed` de la UI sin depender de mensajes de texto.

> ?? **Nota de mantenimiento:** la caché solo comprueba `File.Exists(rutaExeFinal)`. Si en el futuro el ZIP publicado en el Gist cambia sus DLLs vecinas sin cambiar `CLI_NX_NAND_MANAGER_VERSION`, los usuarios con caché previa no recibirán las DLLs nuevas hasta borrar manualmente la carpeta de versión.

---

### `Core/NxNandManagerLogic.cs` — `static class NxNandManagerLogic`
Detección de firmware interno de una emuMMC **RAW** (partición `\\.\PhysicalDriveN`, id `E0`)
mediante el CLI de NxNandManager. La emuMMC basada en archivo (`emuMMC/RAW1` en la SD FAT32)
no está soportada todavía. Únicos argumentos permitidos: `-i`, `-keyset`, `--info` — ninguna
operación de escritura/restauración/copia debe añadirse a esta clase.
| Miembro | Descripción |
|---|---|
| `ObtenerFirmwareRawAsync(numeroDiscoFisico, rutaProdKeys, ct)` | Comprueba privilegios de administrador (`WindowsPrincipal.IsInRole`, defensa adicional — el manifest ya exige admin), asegura el CLI vía `GestorHerramientaNxNandManager`, ejecuta `NxNandManager.exe -i \\.\PhysicalDriveN -keyset <prod.keys> --info` con `ArgumentList` y devuelve `ResultadoFirmwareEmummc`. |

**Ejecución del proceso:** `UseShellExecute=false`, `RedirectStandardOutput/Error=true`, `CreateNoWindow=true`, lectura vía `ReadToEndAsync`/`WaitForExitAsync`. Timeout centralizado en `ConfiguracionLocal.TimeoutCliNxNandManager` (45s) combinado con el `CancellationToken` externo (cambio de unidad / cierre de ventana) mediante `CancellationTokenSource.CreateLinkedTokenSource`. Al expirar el timeout mata el proceso con `Kill(entireProcessTree: true)` y devuelve `TimedOut`; una cancelación externa se propaga como `OperationCanceledException` sin marcarla como error.

**Parseo de salida** (regex tolerante a espacios, `RegexOptions.Multiline`):
- `Firmware ver.\s*:\s*(?<version>[0-9.]+)` ? `Detected` con la versión capturada.
- Si no hay match pero existe línea `NAND type` ? `FirmwareNotDetected` (NAND leída pero sin firmware identificable; **no** se considera corrupta).
- Si no hay match de ninguna de las dos ? `Failed` (salida inesperada).

---

### `Core/LimpiezaSDLogic.cs` — `class LimpiezaSDLogic`
Lógica de limpieza de la Micro SD: escanea primer nivel, separa protegidos y ejecuta borrado.
| Miembro | Descripción |
|---|---|
| `Analizar(letraSD, protegidos)` | Devuelve `AnalisisLimpiezaSD` sin escribir nada. Ordena carpetas primero, luego archivos/comprimidos, dentro de cada grupo alfabéticamente |
| `EjecutarAsync(letraSD, protegidos, progreso, ct)` | Borra todo lo no protegido del primer nivel de la SD. Registra en log inicio, elementos fallidos individualmente y resultado final. |

**Modelos relacionados** (en `Core/LimpiezaSDLogic.cs`):
- `AnalisisLimpiezaSD` — listas `ABorrar` y `AConservar` de tipo `EntradaSD`
- `EntradaSD` — `Nombre`, `Tipo` (`EsTipoEntrada`), `Icono`, `EsCritico` (true si nombre está en `NombresCriticos` y tipo es `Carpeta`)
  - `static NombresCriticos` — `HashSet<string>` con `{ "emuMMC", "Nintendo" }` — fuente centralizada de carpetas críticas
- `EsTipoEntrada` — `Carpeta` / `Archivo` / `Comprimido` (comprimidos detectados por `ZipLogic.ExtensionesComprimidas`)
| Miembro | Descripción |
|---|---|
| `RutaBovedaZips`, `RutaBovedaExtraccion`, `RutaCacheGist`, `RutaCacheIconos`, `RutaCacheSonidos` | Rutas de caché |
| `GuardarJsonGistAsync(json)` | Persiste el JSON del Gist |
| `CargarJsonGistAsync()` | Carga el JSON cacheado |
| `TieneCacheGist` | `bool` |
| `CacheGistEsValido(ttlHoras)` | Valida TTL |
| `CalcularPesoZips()` | Peso total en bytes de todos los ZIPs en bóveda (excluye sidecars `.version`) |
| `CalcularPesoExtraccion()` | Peso total en bytes de todos los archivos extraídos (excluye sidecars `.version`) |

---

### `Core/GestorQueue.cs` — `class GestorQueue : INotifyPropertyChanged`
Cola observable de tareas de instalación mostrada en la UI.
| Miembro | Descripción |
|---|---|
| `Items` | `ObservableCollection<ItemQueue>` |
| `ContadorActivos`, `TieneActivos`, `TieneItems` | Estado de la cola |
| `AgregarItem(titulo)` | Crea y agrega un `ItemQueue` |
| `ActualizarItem(item, progreso, mensaje)` | Actualiza progreso |
| `CompletarItem(item)` / `ErrorItem(item, msg)` / `CancelarItem(item)` | Transiciones de estado |
| `LimpiarCompletados()` | Elimina los finalizados |

---

### `Core/GestorSonidos.cs` — `sealed class GestorSonidos`
| Miembro | Descripción |
|---|---|
| `Configurar(rutaCacheSonidos)` | Establece ruta de caché de sonidos |
| `InicializarAsync(config)` | Carga los sonidos desde `SonidosConfig` |
| `Reproducir(EventoSonido)` | Reproduce un evento de sonido |
| `TieneCache(EventoSonido)` | Verifica si el sonido está en caché |
| `enum EventoSonido` | `Intro, Cerrar, Click, Hover, Instalar, Exito, Error, Navegacion` |

---

### `Core/GestorIconos.cs` — `class GestorIconos`
| Método | Descripción |
|---|---|
| `ObtenerRutaLocal(url)` | Ruta local del icono cacheado (o `null`) |
| `DescargarSiNoExisteAsync(url)` | Descarga si no está en caché |
| `DescargarTodosAsync(urls)` | Descarga múltiples iconos en paralelo |

---

### `Core/DownloadLogic.cs` — `class DownloadLogic`
| Método | Descripción |
|---|---|
| `DescargarArchivoAsync(url, rutaDestino, progreso, ct)` | Descarga con reporte de progreso |

---

### `Core/ZipLogic.cs` — `class ZipLogic`
| Miembro | Descripción |
|---|---|
| `ExtensionesComprimidas` | `HashSet<string>` — extensiones soportadas |
| `ExtraerTodoAsync(...)` | Extrae archivo comprimido |
| `LimpiarTemporales(rutaArchivo, rutaCarpetaExtraida)` | Limpia temporales |

---

### `Core/SHA256Logic.cs` — `class SHA256Logic`
| Método | Descripción |
|---|---|
| `ObtenerHashArchivo(rutaArchivo)` | Calcula SHA-256 del archivo |
| `ValidarIntegridad(rutaLocal, hashEsperado)` | Valida hash |

---

### `Core/HekateIniManager.cs` — `class HekateIniManager`
Parser/editor del archivo `hekate_ipl.ini`.
| Método | Descripción |
|---|---|
| `LoadAsync()` | Carga el `.ini` |
| `GetValue(section, key)` | Lee valor |
| `SetValue(section, key, value)` | Escribe valor |
| `ObtenerSeccionesConClave(clave, valor?)` | Busca secciones por clave |
| `SaveAsync()` | Persiste el `.ini` |

---

### `Core/ReglasLogic.cs` — `class ReglasLogic`
| Método | Descripción |
|---|---|
| `EjecutarPipelineAsync(pipeline, letraSD, progreso, ct, versionModulo, nombreModulo)` | Ejecuta el pipeline de un módulo. `versionModulo` se propaga a `ContextoPipeline`. `nombreModulo` se usa para los logs semánticos de inicio/fin/error/cancelación. Inyecta un `GitHubAssetValidator` (con token DPAPI si existe) en el `ContextoPipeline`. |

---

### `Core/GitHubAssetValidator.cs` — `class GitHubAssetValidator`
Consulta la API de GitHub Releases para obtener el digest SHA256 de un asset y lo compara con el archivo en caché local.
| Miembro | Descripción |
|---|---|
| `GitHubAssetValidator(token?)` | Constructor; `token` es opcional (PAT). Sin token funciona en modo anónimo (60 req/h). |
| `ValidarAsync(urlDescarga, rutaArchivoLocal, ct)` | Devuelve `ResultadoValidacion` (`Valido` / `Desactualizado` / `NoDisponible`). Nunca lanza excepción ni bloquea la instalación. |
| `static EsUrlGitHub(url)` | Devuelve `true` si la URL pertenece a `github.com` o `objects.githubusercontent.com`. |

**Comportamiento híbrido / no forzoso:**
- Bypass automático si la URL no es de GitHub o no hay validador en el contexto.
- `NoDisponible` = sin red, token inválido, API sin digest, release antiguo ? instalación continúa desde caché.
- Solo `Desactualizado` provoca re-descarga.

**`enum ResultadoValidacion`** (en el mismo archivo): `Valido`, `Desactualizado`, `NoDisponible`.

---

### `Core/Configuracion/TokenGitHub.cs` — `static class TokenGitHub`
Almacena y recupera el token de GitHub mediante DPAPI (cifrado con la clave del perfil de Windows).
Ruta: `%AppData%\NX-Suite\github_token.dat`
| Método | Descripción |
|---|---|
| `Guardar(token?)` | Cifra con DPAPI y escribe en disco. Si `token` es nulo/vacío borra el archivo. |
| `Cargar()` | Descifra y devuelve el token, o `null` si no existe/está dañado. |
| `Borrar()` | Elimina el archivo cifrado. |
| `HayToken` | `bool` — indica si el archivo cifrado existe. |

---

### `Core/FiltroLogic.cs` — `static class FiltroLogic`
| Método | Descripción |
|---|---|
| `FiltrarPorEtiquetas(...)` | Filtra lista de módulos por múltiples etiquetas |
| `FiltrarPorEtiqueta(...)` | Filtra por una etiqueta |
| `FiltrarPorTexto(...)` | Filtra por texto libre |

---

### `Core/AnalizadorDependencias.cs` — `class AnalizadorDependencias`
Analiza dependencias entre módulos antes de instalar.
Método principal: `DeterminarVersionInstalada(rutaRaizSD, modulo)`

---

### `Core/ValidadorConfiguracion.cs` — `class ValidadorConfiguracion`
| Método | Descripción |
|---|---|
| `ValidarListaAsync(letraSD, lista)` | Valida lista de `ReglasConfig` |
| `ValidarAsync(letraSD, reglas)` | Valida una regla individual |

---

### `Core/UninstallLogic.cs` — `class UninstallLogic`
| Método | Descripción |
|---|---|
| `DesinstalarAsync(rutasABorrar, letraSD)` | Borra archivos/carpetas del módulo |

---

### `Core/CertificadoLayout.cs` — `internal static class CertificadoLayout`
Archivo de coordenadas pixel para superponer texto sobre `certificado_plantilla.png` (1491×1055).
Editar este archivo es suficiente para reposicionar cualquier campo del certificado sin tocar la lógica.
| Constante | Descripción |
|---|---|
| `ImagenAncho` / `ImagenAlto` | Dimensiones de la plantilla |
| `YGeneradoPor`, `XGeneradoPorValor` | Fila y columna del valor «NX-Swite vX.X.X» |
| `YFecha`, `XFechaValor` | Fila y columna de la fecha de generación |
| `YSerial`, `XSerialValor` | Fila y columna del número de serie |
| `YBiskeys`, `XBiskeyValor` | Fila de «BISKEYS :» y columna del estado (encontrado / no encontrado) |
| `YBiskey0`–`YBiskey3`, `XBiskeyHexValor` | Filas de cada `bis_key_00`–`bis_key_03` en hex y su columna |
| `YProdkeys`, `XProdkeysValor` | Fila y columna del valor de `prod.keys` |
| `YMasterKey`, `XMasterKeyValor` | Fila y columna del nombre de la master key máxima |
| `YCompatibilidad`, `XCompatibilidadValor` | Fila y columna del rango HOS compatible + Atmosphere desde |
| `YIntegridad`, `XIntegridadValor` | Fila y columna del estado de integridad de generaciones |
| `YVerificacion`, `XVerificacionValor` | Fila y columna del estado de verificación criptográfica |
| `XNotasValor`, `XNotasMax`, `YNotasLinea1`, `YNotasLineaH` | Bloque NOTAS: columna inicio, límite derecho (word-wrap), primera fila e interlineado |

---

### `Core/RespaldoLlavesLogic.cs` — `class RespaldoLlavesLogic`
Respaldo seguro de llaves de Nintendo Switch desde la Micro SD.
| Miembro | Descripción |
|---|---|
| `Analizar(letraSD)` | Escanea `atmosphere/automatic_backups` y `switch/prod.keys`. Verifica criptográficamente que `bis_key_00` de PRODINFO coincida con BISKEYS.bin. Devuelve `AnalisisRespaldoLlaves` sin escribir nada. |
| `RespaldarAsync(analisis)` | Copia los archivos detectados a `RutaRespaldosLlaves/{serial}/`. Genera `certificado.txt` con número de serie, verificación criptográfica, archivos respaldados y versión NX-Swite que generó el respaldo. Devuelve `ResultadoRespaldoLlaves`. |
| `RestaurarAsync(analisis, letraSD)` | Restaura desde el respaldo local más reciente del mismo serial a la SD. Devuelve `ResultadoRestauracionLlaves`. |
| `ListarRespaldosLocales()` | Devuelve `List<RespaldoLocal>` con los respaldos en `RutaRespaldosLlaves` ordenados por fecha descendente. |
| `RestaurarDesdeRespaldoLocalAsync(respaldo, letraSD)` | Restaura un `RespaldoLocal` específico seleccionado por el usuario a la SD. |
| `RespaldoEstaAlDia(analisis)` | Compara SD vs respaldo local; devuelve `bool`. |
| `ActualizarRespaldoSiSDTieneMasLlavesAsync(analisis)` | Si `prod.keys` de la SD tiene más entradas válidas que el respaldo local, actualiza el respaldo automáticamente. |
| `ContarEntradasProdkeys(ruta)` | Cuenta entradas válidas `clave = valor` en `prod.keys` (más entradas = versión más reciente y mayor prioridad). |
| `CompararProdkeys(rutaSD, rutaLocal)` | Devuelve `ComparacionProdkeys` indicando cuál tiene prioridad. |
| `GenerarCertificadoTxt(serial, archivosRespaldados, rutaDestino, estadoVerificacion)` | Escribe `certificado.txt` en la carpeta del respaldo con serial, verificación, archivos y versión de la app. |
| `GenerarCertificadoPng(analisis)` | Genera `certificado.png` superponiendo datos sobre la plantilla embebida. Usa coordenadas de `CertificadoLayout`. Etiqueta cada bis_key como `bis_key_0X = <hex>`. Normaliza `\n` literal del Gist a saltos de línea reales para el bloque NOTAS. |

**Modelos** (en `Core/RespaldoLlavesLogic.cs`):
- `AnalisisRespaldoLlaves` — `Serial`, `HayBiskeys`, `HayProdinfo`, `HayProdkeys`, rutas, `EstadoVerificacion`, `EsSeguroRespaldar`, `TieneArchivos`, **`InfoMasterKey?`**
- `InfoMasterKey` — `MasterKeyMaxima`, `RangoHosCompatible`, `AtmosphereDesde`, `IntegridadGeneraciones` (bool), `TotalMasterKeys` — poblado en `Analizar()` cuando hay `prod.keys`
- `ResultadoRespaldoLlaves` — `Exito`, `ArchivosCopiados`, `Errores`, `RutaDestino`
- `ResultadoRestauracionLlaves` — `Exito`, `ArchivosRestaurados`, `Errores`
- `ComparacionProdkeys` — `SDTienePrioridad`, `EntradasSD`, `EntradasLocal`
- `RespaldoLocal` — `Serial`, `Fecha`, `RutaCarpeta`, `HayBiskeys`, `HayProdinfo`, `HayProdkeys`, `HayCertificado`, `FechaFormateada`, `ResumenArchivos`
- `EstadoVerificacionLlaves` — `NoRealizada / Verificado / Discrepancia / SinProdkeys / SinBiskeys / ClaveNoEncontrada / ArchivoInvalido / ErrorLectura`

**Seguridad y política de prioridad:**
- Compara primeros 16 bytes de `BISKEYS.bin` con `bis_key_00` de `prod.keys` antes de copiar
- Si hay discrepancia (`Discrepancia`) el overlay muestra advertencia roja explícita
- **Prioridad de `prod.keys`**: se determina por la **cantidad de entradas válidas** (más entradas = firmware más reciente = mayor valor); no por fecha ni tamaño
- Si la SD tiene más entradas que el respaldo local, el respaldo se actualiza automáticamente antes de cualquier operación destructiva
- Si ya existe un respaldo con el mismo nombre y mismo tamaño, se omite (sin sobrescribir)
- Si el tamaño difiere, el archivo anterior se renombra con timestamp `.bak_YYYYMMDD_HHmmss`
- Registra en log inicio, éxito, fallo y resultado de verificación
- **Integración con operaciones destructivas**: `MainWindow.LimpiezaSD.cs`, `MainWindow.Formato.cs`, `MainWindow.Particionado.cs` y `MainWindow.AsistidoCompleto.cs` llaman a `Analizar` + `RespaldarAsync` antes de borrar y a `RestaurarAsync` después de completar, para garantizar que las llaves nunca se pierdan en procesos de limpieza o reparticionado

### `Core/ModeloSwitchTable.cs` — `public static class ModeloSwitchTable`
Tabla de modelos de Nintendo Switch mapeados por prefijo de serial.
| Miembro | Descripción |
|---|---|
| `record Entry(Prefijo, Modelo, Region)` | Una fila de la tabla. |
| `AplicarRemota(pipeString)` | Fusiona entradas del Gist sobre la base embebida en `_fusionada`. Simultáneamente puebla `_soloRemota` solo con las entradas del Gist. Formato: `XJW, Nintendo Switch Lite, América \| XAW, Nintendo Switch V1, América \| ...` |
| `Resolver(serial)` | Busca en la tabla fusionada (base + Gist). Usado en `RespaldoLlavesLogic` donde el fallback local es correcto. |
| `ResolverSoloRemota(serial)` | Busca **solo** en `_soloRemota`. Devuelve `null` si el Gist no ha llegado o no define ese prefijo. **Usado exclusivamente para el display del panel derecho.** |

**Misma estrategia que `MasterKeyTable`:** base embebida = fallback para certificados; solo-remota = fuente de verdad para el panel derecho.

---

### `Core/MasterKeyTable.cs` — `internal static class MasterKeyTable`
Tabla híbrida de relación entre `master_key_XX`, rango de Horizon OS compatible y primera versión de Atmosphere que la soportó.
| Miembro | Descripción |
|---|---|
| `record Entry(MasterKey, RangoHosCompatible, AtmosphereDesde)` | Una fila de la tabla. `AtmosphereDesde` = versión **mínima** de Atmosphere requerida. |
| `AplicarRemota(tablaRaw)` | Fusiona entradas remotas del Gist sobre la base embebida en `_fusionada`. Simultáneamente puebla `_soloRemota` solo con las entradas del Gist. Llamar tras sincronizar el Gist. |
| `Buscar(masterKey)` | Devuelve `Entry?` de la tabla fusionada (base + Gist). Usado en `RespaldoLlavesLogic` donde el fallback local es correcto. |
| `BuscarSoloRemota(masterKey)` | Devuelve `Entry?` **solo** de las entradas que vinieron del Gist. Devuelve `null` si el Gist no ha llegado o no define esa clave. **Usado exclusivamente para el display del panel derecho.** |
| `Total` | Número total de entradas en la tabla fusionada actualmente. |

**Estrategia híbrida:**
- **Base embebida** (22 entradas, master_key_00 a master_key_15): fallback para `RespaldoLlavesLogic` y certificados — funciona sin red.
- **Solo-remota** (`_soloRemota`): únicamente lo que el Gist define. El panel derecho **siempre** usa esta fuente — si el Gist no llegó, muestra `--` en lugar de un dato potencialmente desactualizado.
- **Remota vía Gist** (`ConfiguracionUI.TablaMasterKeys`, campo JSON `tabla_master_keys`): formato `master_key_15: 22.0.0-22.5.0, 1.11.0 | master_key_16: 23.x, 1.12.0`

---

### `Core/Rp2040Logic.cs` — `class Rp2040Logic`
| Miembro | Descripción |
|---|---|
| `EsRp2040(letraConDosP)` | `bool` — comprueba etiqueta `RPI-RP2` o presencia de `INFO_UF2.TXT` |
| `DetectarLetraRp2040()` | Busca entre unidades removibles y devuelve la letra del RP2040 o `null` |
| `LeerVersionFirmware(letraConDosP)` | Extrae versión del bootloader desde `INFO_UF2.TXT` |
| `FlashearAsync(letraConDosP, urlFirmware, progreso?, ct)` | Descarga el `.uf2` y lo copia a la unidad para flashear |
| `GuardarEnPcAsync(urlFirmware, rutaDestino, progreso?, ct)` | Descarga el `.uf2` a la ruta local elegida por el usuario |

---

### `Core/DetectorVersionesLogic.cs` — `class DetectorVersionesLogic`
Detecta versión instalada de un módulo en la SD.

---

### `Core/SDMonitorLogic.cs`
| Clase | Descripción |
|---|---|
| `ManifiestoLocal` | Modelo del `.nx-metadata.json` (`Version`) |
| `SDMonitorLogic.DetectarModulo(rutaRaiz, nombreCarpetaModulo)` | Detecta presencia del módulo |

---

### `Core/ImageConverter.cs` — `static class ImageConverter`
| Método | Descripción |
|---|---|
| `ConvertirParaHekate(origen, destino, ancho, alto, bits)` | Convierte imagen para Hekate (BMP) |
| `ConvertToHekateIcon(inputPath, outputPath, size)` | Convierte a icono de Hekate (async) |

---

### `Core/Resultado.cs`
```csharp
readonly record struct Resultado(bool Exito, string MensajeError)
  .Ok() / .Error(msg) / implicit bool

readonly record struct Resultado<T>(bool Exito, T? Valor, string MensajeError)
  .Ok(valor) / .Error(msg) / implicit bool
```

---

### `Core/ControladorCarga.cs` — `class ControladorCarga`
Controla la pantalla de carga (splash overlay durante operaciones largas).
| Miembro | Descripción |
|---|---|
| `AntesDeMostrar`, `DespuesDeOcultar` | Callbacks de ciclo de vida |
| `Mostrar(tituloPrincipal)` | Muestra overlay |
| `Ocultar()` | Oculta overlay |
| `ObtenerReportador()` | `IProgress<EstadoProgreso>` para reportar progreso |

---

## ?? Core/Pipeline — Sistema de pasos

### `Core/IPasoPipeline.cs` — `interface IPasoPipeline`
```csharp
string TipoAccion { get; }
Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct);
```

### `Core/ContextoPipeline.cs` — `class ContextoPipeline`
Contexto compartido entre todos los pasos de un pipeline (letra SD, progreso, logger, etc.)
Incluye `VersionModulo` (string) para que `PasoDescargar` invalide la caché si la versión cambió.
Incluye `ValidadorAsset` (`GitHubAssetValidator?`) inyectado por `ReglasLogic`; puede ser `null`; nunca bloquea la instalación.

### `Core/Pipeline/Pasos/` — Implementaciones de `IPasoPipeline`
| Clase | `TipoAccion` | Descripción |
|---|---|---|
| `PasoDescargar` | `"DESCARGAR"` | Descarga archivo remoto. Valida sidecar `<archivo>.version`. **Antes de descargar verifica si la carpeta extraída ya existe y es válida**; si existe y la versión coincide, omite la descarga. **Validación híbrida de hash:** si `ctx.ValidadorAsset != null` y la URL es de GitHub, consulta el digest SHA256 remoto; si difiere invalida la caché y redescarga; si no hay red/token/digest continúa desde caché (no forzoso). URLs no-GitHub y módulos sin paso DESCARGAR no activan esta lógica. Registra inicio, éxito (con tamaño), omisión por caché válida y fallo. |
| `PasoExtraer` | `"EXTRAER"` | Extrae ZIP/RAR/7z. Invalida la carpeta extraída si su sidecar `<CarpetaDestinoTemp>.version` no coincide con `VersionModulo`. Escribe el sidecar tras extracción exitosa. Registra inicio, éxito (con núm. archivos), omisión por ya extraído y fallo. |
| `PasoCopiarSD` | `"COPIARSD"` | Copia archivos a la SD. Registra inicio y éxito (con núm. archivos). |
| `PasoMoverArchivo` | `"MOVERARCHIVO"` | Mueve archivo en la SD |
| `PasoBorrarArchivos` | `"BORRARARCHIVOS"` | Borra archivos específicos |
| `PasoBorrarCarpetas` | `"BORRARCARPETAS"` | Borra carpetas específicas |
| `PasoBorrarCarpetasVacias` | `"BORRARCARPETASVACIAS"` | Borra carpetas vacías |
| `PasoCrearCarpeta` | `"CREARCARPETA"` | Crea carpeta en la SD |
| `PasoCrearIni` | `"CREARINI"` | Crea archivo `.ini` |
| `PasoEditarIni` | `"EDITARINI"` | Edita sección/clave de un `.ini` |
| `PasoCrearTxt` | `"CREARTXT"` | Crea archivo `.txt` |
| `PasoEjecutarCmd` | `"EJECUTARCMD"` | Ejecuta comando del sistema |
| `PasoFormatearSd` | `"FORMATEARSD"` | Formatea/particiona la SD. Registra modo (solo FAT32 / simple / emuMMC), éxito y fallo con excepción. |
| `PasoHekateSetValue` | `"HEKATE_SET_VALUE"` | Edita valor en `.ini` de Hekate. Registra valor escrito y archivo no encontrado. |
| `PasoHekateSetIcon` | `"HEKATE_SET_ICON"` | Aplica icono en Hekate. Registra secciones modificadas, sin cambios y archivo no encontrado. |
| `PasoLimpiarCache` | `"LIMPIAR_CACHE"` | Limpia caché local del módulo. Borra el ZIP (`ArchivoZip`) y/o carpeta extraída (`CarpetaTemp`) junto a sus sidecars `<archivo>.version` correspondientes. |
| `PasoRespaldarAPc` | `"RESPALDARAPC"` | Respalda carpeta de SD a PC |
| `PasoRestaurarDePc` | `"RESTAURARDEPC"` | Restaura desde PC a SD |

> Para agregar un nuevo paso: crear clase en `Core/Pipeline/Pasos/`, implementar `IPasoPipeline`,
> registrarlo en `RegistroPasos` y **actualizar esta tabla**.

---

## ??? Hardware

| Archivo | Clase | Descripción |
|---|---|---|
| `EscanerDiscos.cs` | `EscanerDiscos` | `ObtenerUnidadesRemovibles()`, `ExpulsarUnidad(letraRaiz)` |
| `NotificadorDiscos.cs` | `NotificadorDiscos` | Eventos `UnidadConectada` / `UnidadDesconectada`, `IniciarEscucha(ventana)` |
| `ParticionadorDiscos.cs` | `ParticionadorDiscos` | `ParticionarYFormatearAsync(...)`, `FormatearSoloFAT32Async(...)` |
| `SDInfo.cs` | `SDInfo` | Modelo: `Letra`, `Etiqueta`, `CapacidadTotal`, `Formato`, `Serial`, `DiscoFisico`, `FullName` |
| `CazadorVentanas.cs` | `static CazadorVentanas` | `Ejecutar(driveLetter)` — abre ventana de formato de Windows |
| `Native/DiscoNativo.cs` | — | P/Invoke para disco físico |
| `Native/DiskNative.cs` | — | Wrappers nativos adicionales |

---

## ?? UI — Ventana principal (`MainWindow`)

`MainWindow` es una **clase parcial** dividida en archivos por dominio funcional:

| Archivo | Dominio | Miembros públicos/internos relevantes |
|---|---|---|
| `MainWindow.xaml.cs` | Inicialización | `MainWindow()` — constructor, suscripción a `_cerebro.GistActualizadoEnBackground`; `AplicarConfiguracionRemota(GistData)` — vuelca config UI, re-fusiona `MasterKeyTable`/`ModeloSwitchTable` y mapea `datos.Tools` a `ConfiguracionRemota.Tools` (sección raíz `"tools"` del Gist), llamado en carga inicial y en cada re-sincronización; `OnGistActualizadoEnBackground(GistData)` — llamado en hilo UI cuando el Gist cambia en background, repuebla modelo/región/llaves del panel derecho sin reiniciar |
| `MainWindow.SD.cs` | Unidades SD | `RefrescarVersionAtmos()`; `RefrescarPanelInfoSD()` — refresca todos los campos del panel derecho (capacidad, formato, serial, modelo/región, sección LLAVES) sin red ni reinserción de SD — llamado tras restaurar llaves; `OcultarSeccionLlaves()`; `MostrarSeccionLlaves(info)` — muestra la sección LLAVES del panel con datos de `InfoPanelDerecho`; re-aplica `AplicarConfiguracionRemota` y repuebla modelo/región/llaves tras cada re-sincronización del Gist; **Firmware emuMMC (async, no bloqueante):** `IniciarDeteccionFirmwareEmummcAsync(info)` — cancela cualquier detección previa (`CancelarDeteccionFirmwareEmummc()`), muestra "Detectando firmware de emuMMC..." y lanza `EjecutarDeteccionFirmwareEmummcAsync` sin bloquear el resto del panel (ya pintado por `ObtenerInfoPanel`, síncrono). Usa un id de operación incremental + comparación de `DiscoFisico` para descartar resultados obsoletos si la unidad cambió mientras se detectaba. `CancelarDeteccionFirmwareEmummc()` también se invoca desde `LimpiarInterfazSD()` y `MainWindow.Ventana.cs` (`BtnCerrar_Click`/`BtnClose_Click`) |
| `MainWindow.Navegacion.cs` | Navegación entre vistas | — |
| `MainWindow.Catalogo.cs` | Catálogo de módulos | — |
| `MainWindow.Detalle.cs` | Detalle de módulo | `EliminarCacheVersion(ruta, esZip)` — elimina caché ZIP o Extraído de una versión específica desde los chips de la vista de detalle. Registra en log éxito (`INFO`) y fallo (`ERROR`) con nombre del módulo y tipo. |
| `MainWindow.Asistido.cs` | Modo asistido | — |
| `MainWindow.AsistidoCompleto.cs` | Flujo asistido completo | — |
| `MainWindow.Actualizacion.cs` | Actualizaciones de la app | — |
| `MainWindow.Particionado.cs` | Particionado de SD | `AbrirOverlayParticionado()`, `CerrarOverlayParticionado()` (internal) |
| `MainWindow.Formato.cs` | Formateo rápido FAT32 | — |
| `MainWindow.Queue.cs` | Cola de operaciones | — |
| `MainWindow.Paneles.cs` | Paneles laterales | — |
| `MainWindow.DependenciasOverlay.cs` | Overlay de dependencias | — |
| `MainWindow.Diagnostico.cs` | Diagnóstico | — |
| `MainWindow.News.cs` | Noticias/inicio | — |
| `MainWindow.Ventana.cs` | Chrome de ventana (mover, minimizar, cerrar) | — |
| `MainWindow.Log.cs` | Visor de log | `BtnLog_Click`, `BtnCerrarLog_Click`, `BtnCopiarTextoLog_Click`, `BtnGuardarArchivoLog_Click`, `CargarSesionesLog()`, `CrearBloqueSession(sesion, expandido)`, `CrearFilaLinea(linea)`, `MostrarOverlayLog()`, `OcultarOverlayLog()` |
| `MainWindow.Rp2040.cs` | Overlay firmware RP2040/Picofly | `BtnRp2040_Click`, `BtnCerrarRp2040_Click`, `BtnFlashearRp2040_Click`, `BtnGuardarRp2040_Click`, `ComprobarRp2040Conectado()`, `AbrirOverlayRp2040()`, `CerrarOverlayRp2040()`, `RefrescarEstadoRp2040()` |
| `MainWindow.RespaldoLlaves.cs` | Overlay híbrido de respaldo/restauración de llaves de consola (panel SD ? flechas ? lista respaldos PC) | `AbrirOverlayRespaldoLlaves()`, `CerrarOverlayRespaldoLlaves()`, `ResetearEstadoOverlay()`, `RefrescarOverlayRespaldoLlaves(letraSD)`, `PoblarResultadoAnalisis(analisis)`, `ConfigurarBadgeVerificacion(estado)`, `RefrescarListaRespaldosPC()`, `SeleccionarRespaldo(respaldo)`, `ActualizarBordesTarjetas()`, `EncontrarBorderNombrado(itemsControl, index, nombre)` (usa `VisualTreeHelper`), `BtnConfirmarRespaldo_Click` (SD?PC), `BtnRestaurarSeleccionado_Click` (PC?SD), `EjecutarRestauracionAsync(forzar)` — realiza respaldo preventivo si la SD tiene llaves verificadas y no respaldadas, ejecuta restauración, gestiona confirmación explícita en discrepancia de serial, y tras éxito llama a `RefrescarPanelInfoSD()` + re-análisis de la SD + `RefrescarListaRespaldosPC()` para actualizar el overlay y el panel derecho sin expulsar/reinsertar la SD, `BtnVerCertificado_Click`, `BtnAbrirCarpetaRespaldo_Click`, `MostrarFeedbackSD(msg, color)`, `MostrarFeedbackPC(msg, color)`, `RespaldoLlaves_BackdropClick` |
| `MainWindow.Ajustes.cs` | Overlay de Ajustes: blur fondo, fade-in/out, tabs Sonido, Caché, Carpetas Protegidas y **GitHub Token** | `BtnAjustes_Click`, `BtnCerrarAjustes_Click`, `SwitchAjuste_Click`, `CargarEstadoAjustes`, `RefrescarPanelCache`, `BtnEliminarCacheModulo_Click`, `BtnLimpiarTodoCache_Click`, `BtnAnadirEntradaProtegida_Click`, `BtnQuitarEntradaProtegida_Click`, `CheckEntradaSD_Click`, `TxtNuevaEntrada_KeyDown`, `AbrirAjustesEnTabCarpetasAsync`, `RefrescarPanelCarpetasProtegidasAsync`, `RefrescarPanelGitHub`, `BtnGuardarToken_Click`, `BtnBorrarToken_Click` |

---

## ?? UI — Controles reutilizables (`UI/Controles/`)

| Clase | Archivo | Descripción |
|---|---|---|
| `PanelDerecho` | `PanelDerecho.xaml(.cs)` | Panel derecho de información de SD. Evento `ExpulsarSolicitado`. Campos: `TxtTotalSize`, `TxtFileSystem`, `TxtAtmosVer`, `TxtFirmwareEmummc` (firmware interno de la emuMMC RAW, debajo de VERSION ATMOSPHERE — `"Sin prod.keys"` si no hay llaves, `"Detectando firmware de emuMMC..."` mientras se resuelve async), `TxtSDSerial`, `TxtSDModelo`/`LblSDModelo`, `TxtSDRegion`/`LblSDRegion` (colapsados hasta que el Gist los define), separador `SepLlaves`, `LblSeccionLlaves`, `TxtMasterKey`, `TxtFirmware`, `TxtAtmosMinima` (sección LLAVES, colapsada si no hay `prod.keys` o el Gist no la define). Todos los datos del panel vienen **exclusivamente del Gist** (`ResolverSoloRemota`/`BuscarSoloRemota`) salvo `TxtMasterKey` que se lee de `prod.keys` localmente y `TxtFirmwareEmummc` que se lee de la emuMMC vía NxNandManager. |
| `PanelIzquierdo` | `PanelIzquierdo.xaml(.cs)` | Panel izquierdo; evento `LogoInicioSolicitado`; `AplicarBrandingAsync(branding)` |
| `RetractilDer` | `RetractilDer.xaml(.cs)` | Panel retráctil derecho; eventos `FormatFAT32Solicitado`, `ParticionadoSolicitado`, `LimpiezaMicroSDSolicitada`, `RespaldoLlavesSolicitado`. El botón «LIMPIAR SD» es un `Button` normal (click directo, sin hold-to-confirm) |
| `RetractilIzq` | `RetractilIzq.xaml(.cs)` | Panel retráctil izquierdo; evento `CerrarSolicitado` |
| `SafeButton` | `SafeButton.cs` | Botón con confirmación por pulsación larga. DPs: `IsSafeMode`, `HoldTimeSeconds`, `Progress`, `ProgressScale` |
| `GifIcon` | `GifIcon.cs` | Control `Image` con soporte GIF. DPs: `Url`, `AnimateOnHover`, `AnimateOnClick` |
| `UiAnimaciones` | `UiAnimaciones.cs` | `static` — animaciones de paneles, catálogo, tarjetas y mundos. **`AnimarEntradaTarjeta`**: slide de entrada cambiado de `ThicknessAnimation` sobre `MarginProperty` a `TranslateTransform.Y` + `DoubleAnimation` (GPU pura, sin relayout por frame). |
| `VistaAsistida` | `VistaAsistida.xaml(.cs)` | Vista del asistente. Eventos: `InstalacionSolicitada`, `DetalleModuloSolicitado`, `ProcesarCompletoSolicitado`. Método: `Cargar(nodos, modulos, modoAsistente)`. **Banner de versión compatible:** `OverlaySelectorModo` incluye un `Border` neon (color `#00FFCC`, `DropShadowEffect` estático) encima del título «¿Cómo quieres configurar tu SD?», vinculado a `ConfiguracionRemota.Ui.VersionCompatible`. |
| `VistaPersonalizacion` | `VistaPersonalizacion.xaml(.cs)` | Vista de temas/personalización. Evento: `TemaAplicado`. Método: `CargarTemas(temas)` |

---

## ?? UI — ViewModels de `VistaAsistida` (`UI/Controles/VistaAsistida/`)

| Clase | Descripción |
|---|---|
| `SesionAsistida` | Datos de la sesión: `Modulos`, `IdsDependencias` |
| `ProcesarCompletoArgs : EventArgs` | Args del evento de proceso completo: `GbEmuMMC`, `LetraSD`, `Etiqueta`, `NumeroDisco`, `Modulos`, `IdsDependencias`, `Logger` |
| `SubcategoriaVM` | VM de subcategoría con `Seleccionados`, `SlotsVisibles`, `PermiteMultiseleccion` |
| `ItemCheckoutVM` | VM de ítem en resumen de checkout: `Modulo`, `PasoTitulo`, `EsComplemento` |
| `ItemMultiSeleccionVM` | VM de ítem con checkbox de multiselección |
| `ImagenSlotVM` | VM de slot de imagen con `BitmapSource` |
| `RecomendadoVM` | VM de módulo recomendado con `VersionAInstalar`, `Nota` |
| `HekateSeccionVM` | VM de sección de Hekate |
| `HekateAgregarPlaceholder` | Placeholder para agregar entrada en Hekate |
| `SlotVacioPlaceholder` | Placeholder de slot vacío |
| `ComplementoCardTemplateSelector` | `DataTemplateSelector` para complementos |
| `HekateSeccionCardTemplateSelector` | `DataTemplateSelector` para secciones Hekate |
| `SlotTemplateSelector` | `DataTemplateSelector` para slots (módulo vs vacío) |

---

## ?? UI — Converters (`UI/Converters/`)

| Clase | Descripción |
|---|---|
| `ContainsStringConverter` | Comprueba si una colección contiene una cadena |
| `ConversorIconoCache` | Convierte URL de icono a ruta local cacheada |
| `HexToBrushConverter` | Convierte string hex (`#RRGGBB`) a `SolidColorBrush` (raíz del proyecto) |

---

## ?? UI — Estilos XAML (`UI/Estilos/`)

| Archivo | Descripción |
|---|---|
| `ColoresGlobales.xaml` | Variables de color globales |
| `EstilosBotones.xaml` | Estilos de botones |
| `EstilosOverlay.xaml` | Estilos de overlays/modales |
| `EstilosScrollBars.xaml` | Estilos de scrollbars |
| `EstilosTarjetas.xaml` | Estilos de tarjetas de módulos. **Optimización de rendimiento (rama `feat(Optimizacion-efectos)`):** `BlurEffect` eliminado de `GlowFondo`, `GlowCache` y `GlowAsist` ? reemplazados por `RadialGradientBrush` sin efecto. `BordNeonGiro` y `HaloAsist` cambiados de `Opacity="0"` a `Visibility="Collapsed"` para que WPF no compute el `BlurEffect(20)` cuando no son visibles. Eliminado `EventTrigger Loaded` con `Storyboard Forever` en `RectPulso` (ahora solo lo activa `DataTrigger EstaInstalando=True`). Todos los `DataTrigger` que mostraban el halo usan `Visibility="Visible"` en lugar de `Opacity="1"`. |
| `EstilosAjustes.xaml` | `EstiloToggleSwitch`, `EstiloFilaAjuste`, `EstiloCabeceraSeccion`, `EstiloTabAjuste` — usados en `PanelAjustesOverlay` |

---

## ?? UI — Ventanas secundarias (`UI/`)

| Clase | Archivo | Descripción |
|---|---|---|
| `VentanaDependencias` | `VentanaDependencias.xaml(.cs)` | Muestra dependencias pendientes |
| `VentanaPersonalizacion` | `VentanaPersonalizacion.xaml(.cs)` | Ventana principal de personalización/temas |
| `VentanaSplash` | `VentanaSplash.xaml(.cs)` | Pantalla de carga/splash inicial; carga y aplica `PreferenciasUsuario` antes de cualquier sonido |

> **Nota:** `VentanaAjustes` fue eliminada. Los ajustes ahora se muestran como overlay inline (`PanelAjustesOverlay`, ZIndex 910) dentro de `MainWindow.xaml`, gestionado por `MainWindow.Ajustes.cs`.

---

## ?? Configuración estática

| Clase | Archivo | Descripción |
|---|---|---|
| `ConfiguracionLocal` | `Core/Configuracion/ConfiguracionLocal.cs` | Constantes: `UrlGistPrincipal`, `UrlGistBeta`, `NombreManifiesto`, `CarpetaTemporal`, `EtiquetaSwitchSd`, `TtlCacheGistHoras`, `NombreCacheGist`, `NombreFat32FormatExe`, `RutaPreferencias` (`%AppData%\NX-Suite\preferencias.json`), `RutaLog` (`%AppData%\NX-Suite\NX-Suite.log`), `RutaTokenGitHub` (`%AppData%\NX-Suite\github_token.dat`), `RutaRespaldosLlaves` (`Mis Documentos\NX-Swite\Respaldos\`). `VersionActual` — lee la versión del ensamblado en ejecución vía `Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)`; la fuente real es `<Version>` en `NX-Suite.csproj` (actualmente `0.6.7`). **Herramientas descargables:** `RutaHerramientas` (`%AppData%\NX-Swite\Tools\`), `RutaNxNandManager` (`...\Tools\NxNandManager\`), `RutaVersionNxNandManager(version)`, `RutaEjecutableNxNandManager(version, nombreEjecutable)`, `RutaZipNxNandManager(version, nombreArchivoZip)` (ruta del ZIP temporal antes de extraer) — únicas construcciones válidas de estas rutas, no repetir manualmente en otras clases. `TimeoutCliNxNandManager` (`TimeSpan`, 45s) — timeout centralizado de ejecución del CLI. |
| `ConfiguracionRemota` | `Core/Configuracion/ConfiguracionRemota.cs` | Props estáticas: `Ui` (incluye `IconoConfigUrl`, `IconoCarpetaUrl`, `IconoArchivoUrl`, `IconoZipUrl`, `IconoShieldUrl`, `IconoLogUrl`, `VersionCompatible`, `IconoRp2040Url`, `UrlFirmwareRp2040`, `VersionFirmwareRp2040`, `NotaCertificado`, `TablaMasterKeys`), `NyxColors`, `Recomendados`, **`Tools`** (`ToolsConfig`) — configuración de herramientas externas administradas, mapeada desde la sección raíz **`"tools"`** del Gist (NO desde `ConfiguracionUI`). |
| `ConfiguracionSonidos` | `Core/Configuracion/ConfiguracionSonidos.cs` | Props estáticas: `SonidosActivos`, `Intro`, `Cerrar`, `Click`, `Hover`, `Instalar`, `Exito`, `Error`, `Navegacion`, `Volumen` |
| `PreferenciasUsuario` | `Core/Configuracion/PreferenciasUsuario.cs` | Modelo serializable en disco: `SchemaVersion`, `Sonido` (`SeccionSonido`) |
| `GestorPreferencias` | `Core/Configuracion/GestorPreferencias.cs` | `CargarAsync()`, `GuardarAsync(prefs)`, `static AplicarSonido(SeccionSonido)` ? vuelca a `ConfiguracionSonidos` |
| `Servicios.Preferencias` | `Core/Servicios.cs` | Singleton lazy de `GestorPreferencias`; compartido por `VentanaSplash` y `MainWindow.Ajustes.cs` |

---

## ?? Modelos de datos (`Core/Models/`)

| Clase | Descripción |
|---|---|
| `ModuloConfig` | Módulo del catálogo (nombre, versiones, iconos, etiquetas, pipeline…) |
| `GistData` | Datos del Gist remoto (lista de módulos, mundos, temas, noticias, etc.): `Modulos`, `FiltrosCentroMando`, `DiagramaNodos`, `Temas`, `News`, `AppVersion`, **`Tools`** (`ToolsConfig`, mapeado desde la sección raíz `"tools"` del JSON) |
| `ToolsConfig` | (`Models/Configuracion/ToolsConfig.cs`) Sección raíz `"tools"` del Gist: `CliNxNandManagerUrl` (`CLI_NX_NAND_MANAGER`, URL del ZIP), `CliNxNandManagerSha256` (`CLI_NX_NAND_MANAGER_SHA256`), `CliNxNandManagerFilename` (`CLI_NX_NAND_MANAGER_FILENAME`), `CliNxNandManagerExecutable` (`CLI_NX_NAND_MANAGER_EXECUTABLE`, nombre del `.exe` dentro del ZIP), `CliNxNandManagerVersion` (`CLI_NX_NAND_MANAGER_VERSION`). Leído exclusivamente vía `ConfiguracionRemota.Tools`. |
| `MundoMenuConfig` | Mundo/sección del menú: `Id`, `Nombre`, `Subtitulo`, `IconoUrl`, `ColorNeon`, `Tipo`, `ModoAsistente`, `EtiquetasFiltro` |
| `NodoDiagramaConfig` | Nodo del diagrama asistido: `Id`, `Tipo`, `Nombre`, `Descripcion`, `IconoUrl`, `ColorNeon`, `EsObligatorio`, `EtiquetasFiltro` |
| `TemaConfig` | Tema de personalización: `Id`, `Nombre`, `Autor`, `Version`, `EsOficial`, `Aplicado`, `HekateImagenUrl`, `HekateIniUrl`, `NyxColorAcento` |
| `NewsItem` | Item de noticias: `Title`, `Description`, `ImageUrl`, `Link`, `BackgroundColor` |
| `SonidosConfig` | Rutas de sonidos: `Intro`, `Cerrar`, `Click`, `Hover`, `Instalar`, `Exito`, `Error`, `Navegacion` |
| `NyxConfigColors` | Colores NYX: `Themecolors`, `Themebgs` |
| `NyxColorPreset` | Preset de color NYX: `Nombre`, `Valor`, `HexRgb` |
| `NyxFondoPreset` | Preset de fondo NYX: `Nombre`, `IniValue`, `HexRgb` |
| `HallazgoDependencia` | Resultado de análisis de dependencias: `Modulo`, `DependenciasPendientes` |
| `HallazgoIncompatibilidad` | Conflicto detectado: `Modulo`, `ModuloConflicto`, `TipoConflicto`, `Mensaje` |
| `FiltroMandoConfig` | Filtro del centro de mando |
| `ReglasConfig` | Reglas de validación de configuración |
| `HallazgoConfig` | Resultado de validación de configuración |
| `ModuloRecomendado` | Módulo recomendado por la configuración remota |
| `BrandingConfig` | Configuración de branding (logo, nombre de programa) |
| `InfoPanelDerecho` | Datos para el panel derecho de información de SD. Props: `Capacidad`, `Formato`, `VersionAtmos`, `Serial`, `HayProdkeys` (bool), `MasterKeyMaxima` (nombre de la clave, p.ej. `master_key_15`), `FirmwareCompatible` (rango HOS del Gist o `--`), `AtmosphereDesde` (versión mínima Atmosphere del Gist o `--`), `DiscoFisico` (índice de disco físico, para `\\.\PhysicalDriveN`), `RutaProdKeys` (ruta absoluta a `switch/prod.keys` o vacío) |
| `ResultadoFirmwareEmummc` | (`Models/NxNandManager/`) Resultado de `NxNandManagerLogic`: `Estado` (`EstadoFirmwareEmummc`), `Version` (solo si `Detected`), `MensajeError`, `SalidaCruda` (diagnóstico) |
| `EstadoFirmwareEmummc` | (`Models/NxNandManager/`) enum: `NotStarted`, `Detecting`, `Detected`, `FirmwareNotDetected`, `EmuMmcNotFound`, `KeysMissing`, `KeysInvalid`, `ToolDownloading`, `ToolValidationFailed`, `AccessDenied`, `TimedOut`, `Failed` |
| `ItemCacheModuloVM` | (`Models/Cache/`) ViewModel para la lista del tab Caché en Ajustes: `Nombre`, `Detalle`, `Modulo` |
| `EntradaSDVM` | (`Models/Cache/`) ViewModel para el explorador SD en Ajustes ? Carpetas Protegidas. Implementa `INotifyPropertyChanged`. Props: `Nombre`, `Tipo` (`EsTipoEntrada`), `EstaProtegido` (notifica cambios), `EsCritico` (deriva de `EntradaSD.NombresCriticos`), `IconoUrl` (resuelve `IconoCarpetaUrl` / `IconoZipUrl` / `IconoArchivoUrl` según tipo) |

---

## ?? Paquetes NuGet

| Paquete | Versión | Uso |
|---|---|---|
| `SharpCompress` | 0.50.0 | Extracción de ZIP/RAR/7z en `ZipLogic` |
| `SixLabors.ImageSharp` | 3.1.12 | Conversión de imágenes en `ImageConverter` |
| `System.Management` | 10.0.5 | WMI para detección de discos en `Hardware/` |

---

## ?? Zonas a NO buscar

- `bin/` — archivos compilados
- `obj/` — artefactos de build
- `NewFolder1/`, `NewFolder2/` — excluidos del proyecto explícitamente
- `NX-Suite.Updater/` — proyecto separado, solo maneja auto-actualización

---

## ?? Comportamiento UX — Overlay Log (PanelLogOverlay, ZIndex 912)

- **Apertura:** botón `BtnLog` en la TopBar (junto a Ajustes y Cola). Icono `IconoLogUrl` del Gist (cacheado igual que el resto). `BtnNotificaciones` y `BtnMensajes` ocultados con `Visibility="Collapsed"` — sus URLs del Gist se conservan para uso futuro.
- **Cierre:** click fuera (backdrop `#CC000008`) o botón `?` en la cabecera.
- **Animación:** `Opacity 0?1` + `ScaleTransform 0.96?1.0` en 200 ms (igual que el resto de overlays).
- **Sesiones:** todas las sesiones del log, más reciente primero. Cada sesión es un bloque colapsable con cabecera (fecha + badge de errores) y cuerpo de líneas. La sesión más reciente se abre expandida, las demás colapsadas.
- **Colores por nivel:**
  - `OK` ? verde `#4CAF50`
  - `INFO` ? gris `#A0A0B0`
  - `WARN` ? ámbar `#FFD54A`
  - `ERROR` ? rojo `#FF5555`
- **Cabecera de sesión:** cian si sin errores, ámbar si tiene errores.
- **Footer:** tres botones — `LIMPIAR LOG` (borra el archivo y refresca la vista), `COPIAR TEXTO` (portapapeles) y `GUARDAR ARCHIVO` (`SaveFileDialog` con directorio inicial en `~/Downloads`, extensión `.log`).

## ?? Comportamiento UX — Overlay Limpiar Micro SD

- **Iconos por tipo:** carpetas ? `IconoCarpetaUrl`, comprimidos ? `IconoZipUrl`, archivos ? `IconoArchivoUrl` (todos del Gist)
- **Ordenamiento:** carpetas primero, luego archivos/comprimidos, dentro de cada grupo alfabéticamente
- **Carpetas críticas (`emuMMC`, `Nintendo`) en lista SE BORRARÁ:** borde y nombre parpadean en rojo neon (`ColorAnimation` en `DataTrigger EsCritico=True`). Icono con opacidad 100%.
- **Advertencia dinámica:** `TxtAdvertenciaCriticos` aparece debajo del aviso fijo listando los nombres de carpetas críticas en riesgo
- **Botón ?** eliminado del header — el overlay cierra con click fuera (backdrop)
- **Row layout:** 4 filas (`48 / * / Auto / Auto`) — header / listas / aviso / footer; evita solapamiento

## ?? Comportamiento UX — Overlay Ajustes ? Carpetas Protegidas

- **Explorador SD** (`ListaExploradorSD`): muestra contenido físico de la SD con toggle `CheckBox`. Iconos por tipo (misma lógica que LimpiezaSD). Carpetas críticas **NO protegidas** ? parpadeo neon rojo (`MultiDataTrigger EsCritico=True AND EstaProtegido=False`). Al protegerlas el parpadeo cesa.
- **Entradas huérfanas** (`ListaEntradasProtegidas`): entradas en `PreferenciasUsuario` que NO existen físicamente en la SD. Se muestran con icono ? ámbar y botón ?. Si no hay SD, muestra **todas** las entradas guardadas.
- **`TxtCabeceraEntradasGuardadas`**: texto dinámico — `"ENTRADAS SIN COINCIDENCIA EN LA SD"` (con SD) o `"ENTRADAS PROTEGIDAS GUARDADAS"` (sin SD)
- **Ordenamiento explorador:** carpetas primero ? comprimidos/archivos ? alfabético

---

## ?? Comportamiento UX — Overlay Respaldo de Llaves (PanelRespaldoLlavesOverlay, ZIndex 913)

- **Apertura:** botón «RESPALDAR LLAVES» en el panel retráctil Arsenal (derecha).
- **Cierre:** click fuera (backdrop `#CC000008`) o botón `?` en cabecera.
- **Animación:** igual que el resto de overlays — `Opacity 0?1` + `ScaleTransform 0.96?1.0` en 200 ms.
- **Flujo:**
  1. Al abrir: análisis no destructivo de la SD ? muestra serial, archivos encontrados y resultado de verificación criptográfica.
  2. **Verificado** (bis_key_00 coincide) ? badge verde, botón RESPALDAR directo.
  3. **Discrepancia** (bis_key_00 no coincide) ? advertencia roja explícita, botón RESPALDAR habilitado con texto de aviso (el usuario decide conscientemente).
  4. **Sin prod.keys** ? badge ámbar, solo se respaldan los archivos de `atmosphere/automatic_backups`.
  5. Tras éxito de respaldo: feedback verde + botón «ABRIR CARPETA» que lanza Explorer en el destino.
- **Restauración (PC ? SD):**
  - SD vacía: permitida (el usuario confía en NX-Swite como única fuente).
  - **Respaldo preventivo:** si la SD tiene llaves verificadas y el respaldo local no está al día, se respalda automáticamente antes de sobreescribir.
  - **Serial distinto:** se muestra diálogo de confirmación explícito (no bloqueo duro); el usuario puede forzar la restauración conscientemente.
  - **`prod.keys` más completo** (más entradas válidas) siempre se preserva, independientemente del sentido de la restauración.
  - Tras éxito: el panel derecho SD se actualiza al instante (`RefrescarPanelInfoSD()`), el lado izquierdo del overlay re-analiza la SD y la lista de respaldos PC se sincroniza — **sin necesidad de expulsar/reinsertar la micro SD**.
- **Destino:** `Mis Documentos\NX-Swite\Respaldos\{SERIAL}\`
- **Seguridad sobre-escritura:** si el archivo ya existe con el mismo tamaño se omite; si el tamaño difiere se hace backup `.bak_YYYYMMDD_HHmmss` antes de sobrescribir.
- **Se cierra automáticamente** si la SD se desconecta (vía `CerrarOverlaysPorDesconexionSD`).

---

*Actualizado: 2025 — rama `feat(mod_panel_derecho)` — rediseño del panel derecho SD: nueva sección LLAVES con `TxtMasterKey` (detectado de `prod.keys`), `TxtFirmware` y `TxtAtmosMinima` (solo del Gist). Modelo y Región colapsados hasta que el Gist los define. `InfoPanelDerecho` extendido con `HayProdkeys`, `MasterKeyMaxima`, `FirmwareCompatible`, `AtmosphereDesde`. `SuiteController.DetectarMasterKeyMaxima()` detecta la clave del archivo sin consultar la tabla. `MasterKeyTable` añade `_soloRemota` + `BuscarSoloRemota()`. `ModeloSwitchTable` añade `_soloRemota` + `ResolverSoloRemota()`. Panel derecho usa **exclusivamente** `BuscarSoloRemota`/`ResolverSoloRemota` — si el Gist no define el dato, muestra `--` o colapsa el campo. `GistParser` expone evento `GistActualizadoEnBackground` disparado cuando la revalidación ETag detecta cambios; `SuiteController`/`SuiteControllerFacade`/`ISuiteController` lo propagan; `MainWindow` se suscribe y llama `OnGistActualizadoEnBackground` en hilo UI para refrescar el panel sin reiniciar. `AplicarConfiguracionRemota(GistData)` centraliza el volcado de `ConfiguracionUI` a `ConfiguracionRemota.Ui` y la re-fusión de ambas tablas.*

*Actualizado: 2025 — rama `feat(respaldo_llaves)` — overlay híbrido de respaldo/restauración de llaves. Flujo de restauración endurecido: restauración permitida en SD vacía (para usuarios de confianza); `prod.keys` más completo siempre preservado por conteo de entradas válidas; discrepancia de serial convierte el bloqueo en confirmación explícita del usuario; respaldo preventivo automático antes de sobreescribir si la SD tiene llaves verificadas y no respaldadas. `ResultadoRestauracionLlaves` añade `DiscrepanciaSerial` y `SerialEnSD`. Nuevo método `RefrescarPanelInfoSD()` en `MainWindow.SD.cs` centraliza el refresco completo del panel derecho. Tras restauración exitosa: `RefrescarPanelInfoSD()` actualiza el panel al instante, re-análisis de la SD repuebla el lado izquierdo del overlay, y `RefrescarListaRespaldosPC()` sincroniza la lista — todo sin expulsar/reinsertar la micro SD.*

*Actualizado: 2025 — rama `FIX-DescargavsCache`

*Actualizado: 2025 — rama `fix(sameversion-diferentziphash)` — detección de cambio silencioso de hash (mismo número de versión, archivo distinto): nuevo `GitHubAssetValidator` consulta el digest SHA256 del asset vía API de GitHub Releases; `PasoDescargar` invalida la caché solo si el hash difiere (`ResultadoValidacion.Desactualizado`); si no hay red/token/digest el proceso continúa desde caché sin interrumpir la instalación. Nuevo `TokenGitHub` (DPAPI) persiste el PAT cifrado en `github_token.dat`. `ContextoPipeline` incorpora `ValidadorAsset`. Nuevo tab “GitHub Token” en el overlay de Ajustes con `PasswordBox`, botón guardar/borrar e indicador de estado.*
