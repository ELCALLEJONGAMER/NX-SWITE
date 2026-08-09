# CLAUDE.md — Reglas obligatorias para NX-SWITE-Switch

Este documento define las reglas de arquitectura y desarrollo que cualquier
agente/IA (o colaborador humano) debe seguir al modificar `NX-SWITE-Switch`.

Es un **documento vivo**: ver regla 20 sobre cómo actualizarlo.

---

## 1. Reutilización de componentes

No duplicar botones, controles, efectos, modales, barras de progreso,
encabezados, footers ni elementos visuales que cumplan la misma función.

- ? `BackButtonHome`, `BackButtonSettings`, `BackButtonFirmware`
- ? Un componente reutilizable equivalente a `Button` / `BackButton`

Aplica también a: Aceptar, Cancelar, Volver, Confirmar, Tarjetas, Modales,
Indicadores de progreso, Mensajes de error, Toasts, Selectores, Headers,
Footers.

Si ya existe un componente que cubre el caso de uso, reutilizarlo antes de
crear uno nuevo.

## 2. Efectos visuales reutilizables

Los estados y efectos de controles se definen **centralmente**, nunca
manualmente en cada pantalla.

Estados a centralizar: Normal, Focused, Pressed, Disabled, Selected, Hover
(si aplica alguna vez), Success, Warning, Error.

Los cambios visuales globales deben poder modificarse desde un único lugar.

## 3. Vistas separadas

Cada pantalla o sección principal tendrá su propio archivo/módulo:

```
Views/
  HomeView
  UpdateView
  FirmwareView
  ToolsView
  SettingsView
```

`main.cpp` se limita a:
- inicialización
- ciclo principal
- navegación de alto nivel
- cleanup

Cada vista se encarga únicamente de presentar e interactuar con su sección.
Los componentes reutilizables NO se copian dentro de cada View.

## 4. Separación entre UI y lógica

Las vistas NO contienen directamente lógica de HTTP, descargas, ZIP,
filesystem, Gist, actualización, firmware o detección.

Usar servicios/clases independientes:

```
Services/
  Downloader
  ZipExtractor
  GistClient
  UpdateService
  FirmwareService

Views/
  HomeView
  UpdateView
```

La UI llama a los servicios; los servicios no dependen de cómo se dibuja la
interfaz.

## 5. Índice de código

Mantener `CODEBASE_INDEX.md` actualizado. Debe permitir encontrar
rápidamente: Views, componentes UI, servicios, modelos, manejo de entrada,
navegación, descargas, extracción ZIP, Gist, actualización, filesystem,
configuración, idiomas.

No hacer un índice línea por línea; mantenerlo compacto y práctico.

## 6. Multidioma desde el principio

Ningún texto visible al usuario debe quedar hardcodeado directamente dentro
de las Views, salvo textos temporales marcados explícitamente como `DEBUG`.

Formato preferido: JSON.

```
languages/
  es.json
  en.json
```

```json
{
  "common.back": "Volver",
  "common.accept": "Aceptar",
  "home.title": "Inicio",
  "update.downloading": "Descargando paquete...",
  "update.extracting": "Extrayendo paquete..."
}
```

El código trabaja mediante claves: `Tr("common.back")`, nunca `"Volver"`
directamente.

Idioma predeterminado: **español**. Si falta una traducción:
1. Intentar idioma seleccionado.
2. Fallback a español.
3. Si tampoco existe, mostrar la clave.

Los archivos de idioma deben poder añadirse sin recompilar la lógica central
siempre que sea técnicamente viable.

## 7. Claves de traducción estables

No usar el texto en español como clave.

- ? `"Volver": "Back"`
- ? `"common.back": "Volver"`

Las claves describen intención y contexto, no el texto literal.

## 8. Design System central

Crear en el futuro un Theme/DesignSystem central para: colores, tipografía,
tamaños, espaciados, radios, tamaños de controles, estados visuales,
márgenes, iconografía básica.

No hardcodear medidas y estilos arbitrarios por pantalla cuando exista una
constante reutilizable.

## 9. Navegación centralizada

Ninguna View implementa su propio sistema de navegación global. Debe existir
un controlador/router/estado central que conozca la pantalla actual.

