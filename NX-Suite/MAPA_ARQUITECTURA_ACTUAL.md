# Mapa de arquitectura actual - NX-Suite

## 0. Raíz real del proyecto

La ruta correcta del proyecto es:

`F:\Proyectos\NX-Suite\NX-Suite`

Dentro de esa raíz están las carpetas principales del código, por ejemplo:

- `Core`
- `UI`
- `Services`
- `Models`
- `Network`
- `Hardware`

> Importante: no asumir otra ubicación intermedia.  
> Cuando se hable de archivos del proyecto, siempre partir desde esta raíz.

---

## 1. Objetivo general

NX-Suite es una aplicación WPF para:

- cargar catálogo desde Gist
- mostrar módulos en tarjetas
- detectar SD
- instalar y desinstalar módulos
- limpiar caché
- preparar tareas de disco y formato

El objetivo actual es:

- ordenar el proyecto
- centralizar estilos
- separar la lógica de `MainWindow`
- dejar una base limpia para añadir funciones nuevas después

---

## 2. Interpretación funcional del programa

NX-Suite funciona como un **orquestador de gestión para SD de Nintendo Switch**.

### Finalidad principal
Permite:

- sincronizar un catálogo remoto
- visualizar módulos en la interfaz
- detectar qué hay instalado en la SD
- instalar o eliminar módulos
- limpiar descargas y caché local
- preparar operaciones de extracción, copia y formato

### Idea de arquitectura
- **UI**: muestra datos y recibe eventos
- **Core**: ejecuta la lógica real
- **Network**: obtiene datos remotos
- **Hardware**: gestiona discos y SD
- **Models**: transportan datos
- **Styles**: centralizan apariencia

---

## 3. Regla principal de arquitectura

### UI
La UI debe:

- mostrar datos
- recibir eventos
- cambiar vistas
- no contener lógica pesada

### Core
El `Core` debe:

- orquestar procesos
- ejecutar reglas
- descargar
- extraer
- filtrar
- limpiar caché
- desinstalar

### Models
Los modelos deben:

- transportar datos
- no perder información del `Gist`
- mantener campos clave como `Id`, `Categoria` y `Mundo`

### Estilos
Los estilos deben:

- estar centralizados
- evitar colores y plantillas repetidas
- reutilizar botones y tarjetas

---

## 4. Estado actual de la estructura

### 4.1 `MainWindow.xaml`
Archivo principal de la ventana.

Responsabilidad:

- alojar el catálogo
- alojar el detalle
- alojar overlays
- conectar controles laterales
- usar recursos globales

Estado:

- todavía contiene algunos colores y diseño embebido
- ya se empezó a centralizar estilos
- conviene seguir limpiándolo poco a poco

---

### 4.2 `MainWindow.xaml.cs`
Archivo de orquestación de la ventana.

Responsabilidad ideal:

- enlazar eventos
- cargar datos iniciales
- mostrar detalle
- instalar / borrar
- cambiar vistas
- llamar a `ISuiteController`

Estado actual:

- ya se empezaron a separar métodos
- se han limpiado y simplificado:
  - `ConfigurarEventos()`
  - `CargarCatalogoInicialAsync()`
  - `ActualizarListaUnidadesAsync()`
  - `ComboDrives_SelectionChanged()`
  - `AbrirDetalleModulo()`

Regla:

- no meter nueva lógica pesada aquí
- mantenerlo como ventana de control

---

### 4.3 `Core\SuiteController.cs`
Responsable de coordinar acciones principales.

Hace:

- sincronización completa
- obtención de unidades removibles
- información para panel derecho
- instalación
- desinstalación
- limpieza de caché
- filtrado

Estado:

- es el punto central correcto
- aún puede refinarse más adelante

---

### 4.4 `Core\SuiteControllerFacade.cs`
Capa de delegación entre UI y `SuiteController`.

Estado:

- simple
- correcto
- no necesita lógica propia

---

### 4.5 `Core\ISuiteController.cs`
Contrato de las operaciones principales.

Estado:

- correcto
- debe mantenerse claro y estable

---

### 4.6 `Core\ReglasLogic.cs`
Motor del pipeline de instalación.

Hace:

- descargar
- extraer
- copiar
- borrar
- crear carpetas
- ejecutar comandos
- respaldar
- restaurar
- formatear SD

Estado:

- es una de las piezas más grandes
- se limpiará después de `MainWindow`
- conviene dividirla por pasos

---

### 4.7 `Core\ZipLogic.cs`
Motor de extracción.

Estado:

- bien ubicado
- ya está bastante separado
- puede pulirse después

---

### 4.8 `Core\GestorCache.cs`
Gestiona:

- carpeta de ZIPs
- carpeta de extraídos
- estado de caché
- borrado de caché local

Estado:

- correcto
- alineado con su responsabilidad

---

### 4.9 `Core\DownloadLogic.cs`
Responsable de descargas.

Estado:

- correcto
- separado de la UI

---

### 4.10 `Core\DetectorVersionesLogic.cs`
Detecta qué versión está instalada en la SD.

Estado:

- correcto
- depende de `SHA256Logic`

---

### 4.11 `Core\UninstallLogic.cs`
Desinstalación de archivos y carpetas.

Estado:

- correcto
- tarea única

---

### 4.12 `Core\FiltroLogic.cs`
Filtrado del catálogo.

Estado:

- correcto
- lógica pura
- sin UI

---

### 4.13 `Network\GistParser.cs`
Descarga y deserializa el JSON del Gist.

Estado:

- funcional
- todavía mezcla red con mensajes de UI
- más adelante conviene quitar `MessageBox` de aquí

---

### 4.14 `Hardware\DiskMaster.cs`
Gestiona:

- detección de unidades removibles
- seriales
- disco físico
- escucha de cambios
- cierre de ventanas del explorador

Estado:

- funcional
- mezcla varias responsabilidades
- más adelante puede dividirse

---

### 4.15 `Models\Modelos.cs`
Contiene:

- `ConfiguracionUI`
- `GistData`
- `EstadoProgreso`
- `InfoPanelDerecho`
- `BrandingConfig`
- `MundoMenuConfig`
- `FiltroMandoConfig`

Estado:

- se han añadido valores por defecto
- evita `null`
- prepara mejor la UI

---

### 4.16 `Models\ModuloModelo.cs`
Contiene:

- `PasoPipeline`
- `ModuloVersion`
- `ModuloConfig`
- `FirmaDeteccion`
- `ArchivoCritico`

Estado:

- `Id`, `Categoria` y `Mundo` se mantienen
- son datos importantes del `Gist`
- no deben borrarse
- solo se limpian riesgos de `null` y fallos visuales

---

## 5. Estado de la UI y estilos

### 5.1 `UI\Estilos\EstilosBotones.xaml`
Archivo central de estilos.

Estado actual:

- ya se está usando como base para botones
- se centralizaron varios estilos
- se corrigieron recursos faltantes

Debe contener:

- estilos de botones
- estilos de listas
- templates reutilizables
- colores y efectos centralizados

Regla:

- no repetir diseño dentro de las vistas
- si un estilo se reutiliza, debe vivir aquí

---

### 5.2 `UI\Controles\PanelIzquierdo.xaml`
Panel lateral izquierdo.

Estado:

- visual
- muestra mundos
- usa templates centralizados

---

### 5.3 `UI\Controles\RetractilIzq.xaml`
Panel del centro de mando.

Estado:

- visual
- muestra categorías
- todavía puede quedar para revisión posterior

---

### 5.4 `UI\Controles\RetractilDer.xaml`
Panel Arsenal SD.

Estado:

- visual
- tiene botones de acciones de SD
- su diseño sirve como referencia visual

---

### 5.5 `UI\Controles\PanelDerecho.xaml`
Panel de información de SD.

Estado:

- visual
- usa estilos centralizados
- ya se corrigieron errores de recursos

---

## 6. Cambios ya decididos

### Centralización visual
Se decidió:

- mover botones reutilizables a `EstilosBotones.xaml`
- evitar colores sueltos en vistas
- unificar estilos

### Limpieza de `MainWindow`
Se decidió:

- no dejar la lógica grande dentro de la ventana
- separar en métodos pequeños
- mantener solo orquestación y eventos

### Paneles laterales
Se decidió:

- dejarlos en pausa
- no seguir definiendo su diseño ahora
- volver a ellos cuando la base esté más limpia

---

## 7. Estado actual del trabajo

### Ya resuelto
- limpieza y orden de `MainWindow.xaml.cs`
- centralización parcial de estilos
- ajuste de modelos para evitar `null`
- exclusión de `bin` y `obj` del control de versiones
- rama de trabajo separada para mejoras

### Estado actual
El proyecto ya está en una fase en la que se puede empezar a:

- perfeccionar funciones
- corregir lógica
- ajustar detalles visuales
- añadir mejoras nuevas sin romper la base

---

## 8. Orden recomendado de trabajo

1. `MainWindow.xaml.cs`
2. `MainWindow.xaml`
3. `UI\Estilos\EstilosBotones.xaml`
4. `Core\ReglasLogic.cs`
5. `Network\GistParser.cs`
6. `Hardware\DiskMaster.cs`

---

## 9. Regla de trabajo actual

- una rama = un objetivo
- un commit = un cambio pequeño
- compilar después de cada ajuste
- no tocar archivos generados de `obj`
- no borrar datos importantes del `Gist`
- no mezclar refactor con nuevas funciones

---

## 10. Resumen corto

La estructura ideal es:

- **UI**: muestra y recibe eventos
- **Core**: ejecuta la lógica
- **Network**: obtiene datos remotos
- **Hardware**: maneja discos
- **Models**: transporta datos
- **Styles**: centraliza la apariencia

El siguiente paso es seguir limpiando `MainWindow.xaml.cs` sin romper la aplicación.

---

## 2.1 Receta central del sistema

El `Gist JSON` es la fuente de verdad del programa.

Desde ese JSON se define:

- qué módulos existen
- qué tarjeta se muestra en el catálogo
- qué icono usa cada módulo
- qué URL oficial se descarga
- qué pipeline de instalación sigue
- qué rutas se eliminan al desinstalar
- qué archivos se comparan para detectar instalación
- qué versiones se consideran válidas

### Filosofía del sistema
NX-Suite no se basa en paquetes fijos dentro de la app.  
Se basa en una **receta modular externa** que permite:

- construir paquetes desde fuentes oficiales
- personalizar cada módulo sin recompilar la app
- detectar módulos instalados por comparación `SHA256`
- mostrar de forma clara el estado de cada tarjeta en el catálogo

### Detección de instalación
La instalación de un módulo se determina por comparación de hashes `SHA256` de uno o varios archivos definidos en el JSON.

Eso permite:
- saber si el módulo está instalado
- saber si requiere actualización
- mostrar el estado real en el menú principal
- evitar depender solo de nombres de carpetas o archivos sueltos