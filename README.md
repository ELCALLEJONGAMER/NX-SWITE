# NX-SWITE
# NX-Suite

<div align="center">

**Herramienta de gesti�n todo-en-uno para Nintendo Switch**

[![Versi�n](https://img.shields.io/github/v/release/ELCALLEJONGAMER/NX-SWITE?include_prereleases&label=versi�n)](https://github.com/ELCALLEJONGAMER/NX-SWITE/releases)
[![Licencia](https://img.shields.io/github/license/ELCALLEJONGAMER/NX-SWITE)](LICENSE)
[![Issues](https://img.shields.io/github/issues/ELCALLEJONGAMER/NX-SWITE)](https://github.com/ELCALLEJONGAMER/NX-SWITE/issues)
[![Plataforma](https://img.shields.io/badge/plataforma-Windows%20x64-blue)](https://github.com/ELCALLEJONGAMER/NX-SWITE/releases)

</div>

---

## �Qu� es NX-Suite?

NX-Suite es una aplicaci�n de escritorio para Windows que simplifica la gesti�n de tarjetas SD para Nintendo Switch con custom firmware (CFW). Permite instalar, actualizar y administrar m�dulos como Atmosphere, Hekate, y otros, de forma visual y guiada sin necesidad de hacerlo manualmente.

### Caracter�sticas principales

- ?? **Instalaci�n guiada** de m�dulos y CFW en la SD
- ?? **Actualizaciones autom�ticas** de m�dulos instalados
- ?? **Temas y personalizaci�n** de la interfaz
- ?? **Formateo y particionado** de tarjetas SD
- ?? **Detecci�n autom�tica** de versiones instaladas
- ? **Auto-actualizaci�n** de la propia aplicaci�n

---

## Descarga

> **Esta aplicaci�n est� actualmente en fase beta.** Pueden existir errores. Si encuentras alguno, por favor [abre un Issue](https://github.com/ELCALLEJONGAMER/NX-SWITE/issues/new/choose).

Descarga la �ltima versi�n desde la secci�n de [**Releases**](https://github.com/ELCALLEJONGAMER/NX-SWITE/releases).

### Requisitos

- Windows 10/11 (64-bit)
- No requiere instalar .NET (incluido en el ejecutable)

### Instalaci�n

1. Descarga el `.zip` de la �ltima release
2. Extrae la carpeta donde quieras (ej. `C:\NX-Suite\`)
3. Ejecuta `NX-Suite.exe`
4. *(Solo primera vez)* Windows puede mostrar una advertencia de SmartScreen ? clic en **"M�s informaci�n" ? "Ejecutar de todas formas"**

> ?? Mant�n `NX-Suite.exe` y `NX-Suite.Updater.exe` siempre en la misma carpeta. El updater es necesario para que las actualizaciones autom�ticas funcionen.

---

## Compilar desde el c�digo fuente

### Requisitos previos

- [Visual Studio 2022](https://visualstudio.microsoft.com/) con el workload **.NET Desktop Development**
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Pasos

```bash
git clone https://github.com/ELCALLEJONGAMER/NX-SWITE.git
cd NX-SWITE
dotnet build
```

### Generar build de distribuci�n

```powershell
.\publish-beta.ps1
```

El resultado se genera en `dist\beta\`.

---

## Contribuir

�Las contribuciones son bienvenidas! Por favor:

1. Haz un **Fork** del repositorio
2. Crea una rama para tu cambio: `git checkout -b feature/mi-mejora`
3. Haz commit de tus cambios: `git commit -m "feat: descripci�n del cambio"`
4. Abre un **Pull Request** describiendo qu� cambia y por qu�

Para bugs o sugerencias, usa la secci�n de [**Issues**](https://github.com/ELCALLEJONGAMER/NX-SWITE/issues).

---

## Licencia

Este proyecto est� bajo la licencia [MIT](LICENSE). Puedes usar, modificar y distribuir el c�digo libremente siempre que incluyas la atribuci�n original.

---

<div align="center">
Hecho con ?? por <a href="https://github.com/ELCALLEJONGAMER">ELCALLEJONGAMER</a>
</div>