Las vistas solicitan: `NavigateTo(Home)`, `NavigateTo(Firmware)`, `Back()`;
no controlan directamente toda la aplicación.

## 10. Input reutilizable

Centralizar la entrada de mando. No consultar `hidKeysDown()`/
`padGetButtons()` de forma distinta en cada control si existe un sistema de
input común.

Debe ser fácil mapear: `A = aceptar`, `B = volver`, `X = acción secundaria`,
`Y = acción contextual`, y cambiar el comportamiento global si es necesario.

## 11. Nombres y responsabilidad

Cada archivo/clase tiene una responsabilidad clara. Evitar archivos gigantes
que mezclen UI + red + filesystem + parsing + lógica de actualización.

Cuando un archivo empiece a tener responsabilidades claramente diferentes,
dividirlo.

## 12. No sobrearquitectura

Estas reglas NO significan crear decenas de abstracciones innecesarias.
`NX-SWITE-Switch` debe mantenerse simple y efectivo.

No crear: factories innecesarias, interfaces sin uso real, capas vacías,
patrones complejos únicamente por "arquitectura".

Crear una abstracción solo cuando:
- evita duplicación real,
- mejora el mantenimiento,
- permite reutilización,
- separa responsabilidades claras.

## 13. Compatibilidad con devkitPro

`NX-SWITE-Switch` debe continuar usando:
- C++17
- devkitA64
- libnx
- Makefile

No introducir dependencias de .NET, WPF, MSVC, ni APIs exclusivas de Windows
sin autorización explícita.

> Nota: el resto del repositorio (`NX-Suite`, la app Windows) usa .NET 8; eso
> es un proyecto distinto y no debe mezclarse con `NX-SWITE-Switch`.

## 14. NX-SUITE (Windows) es referencia

El proyecto Windows `NX-Suite` puede leerse para comprender Gist, modelos,
lógica, procesos y comportamiento.

No modificar `NX-Suite` salvo solicitud explícita. No realizar reemplazos
globales entre `NX-Suite` y `NX-SWITE-Switch`.

## 15. Cambios pequeños y validables

Preferir implementaciones incrementales. Después de cambios importantes:
- compilar,
- verificar errores,
- indicar archivos modificados.

No implementar varias funciones grandes no relacionadas en una misma tarea
sin autorización.

## 16. Seguridad de filesystem

Cualquier operación destructiva debe ser explícita y limitada. No borrar o
mover rutas fuera del ámbito previsto.

Toda función que elimine datos debe validar: ruta, directorio base
permitido, errores, resultado. Nunca asumir que una ruta externa es segura.

> Ya implementado en `ZipExtractor`: `RecrearCarpetaVacia` solo opera sobre
> `staging/`, y la extracción rechaza componentes `..` (path traversal).

## 17. Datos remotos

El Gist/catálogos remotos deben considerarse datos no confiables. Antes de
utilizarlos:
- validar estructura,
- validar campos requeridos,
- validar URLs,
- validar rutas,
- evitar traversal `../`,
- manejar campos faltantes.

Una respuesta remota inválida no debe provocar operaciones destructivas.

## 18. Log y debug

Separar información de debug de la interfaz final. Debe existir una forma
sencilla de activar/desactivar información como: velocidad, tiempo, cantidad
de archivos, mensajes internos.

No llenar la UI final con diagnósticos.

## 19. Documentar decisiones importantes

Si se toma una decisión arquitectónica que afectará futuras funciones,
añadir una nota breve a `CLAUDE.md` o `CODEBASE_INDEX.md`.

Ejemplos: por qué el updater usa staging, por qué el self-updater es
independiente, por qué las noticias usan QR, por qué el NRO no interpreta
todavía todo el pipeline de NX-SUITE.

## 20. Actualización de estas reglas

`CLAUDE.md` es un documento vivo. Cuando el usuario establezca una nueva
regla permanente:
1. Evaluar si contradice una regla existente.
2. Advertir si técnicamente no es recomendable.
3. Proponer alternativa.
4. Actualizar `CLAUDE.md` solo después de que la regla sea aceptada.

No eliminar reglas existentes silenciosamente.
