# NX-SWITE


<div align="center">

**Herramienta de gestión todo-en-uno para Nintendo Switch**

[![Versión](https://img.shields.io/github/v/release/ELCALLEJONGAMER/NX-SWITE?include_prereleases&label=versión)](https://github.com/ELCALLEJONGAMER/NX-SWITE/releases)
[![Licencia](https://img.shields.io/github/license/ELCALLEJONGAMER/NX-SWITE)](LICENSE)
[![Issues](https://img.shields.io/github/issues/ELCALLEJONGAMER/NX-SWITE)](https://github.com/ELCALLEJONGAMER/NX-SWITE/issues)
[![Plataforma](https://img.shields.io/badge/plataforma-Windows%20x64-blue)](https://github.com/ELCALLEJONGAMER/NX-SWITE/releases)

</div>

---

## ¿Qué es NX-Suite?

NX-Suite es una aplicación de escritorio para Windows que simplifica la gestión de tarjetas SD para Nintendo Switch con custom firmware (CFW). Permite instalar, actualizar y administrar módulos como Atmosphere, Hekate, y otros, de forma visual y guiada sin necesidad de hacerlo manualmente.

### Características principales

- ?? **Instalación guiada** de módulos y CFW en la SD
- ?? **Actualizaciones automáticas** de módulos instalados
- ?? **Temas y personalización** de la interfaz
- ?? **Formateo y particionado** de tarjetas SD
- ?? **Detección automática** de versiones instaladas
- ? **Auto-actualización** de la propia aplicación

---

## Descarga

> **Esta aplicación está actualmente en fase beta.** Pueden existir errores. Si encuentras alguno, por favor [abre un Issue](https://github.com/ELCALLEJONGAMER/NX-SWITE/issues/new/choose).

Descarga la última versión desde la sección de [**Releases**](https://github.com/ELCALLEJONGAMER/NX-SWITE/releases).

### Requisitos

- Windows 10/11 (64-bit)
- No requiere instalar .NET (incluido en el ejecutable)

### Instalación

1. Descarga el `.zip` de la última release
2. Extrae la carpeta donde quieras (ej. `C:\NX-Suite\`)
3. Ejecuta `NX-Suite.exe`
4. *(Solo primera vez)* Windows puede mostrar una advertencia de SmartScreen ? clic en **"Más información" ? "Ejecutar de todas formas"**

> ?? Mantén `NX-Suite.exe` y `NX-Suite.Updater.exe` siempre en la misma carpeta. El updater es necesario para que las actualizaciones automáticas funcionen.

---

## Compilar desde el código fuente

### Requisitos previos

- [Visual Studio 2022](https://visualstudio.microsoft.com/) con el workload **.NET Desktop Development**
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Pasos

```bash
git clone https://github.com/ELCALLEJONGAMER/NX-SWITE.git
cd NX-SWITE
dotnet build
```

### Generar build de distribución

```powershell
.\publish-beta.ps1
```

El resultado se genera en `dist\beta\`.

---

## Contribuir

¡Las contribuciones son bienvenidas! Por favor:

1. Haz un **Fork** del repositorio
2. Crea una rama para tu cambio: `git checkout -b feature/mi-mejora`
3. Haz commit de tus cambios: `git commit -m "feat: descripción del cambio"`
4. Abre un **Pull Request** describiendo qué cambia y por qué

Para bugs o sugerencias, usa la sección de [**Issues**](https://github.com/ELCALLEJONGAMER/NX-SWITE/issues).

---

## Licencia

Este proyecto está bajo la licencia [MIT](LICENSE). Puedes usar, modificar y distribuir el código libremente siempre que incluyas la atribución original.

---

<div align="center">
Hecho con ?? por <a href="https://github.com/ELCALLEJONGAMER">ELCALLEJONGAMER</a>
</div>
