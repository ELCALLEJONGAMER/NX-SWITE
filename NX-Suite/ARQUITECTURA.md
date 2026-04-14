# 🗺️ Mapa de Arquitectura - NX-Suite (Actualizado)

**Framework:** WPF (C#) / **Arquitectura:** Server-Driven UI (JSON en GitHub Gist) con Estado Reactivo Local.

---

## 1. 🗂️ MODELS (Moldes de Datos y UI)

* [cite_start]**`Modelos.cs`**: Contiene `ConfiguracionUI` (Colores e íconos dinámicos desde la nube) y `GistData` (Clase raíz que recibe y organiza el JSON)[cite: 2, 51].
* [cite_start]**`ModuloModelo.cs`**: Define `ModuloConfig`, el corazón reactivo de la aplicación[cite: 53].
    * [cite_start]**Reactividad (INotifyPropertyChanged)**: La propiedad `EstaEnCache` notifica a la interfaz en tiempo real cuando un archivo se descarga o se elimina[cite: 53].
    * [cite_start]**Propiedades Calculadas**: `IconoCacheActual` y `MensajeCacheActual` se actualizan solas basándose en el estado de la caché física[cite: 53].
* [cite_start]**`CategoriaModelo.cs`**: Define las categorías para el filtrado, permitiendo que la UI resalte la selección activa mediante notificaciones de cambio[cite: 137].

## 2. ⚙️ SERVICES (Motores y Lógica de Negocio)

* [cite_start]**`GistParser.cs`**: Cliente HTTP que descarga el JSON del Gist y lo convierte en objetos C#[cite: 57, 112].
* [cite_start]**`DownloadLogic.cs`**: Gestiona la descarga de archivos ZIP hacia `AppData/NX-Suite/Cache/Zips` con reportes de progreso en tiempo real[cite: 108, 110].
* [cite_start]**`ZipLogic.cs`**: Motor de descompresión que expande los paquetes en la subcarpeta `Extracted`[cite: 91].
* [cite_start]**`ReglasLogic.cs`**: Motor de instalación que ejecuta comandos JSON (`CrearCarpeta`, `Mover`, `Borrar`, `Renombrar`) para organizar la SD[cite: 91].
* [cite_start]**`UninstallLogic.cs`**: Lee el array `RutasDesinstalacion` y elimina de forma segura los archivos específicos de la Micro SD[cite: 91, 107].
* [cite_start]**`SDMonitorLogic.cs`**: Utiliza APIs de Windows para detectar la inserción/extracción de SDs y leer sus metadatos (formato, serial, capacidad)[cite: 91].
* [cite_start]**`SHA256Logic.cs`**: Verifica que los archivos descargados coincidan con el Hash del Gist para evitar archivos corruptos[cite: 91].
* [cite_start]**`ConfiguracionPro.cs`**: Archivo de configuración estática con las URLs maestras de GitHub y rutas de carpetas internas[cite: 107].

## 3. 🖥️ VIEWS & CONTROLLERS (Interfaz y Controladores)

* **`App.xaml`**: **Repositorio Único de Recursos**.
    * [cite_start]Contiene la **Plantilla Única** de la tarjeta (`PlantillaModuloGamer`), eliminando duplicados para asegurar consistencia visual[cite: 1, 46].
    * [cite_start]Define el botón global de borrado rápido ("X") mediante `DataTriggers` que reaccionan al estado de la caché[cite: 50, 53].
* **`App.xaml.cs`**: **Controlador Global de Eventos**.
    * [cite_start]**`BtnEliminarCache_Click`**: Lógica centralizada para borrar físicamente archivos ZIP y carpetas de la PC[cite: 130].
    * **`Tarjeta_Redirigir_Click`**: Actúa como puente para que la plantilla global de `App.xaml` pueda abrir la `VistaDetalle` en la ventana principal[cite: 130].
* [cite_start]**`MainWindow.xaml`**: Estructura principal de la aplicación con paneles animados para navegación, herramientas de SD y catálogo[cite: 65, 71, 130].
* **`MainWindow.xaml.cs`**: **Coordinador General**.
    * [cite_start]Maneja las transiciones de pantalla y la lógica de los paneles desplegables[cite: 91].
    * **`RefrescarEstadoCacheModulos()`**: Método público que escanea físicamente la carpeta de Zips para sincronizar los iconos verdes y las "X" rojas[cite: 91].

---

### 🔄 Flujo de Sincronización
Cuando el usuario presiona la **"X"** en una tarjeta, el evento viaja a `App.xaml.cs`, borra los archivos, pone `EstaEnCache` en `false` y el modelo avisa automáticamente a la tarjeta para que oculte la "X" y cambie el disco a rojo, todo sin recargar la lista.