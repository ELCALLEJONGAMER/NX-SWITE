# NX-Suite

<div align="center">

**Herramienta de gestion todo-en-uno para Nintendo Switch**

[![Version](https://img.shields.io/github/v/release/ELCALLEJONGAMER/NX-SWITE?include_prereleases&label=version)](https://github.com/ELCALLEJONGAMER/NX-SWITE/releases)
[![Licencia](https://img.shields.io/github/license/ELCALLEJONGAMER/NX-SWITE)](LICENSE)
[![Issues](https://img.shields.io/github/issues/ELCALLEJONGAMER/NX-SWITE)](https://github.com/ELCALLEJONGAMER/NX-SWITE/issues)
[![Plataforma](https://img.shields.io/badge/plataforma-Windows%20x64-blue)](https://github.com/ELCALLEJONGAMER/NX-SWITE/releases)

</div>

---

## Que es NX-Suite?

NX-Suite es una aplicacion de escritorio para Windows que simplifica la gestion de tarjetas SD para Nintendo Switch con custom firmware (CFW). Permite instalar, actualizar y administrar modulos como Atmosphere, Hekate, y otros, de forma visual y guiada sin necesidad de hacerlo manualmente.

### Caracteristicas principales

- **Instalacion guiada** de modulos y CFW en la SD
- **Actualizaciones automaticas** de modulos instalados
- **Temas y personalizacion** de la interfaz
- **Formateo y particionado** de tarjetas SD
- **Deteccion automatica** de versiones instaladas
- **Auto-actualizacion** de la propia aplicacion

---

## Descarga

> **Esta aplicacion esta actualmente en fase beta.** Pueden existir errores.
> Si encuentras alguno, por favor [abre un Issue](https://github.com/ELCALLEJONGAMER/NX-SWITE/issues/new/choose).

Descarga la ultima version desde la seccion de [**Releases**](https://github.com/ELCALLEJONGAMER/NX-SWITE/releases).

### Requisitos

- Windows 10/11 (64-bit)
- No requiere instalar .NET (incluido en el ejecutable)

### Instalacion

1. Descarga el `.zip` de la ultima release
2. Extrae la carpeta donde quieras, por ejemplo `C:\NX-Suite\`
3. Ejecuta `NX-Suite.exe`
4. **Solo la primera vez:** Windows puede mostrar SmartScreen, haz clic en **"Mas informacion"** y luego **"Ejecutar de todas formas"**

> **Importante:** Manten `NX-Suite.exe` y `NX-Suite.Updater.exe` siempre en la misma carpeta.

---

## Compilar desde el codigo fuente

### Requisitos previos

- [Visual Studio 2022](https://visualstudio.microsoft.com/) con el workload **.NET Desktop Development**
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Pasos

    git clone https://github.com/ELCALLEJONGAMER/NX-SWITE.git
    cd NX-SWITE
    dotnet build

### Generar build de distribucion

    .\publish-beta.ps1

El resultado se genera en la carpeta `dist\beta\`.

---

## Flujo de actualizacion para testers

Cuando hay una nueva version disponible:

1. NX-Suite compara automaticamente su version con la del servidor al iniciarse
2. Si hay una version nueva, aparece un indicador verde en la barra superior
3. Al hacer clic, se muestra el panel de actualizacion con el changelog
4. El tester acepta y `NX-Suite.Updater.exe` descarga, reemplaza y reinicia la app

---

## Reportar un problema

Usa la seccion de [**Issues**](https://github.com/ELCALLEJONGAMER/NX-SWITE/issues/new/choose).
Hay plantillas para:

- **Bug report** - si algo no funciona como deberia
- **Sugerencia** - si tienes una idea de mejora

---

## Contribuir

Las contribuciones son bienvenidas. Por favor:

1. Haz un **Fork** del repositorio
2. Crea una rama: `git checkout -b feature/mi-mejora`
3. Haz commit: `git commit -m "feat: descripcion del cambio"`
4. Abre un **Pull Request** describiendo que cambia y por que

---

## Licencia

Este proyecto esta bajo la licencia [MIT](LICENSE).
Puedes usar, modificar y distribuir el codigo libremente
siempre que incluyas la atribucion original.

---

<div align="center">
Hecho con amor por <a href="https://github.com/ELCALLEJONGAMER">ELCALLEJONGAMER</a>
</div>
