# CODEBASE_INDEX.md — Índice técnico de NX-SWITE-Switch

Índice compacto y práctico. Ver `CLAUDE.md` para las reglas de arquitectura.

---

## Estado actual (implementado)

### main.cpp
Archivo: `source/main.cpp`

Responsabilidad:
Punto de entrada de la app homebrew. Inicializa consola/sockets/curl,
orquesta el flujo de prueba (descarga ? extracción), muestra mensajes de
estado por consola y espera regreso a HOME.

Funciones/estructuras principales:
- `main(...)` — flujo principal (inicialización, descarga, extracción, espera).
- `EsperarSalida(PadState&)` — mantiene viva la app hasta que `appletMainLoop()`
  termine (no hay salida manual por botón).
- `GuardaSockets` / `GuardaCurl` / `GuardaConsola` — guardas RAII que
  garantizan `socketExit()`, `Downloader::Finalizar()` y `consoleExit(NULL)`
  exactamente una vez, sin importar el camino de retorno de `main()`.

Dependencias: `Downloader.hpp`, `ZipExtractor.hpp`, `switch.h` (libnx).

Notas importantes:
- Actualmente TODA la lógica de presentación vive en `main.cpp` (no hay
  Views/Services todavía). Ver "Arquitectura prevista" más abajo — es deuda
  técnica conocida y aceptada mientras el proyecto es una prueba funcional.
- La UI de progreso de descarga/extracción está simplificada
  TEMPORALMENTE (solo líneas estáticas "Descargando paquete..." /
  "Extrayendo paquete...") hasta que se implemente una interfaz visual real.
- Textos actualmente hardcodeados en español directamente en `main.cpp`
  (pendiente de migrar a claves de traducción, ver regla 6/7 de `CLAUDE.md`).

---

### Downloader (servicio de descarga)
Archivos:
- `include/Downloader.hpp`
- `source/Downloader.cpp`

Responsabilidad:
Descarga HTTP/HTTPS de un archivo a la SD usando libcurl, con escritura
"buffered" (acumulador de 1 MiB) para minimizar el número de `fwrite()`.

Funciones principales:
- `Downloader::Inicializar()` — `curl_global_init()`.
- `Downloader::Finalizar()` — `curl_global_cleanup()`.
- `Downloader::DescargarArchivo(url, rutaDestino, onProgreso = nullptr)` —
  descarga y guarda en `rutaDestino`; crea carpetas intermedias; reporta
  métricas de rendimiento en el `ResultadoDescarga` devuelto
  (`tamanoBytes`, `tiempoSegundos`, `velocidadPromedioMBs`,
  `numeroEscrituras`).

Detalles internos relevantes:
- `BuferEscritura`: buffer acumulador de 1 MiB reservado una única vez;
  `fwrite()` solo se llama cuando el buffer se llena o al finalizar la
  descarga (resto pendiente). Sin `fflush()` por bloque.
- `ProgresoCurl(...)` — callback `CURLOPT_XFERINFOFUNCTION`; limita el
  reporte de progreso a cambios de porcentaje entero + ?200ms, para no
  saturar la consola.
- `CURLOPT_BUFFERSIZE` configurado a 256 KB.

Dependencias: libcurl, `switch.h` (para `armGetSystemTick`).

Notas importantes:
- Antes de esta optimización la velocidad de escritura a SD era el cuello
  de botella (~1.3 MB/s); con el buffer de 1 MiB se alcanzó ~4.17 MB/s en
  hardware real (ver decisión documentada en `CLAUDE.md` si se amplía en
  el futuro).

---

### ZipExtractor (servicio de extracción)
Archivos:
- `include/ZipExtractor.hpp`
- `source/ZipExtractor.cpp`

Responsabilidad:
Extrae el contenido completo de un archivo ZIP (minizip) a una carpeta de
staging en la SD, con protecciones contra path traversal y limpieza previa
del staging.

Funciones principales:
- `ZipExtractor::RecrearCarpetaVacia(carpetaDestino)` — elimina
  recursivamente `carpetaDestino` (si existe) y la recrea vacía; si no puede
  dejarla limpia, la extracción se cancela.
- `ZipExtractor::ExtraerTodo(rutaZip, carpetaDestino, onProgreso = nullptr)`
  — extrae todas las entradas del ZIP; usa un buffer reutilizable de 256 KB;
  crea solo las carpetas padre necesarias para archivos (nunca trata un
  archivo como directorio); reporta `archivosExtraidos` y `tiempoSegundos`
  en el `ResultadoExtraccion`.

Detalles internos relevantes:
- `NormalizarSeparadores(...)` / `ContieneComponentePadre(...)` — normaliza
  `\`?`/` y rechaza cualquier entrada con componente `..` (protección contra
  rutas fuera de staging).
- `TerminaConBarra(...)` — una entrada es directorio SOLO si termina
  explícitamente en `/`.
- `EliminarRecursivo(...)` — usado por `RecrearCarpetaVacia` para limpiar
  el staging antes de cada extracción.

Dependencias: minizip (`unzip.h`), `switch.h` (para `armGetSystemTick`).

Notas importantes:
- El progreso por entrada existe en la firma (`onProgreso`), pero
  actualmente `main.cpp` no lo usa (pasa `nullptr`) porque la barra ASCII
  provisional generaba salida en cascada; se retomará al implementar la
  interfaz visual real.

---

### Build
Archivo: `Makefile`

- C++17, toolchain devkitA64/libnx (ver regla 13 de `CLAUDE.md`).
- Librerías enlazadas: `libcurl`, `libminizip`, `libz`, `libnx`.
- `SOURCES := source`, `INCLUDES := include`, `DATA := assets`.

---

## Arquitectura prevista (PLAN — no implementado todavía)

La siguiente estructura es la dirección deseada a futuro, conforme
`CLAUDE.md`. Nada de esto existe hoy salvo lo ya listado arriba.

```
NX-SWITE-Switch/
  source/
    main.cpp                  (existe: se reducirá a init + loop + cleanup)
    services/
      Downloader.cpp          (existe, se moveria aqui)
      ZipExtractor.cpp        (existe, se moveria aqui)
      GistClient.cpp          (futuro)
      UpdateService.cpp       (futuro)
      FirmwareService.cpp     (futuro)
    views/
      HomeView.cpp            (futuro)
      UpdateView.cpp          (futuro)
      FirmwareView.cpp        (futuro)
      ToolsView.cpp           (futuro)
      SettingsView.cpp        (futuro)
    ui/
      componentes reutilizables: Button, BackButton, ProgressBar,
      Modal, Toast, Card...    (futuro)
    core/
      Router / navegación centralizada (futuro)
      InputManager centralizado (futuro)
    localization/
      Tr(clave) + carga de JSON (futuro)
  include/
    (headers correspondientes a lo anterior)
  languages/
    es.json                   (futuro, idioma por defecto)
    en.json                   (futuro)
```

Principios que guiarán esta migración (ver `CLAUDE.md` para detalle):
- Servicios independientes de la UI (regla 4).
- Componentes de UI reutilizables, no duplicados por pantalla (reglas 1-2).
- Multidioma por claves desde el principio de cualquier View nueva
  (reglas 6-7).
- Navegación e input centralizados (reglas 9-10).
- Sin sobrearquitectura: solo se crean estas capas cuando aporten valor real
  (regla 12).

No se debe implementar esta estructura sin autorización explícita previa.
