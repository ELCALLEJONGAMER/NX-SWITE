# NX-SWITE-Switch

Aplicación homebrew para Nintendo Switch (formato `.nro`), parte del ecosistema NX-SWITE.

Este proyecto es **completamente independiente** de `NX-Suite` (la app WPF/.NET de Windows).
Comparte repositorio solo por conveniencia organizativa; no comparte código, dependencias ni build.

## Estado actual

Prueba mínima de flujo de compilación: imprime un mensaje fijo en consola libnx y permite
salir con el botón `+`. No incluye red, JSON, descargas, ni lógica de actualización todavía.

## Requisitos

- [devkitPro](https://devkitpro.org/) con **devkitA64** y **libnx** instalados.
- Variables de entorno `DEVKITPRO` y `DEVKITA64` configuradas.

## Compilar

Desde el MSYS2 de devkitPro (o cualquier shell con `make` y devkitA64 en el `PATH`):

```bash
cd NX-SWITE-Switch
make
```

El resultado será `NX-SWITE-Switch.nro` en la raíz de esta carpeta.

## Limpiar

```bash
make clean
```

## Estructura

```
NX-SWITE-Switch/
??? source/     # Código fuente C++ (.cpp)
??? include/    # Cabeceras propias del proyecto
??? assets/     # Recursos (romfs, icono, etc.) — vacío por ahora
??? Makefile
??? README.md
```
