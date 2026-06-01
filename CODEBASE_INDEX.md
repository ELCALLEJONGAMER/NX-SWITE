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
??? MainWindow.*.cs         # Partial classes de MainWindow (una por dominio funcional)
???   MainWindow.Ajustes.cs      # Tabs: Sonido, Caché, Carpetas Protegidas
???   MainWindow.LimpiezaSD.cs   # Overlay "Limpiar Micro SD": proteger/desproteger inline, confirmar con hold-to-confirm
??? MainWindow.xaml
??? App.xaml / App.xaml.cs
??? HexToBrushConverter.cs
```

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
| `SincronizarTodoAsync(urlGist, letraSD[, ct])` | Descarga y sincroniza el catálogo remoto (Gist) |
| `ObtenerUnidadesRemoviblesAsync()` | Lista unidades SD detectadas |
| `ObtenerInfoPanel(unidad, modulos)` | Datos para el panel derecho |
| `InstalarModuloAsync(...)` | Ejecuta pipeline de instalación (sobrecarga con `CancellationToken`) |
| `DesinstalarModuloAsync(modulo, letraSD)` | Desinstala módulo de la SD |
| `LimpiarCacheModulo(modulo)` | Borra caché local del módulo |
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
Decorador/fachada de `SuiteController`. Misma interfaz, úsalo para pruebas o throttling.

---

### `Core/ISuiteController.cs` — `interface ISuiteController`
Contrato público del controlador. **Punto de referencia para extensiones**.

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

### `Core/LimpiezaSDLogic.cs` — `class LimpiezaSDLogic`
Lógica de limpieza de la Micro SD: escanea primer nivel, separa protegidos y ejecuta borrado.
| Miembro | Descripción |
|---|---|
| `Analizar(letraSD, protegidos)` | Devuelve `AnalisisLimpiezaSD` sin escribir nada. Ordena carpetas primero, luego archivos/comprimidos, dentro de cada grupo alfabéticamente |
| `EjecutarAsync(letraSD, protegidos, progreso, ct)` | Borra todo lo no protegido del primer nivel de la SD |

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
| `EjecutarPipelineAsync(pipeline, letraSD, progreso, ct, versionModulo)` | Ejecuta el pipeline de un módulo; `versionModulo` se propaga a `ContextoPipeline.VersionModulo` |

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

### `Core/Pipeline/Pasos/` — Implementaciones de `IPasoPipeline`
| Clase | `TipoAccion` | Descripción |
|---|---|---|
| `PasoDescargar` | `"DESCARGAR"` | Descarga archivo remoto. Valida sidecar `<archivo>.version`: si la versión en caché ? `VersionModulo`, borra el archivo obsoleto y redescarga. Escribe el sidecar tras descarga exitosa. |
| `PasoExtraer` | `"EXTRAER"` | Extrae ZIP/RAR/7z |
| `PasoCopiarSD` | `"COPIARSD"` | Copia archivos a la SD |
| `PasoMoverArchivo` | `"MOVERARCHIVO"` | Mueve archivo en la SD |
| `PasoBorrarArchivos` | `"BORRARARCHIVOS"` | Borra archivos específicos |
| `PasoBorrarCarpetas` | `"BORRARCARPETAS"` | Borra carpetas específicas |
| `PasoBorrarCarpetasVacias` | `"BORRARCARPETASVACIAS"` | Borra carpetas vacías |
| `PasoCrearCarpeta` | `"CREARCARPETA"` | Crea carpeta en la SD |
| `PasoCrearIni` | `"CREARINI"` | Crea archivo `.ini` |
| `PasoEditarIni` | `"EDITARINI"` | Edita sección/clave de un `.ini` |
| `PasoCrearTxt` | `"CREARTXT"` | Crea archivo `.txt` |
| `PasoEjecutarCmd` | `"EJECUTARCMD"` | Ejecuta comando del sistema |
| `PasoFormatearSd` | `"FORMATEARSD"` | Formatea la SD |
| `PasoHekateSetValue` | `"HEKATE_SET_VALUE"` | Edita valor en `hekate_ipl.ini` |
| `PasoHekateSetIcon` | `"HEKATE_SET_ICON"` | Aplica icono en Hekate |
| `PasoLimpiarCache` | `"LIMPIAR_CACHE"` | Limpia caché local del módulo |
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
| `MainWindow.xaml.cs` | Inicialización | `MainWindow()` — constructor, inyección de `ISuiteController` |
| `MainWindow.SD.cs` | Unidades SD | `RefrescarVersionAtmos()` |
| `MainWindow.Navegacion.cs` | Navegación entre vistas | — |
| `MainWindow.Catalogo.cs` | Catálogo de módulos | — |
| `MainWindow.Detalle.cs` | Detalle de módulo | — |
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
| `MainWindow.Ajustes.cs` | Overlay de Ajustes: blur fondo, fade-in/out, tabs Sonido, Caché y Carpetas Protegidas | `BtnAjustes_Click`, `BtnCerrarAjustes_Click`, `SwitchAjuste_Click`, `CargarEstadoAjustes`, `RefrescarPanelCache`, `BtnEliminarCacheModulo_Click`, `BtnLimpiarTodoCache_Click`, `BtnAnadirEntradaProtegida_Click`, `BtnQuitarEntradaProtegida_Click`, `CheckEntradaSD_Click`, `TxtNuevaEntrada_KeyDown`, `AbrirAjustesEnTabCarpetasAsync`, `RefrescarPanelCarpetasProtegidasAsync` — muestra entradas huérfanas (guardadas pero no en SD) con ? y ?, todas las entradas si sin SD; explorador SD con toggle para entradas físicas |

---

## ?? UI — Controles reutilizables (`UI/Controles/`)

| Clase | Archivo | Descripción |
|---|---|---|
| `PanelDerecho` | `PanelDerecho.xaml(.cs)` | Panel derecho; evento `ExpulsarSolicitado` |
| `PanelIzquierdo` | `PanelIzquierdo.xaml(.cs)` | Panel izquierdo; evento `LogoInicioSolicitado`; `AplicarBrandingAsync(branding)` |
| `RetractilDer` | `RetractilDer.xaml(.cs)` | Panel retráctil derecho; eventos `FormatFAT32Solicitado`, `ParticionadoSolicitado`, `LimpiezaMicroSDSolicitada`. El botón «LIMPIAR SD» es un `Button` normal (click directo, sin hold-to-confirm) |
| `RetractilIzq` | `RetractilIzq.xaml(.cs)` | Panel retráctil izquierdo; evento `CerrarSolicitado` |
| `SafeButton` | `SafeButton.cs` | Botón con confirmación por pulsación larga. DPs: `IsSafeMode`, `HoldTimeSeconds`, `Progress`, `ProgressScale` |
| `GifIcon` | `GifIcon.cs` | Control `Image` con soporte GIF. DPs: `Url`, `AnimateOnHover`, `AnimateOnClick` |
| `UiAnimaciones` | `UiAnimaciones.cs` | `static` — animaciones de paneles, catálogo, tarjetas y mundos |
| `VistaAsistida` | `VistaAsistida.xaml(.cs)` | Vista del asistente. Eventos: `InstalacionSolicitada`, `DetalleModuloSolicitado`, `ProcesarCompletoSolicitado`. Método: `Cargar(nodos, modulos, modoAsistente)` |
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
| `EstilosTarjetas.xaml` | Estilos de tarjetas de módulos |
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
| `ConfiguracionLocal` | `Core/Configuracion/ConfiguracionLocal.cs` | Constantes: `UrlGistPrincipal`, `UrlGistBeta`, `NombreManifiesto`, `CarpetaTemporal`, `EtiquetaSwitchSd`, `TtlCacheGistHoras`, `NombreCacheGist`, `NombreFat32FormatExe`, `RutaPreferencias` (`%AppData%\NX-Suite\preferencias.json`) |
| `ConfiguracionRemota` | `Core/Configuracion/ConfiguracionRemota.cs` | Props estáticas: `Ui` (incluye `IconoConfigUrl`, `IconoCarpetaUrl`, `IconoArchivoUrl`, `IconoZipUrl`, `IconoShieldUrl`), `NyxColors`, `Recomendados` |
| `ConfiguracionSonidos` | `Core/Configuracion/ConfiguracionSonidos.cs` | Props estáticas: `SonidosActivos`, `Intro`, `Cerrar`, `Click`, `Hover`, `Instalar`, `Exito`, `Error`, `Navegacion`, `Volumen` |
| `PreferenciasUsuario` | `Core/Configuracion/PreferenciasUsuario.cs` | Modelo serializable en disco: `SchemaVersion`, `Sonido` (`SeccionSonido`) |
| `GestorPreferencias` | `Core/Configuracion/GestorPreferencias.cs` | `CargarAsync()`, `GuardarAsync(prefs)`, `static AplicarSonido(SeccionSonido)` ? vuelca a `ConfiguracionSonidos` |
| `Servicios.Preferencias` | `Core/Servicios.cs` | Singleton lazy de `GestorPreferencias`; compartido por `VentanaSplash` y `MainWindow.Ajustes.cs` |

---

## ?? Modelos de datos (`Core/Models/`)

| Clase | Descripción |
|---|---|
| `ModuloConfig` | Módulo del catálogo (nombre, versiones, iconos, etiquetas, pipeline…) |
| `GistData` | Datos del Gist remoto (lista de módulos, mundos, temas, noticias, etc.): `Modulos`, `FiltrosCentroMando`, `DiagramaNodos`, `Temas`, `News`, `AppVersion` |
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
| `InfoPanelDerecho` | Datos para el panel derecho de información de SD |
| `ItemCacheModuloVM` | (`Models/Cache/`) ViewModel para la lista del tab Caché en Ajustes: `Nombre`, `Detalle`, `Modulo` |
| `EntradaSDVM` | (`Models/Cache/`) ViewModel para el explorador SD en Ajustes ? Carpetas Protegidas. Implementa `INotifyPropertyChanged`. Props: `Nombre`, `Tipo` (`EsTipoEntrada`), `EstaProtegido` (notifica cambios), `EsCritico` (deriva de `EntradaSD.NombresCriticos`), `IconoUrl` (resuelve `IconoCarpetaUrl` / `IconoZipUrl` / `IconoArchivoUrl` según tipo) |

---

## ?? Paquetes NuGet

| Paquete | Versión | Uso |
|---|---|---|
| `SharpCompress` | 0.47.4 | Extracción de ZIP/RAR/7z en `ZipLogic` |
| `SixLabors.ImageSharp` | 3.1.12 | Conversión de imágenes en `ImageConverter` |
| `System.Management` | 10.0.5 | WMI para detección de discos en `Hardware/` |

---

## ?? Zonas a NO buscar

- `bin/` — archivos compilados
- `obj/` — artefactos de build
- `NewFolder1/`, `NewFolder2/` — excluidos del proyecto explícitamente
- `NX-Suite.Updater/` — proyecto separado, solo maneja auto-actualización

---

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

*Última actualización: 2025 — rama `feat(Limpieza-de-micro-sd)`*
