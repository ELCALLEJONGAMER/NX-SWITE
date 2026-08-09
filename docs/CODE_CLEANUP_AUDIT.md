# NX-Suite — Auditoría de Código Muerto, Duplicaciones y Residuos

> **Documento de solo lectura.** Ningún archivo de producción fue modificado
> para producir este informe. Es un inventario de candidatos, no una
> ejecución de limpieza. Ver reglas de trabajo en `docs/MAINWINDOW_MIGRATION_PLAN.md`.

Fecha: 2026 — rama `feat(optimizacion_de_codigo)`.

---

## 1. Resumen ejecutivo

La auditoría se centró en tres frentes:

1. **Archivos vacíos / huérfanos** en `Hardware/` — 4 archivos `.cs` de
   0 bytes que son residuos de un renombrado histórico (de nombres en
   inglés a nombres en español). Los archivos españoles equivalentes
   (`ParticionadorDiscos.cs`, `NotificadorDiscos.cs`, `EscanerDiscos.cs`,
   `CazadorVentanas.cs`) sí contienen el código real y están en uso.
   **Candidato A — seguro para eliminar.**
2. **Duplicación aparente de converters de color hex** (`HexToBrushConverter`
   en la raíz vs `HexToNeonBrushConverter` en `UI/Converters/`) — **NO son
   duplicados**, ambos están en uso activo desde distintos XAML con
   propósitos ligeramente distintos (uno sólido, otro degradado neon
   rotatorio animable). Clasificado **C** solo a efectos de documentar la
   posible confusión de nombres para agentes IA, pero **no se recomienda
   consolidar** — el riesgo de romper el binding no-Freeze del segundo es real.
3. **Mojibake generalizado** (`?`) en comentarios y, en menor medida, en
   textos de UI, presente en prácticamente todos los archivos `.cs` del
   proyecto (~54 archivos, más de 1000 ocurrencias). Es un problema de
   codificación de guardado (probablemente archivos guardados como
   ANSI/Windows-1252 en algún punto del historial en lugar de UTF-8),
   no un bug funcional. Se documenta pero **no se corrige** en esta
   auditoría (se pospone a la preparación de multiidioma, según las
   instrucciones).

Ningún otro candidato de "código muerto" con alta confianza fue
encontrado en `Core/`, `Models/`, `UI/Controles/` o `Core/Pipeline/Pasos/`.
El proyecto está, en términos generales, notablemente limpio tras las
FASES 1-3 de extracción de `MainWindow`. La mayoría de clases que a
primera vista parecen "solitarias" (ViewModels de `VistaAsistida`,
converters, modelos de configuración remota) tienen consumidores reales
en XAML, en `RegistroPasos`, o en deserialización del Gist.

---

## 2. Método utilizado

1. Lectura de `CODEBASE_INDEX.md` como mapa de referencia (no se leyó
   ningún archivo grande sin antes confirmar que era necesario).
2. `get_files_in_project` sobre `NX-Suite.csproj` para obtener el
   inventario completo de archivos compilados (excluyendo `obj/`/`bin/`
   generados, que igualmente aparecieron y fueron descartados).
3. Búsquedas dirigidas por símbolo (`code_search` y `Select-String`
   vía PowerShell) para cada clase/converter/enum sospechoso, buscando
   consumidores en `.cs` **y** `.xaml` (bindings, `StaticResource`,
   `x:Key`, `xmlns`).
4. Verificación de tamaño de archivo (`Get-ChildItem ... Length`) para
   confirmar archivos vacíos antes de clasificarlos como A.
5. Verificación cruzada de `RegistroPasos.cs` contra el listado de
   `Core/Pipeline/Pasos/*.cs` para confirmar que **todos** los pasos
   declarados están registrados (ninguno huérfano).
6. Búsqueda de `TODO|FIXME|HACK` en todo el árbol `.cs`.
7. Búsqueda de mojibake (`\ufffd`, el carácter de reemplazo Unicode)
   en todo el árbol `.cs`/`.xaml` para la sección de anomalías de
   codificación.

No se leyeron íntegramente archivos grandes como `ParticionadorDiscos.cs`
(811 líneas) o `MainWindow.Detalle.cs` salvo fragmentos puntuales
necesarios para confirmar un hallazgo.

---

## 3. Alcance inspeccionado (aprox.)

- **Archivos en el proyecto principal (`NX-Suite.csproj`):** ~180 archivos `.cs` de producción (excluyendo `obj/`).
- **Símbolos/clases verificados individualmente:** ~45 (converters, ViewModels de `VistaAsistida`, enums de `Models/Catalogo` y `Models/Queue`, clases de `Hardware/`, `GestorActualizacion`, `FirmaDeteccion`, pasos de pipeline).
- **Archivos XAML cruzados para verificar consumo dinámico:** `VistaAsistida.xaml`, `EstilosBotones.xaml`, `ColoresGlobales.xaml`, `VentanaDependencias.xaml`.
- **Pipeline:** 17/17 pasos de `Core/Pipeline/Pasos/` confirmados registrados en `RegistroPasos.cs`.

---

## 4. A — Seguro para eliminar

> **CLEANUP 1 — COMPLETADO.** Los 5 archivos listados abajo fueron eliminados.
> Build antes: OK. Build después: OK. `CODEBASE_INDEX.md` actualizado
> (eliminada la fila de `Native/DiskNative.cs` en la tabla de `Hardware`).

| Archivo | Evidencia | Estado |
|---|---|---|
| `NX-Suite/Hardware/DiskPartitioner.cs` | 0 bytes. Reemplazado por `Hardware/ParticionadorDiscos.cs` (38.872 bytes, en uso activo). Ningún `namespace`/clase definida — no puede tener consumidores. | **Eliminado** |
| `NX-Suite/Hardware/DiskNotifications.cs` | 0 bytes. Reemplazado por `Hardware/NotificadorDiscos.cs` (1.929 bytes, en uso activo). | **Eliminado** |
| `NX-Suite/Hardware/DiskScanner.cs` | 0 bytes. Reemplazado por `Hardware/EscanerDiscos.cs` (2.491 bytes, en uso activo). | **Eliminado** |
| `NX-Suite/Hardware/WindowSniper.cs` | 0 bytes. Reemplazado por `Hardware/CazadorVentanas.cs` (4.258 bytes, en uso activo). | **Eliminado** |
| `NX-Suite/Hardware/Native/DiskNative.cs` | 0 bytes. Reemplazado por `Hardware/Native/DiscoNativo.cs` (26.221 bytes, en uso activo). | **Eliminado** |

**Nota:** estos 5 archivos vacíos siguen incluidos en el `.csproj` (SDK-style
`*.cs` glob), por lo que no rompen la compilación, pero:
- Aparecen en cualquier búsqueda de agente IA por nombre en inglés
  ("DiskPartitioner", "DiskScanner", etc.), generando falsos positivos
  y confusión sobre cuál es el archivo "real".
- No tienen ningún contenido, código, comentario ni referencia — cero
  riesgo de romper nada al eliminarlos.

Este es el conjunto de mayor confianza de toda la auditoría.

---

## 5. B — Probablemente muerto

No se encontraron candidatos de clase/método completo con **cero**
consumidores directos ni dinámicos que no fueran ya explicados por (A).
Es decir: no hay ningún hallazgo B en esta pasada.

Esto es coherente con que el proyecto ya pasó por 3 fases de limpieza
progresiva (FASE 1-3 del plan de migración de `MainWindow`) antes de
esta auditoría.

---

## 6. C — Duplicado / consolidable

### C.1 — Converters de color hex

**Implementación A:** `NX-Suite/HexToBrushConverter.cs` (namespace raíz `NX_Swite`)
- Convierte un hex a `SolidColorBrush` simple.
- Registrado como `x:Key="ColorNeonConverter"` en `ColoresGlobales.xaml`.
- Consumido en `VentanaDependencias.xaml` (líneas 227, 246).

**Implementación B:** `NX-Suite/UI/Converters/HexToNeonBrushConverter.cs` (namespace `NX_Swite.UI.Converters`)
- Convierte un hex a un `LinearGradientBrush` de 3 paradas con mezcla
  a púrpura, sin `Freeze()`, para permitir animar `RelativeTransform`.
- Registrado como `x:Key="NeonMundoConverter"` en `EstilosBotones.xaml`.
- Consumido en `VistaAsistida.xaml` (4 usos) y en `EstilosBotones.xaml`
  (`DataTrigger`s de hover, 2 usos).

**Responsabilidad compartida:** ambos toman un string hex del Gist
(`ColorNeon`) y producen un `Brush` para bordes/rellenos neon.

**Diferencias reales:** el primero es un brush sólido estático
(pensado para texto/relleno simple); el segundo es un gradiente
animable pensado específicamente para el efecto "halo rotatorio" de
`PincelNeonRotatorio`. No son intercambiables sin perder la animación
de rotación en el segundo caso.

**Fuente de verdad recomendada:** ninguna — ambos son necesarios. Se
podría, como mucho, renombrar `HexToBrushConverter` (raíz, sin
namespace de proyecto claro) a `UI/Converters/HexToSolidBrushConverter.cs`
por consistencia de carpetas, pero eso es un moving/renaming fuera de
alcance de esta auditoría.

**Riesgo de consolidación:** ALTO (romper animación de rotación).
**Impacto en contexto IA:** MEDIO — el nombre similar y la ubicación
distinta (uno en la raíz del proyecto, sin carpeta `UI/Converters/`)
puede hacer que un agente asuma que es un duplicado real y proponga
fusionarlos incorrectamente.

### C.2 — `ISuiteController` / `SuiteController` / `SuiteControllerFacade`

Ya documentado explícitamente en `CODEBASE_INDEX.md` como patrón
intencional (fachada/decorador para tests). **No se considera
duplicación real** — se menciona aquí solo porque es el tipo de patrón
que un escaneo automático de "misma interfaz implementada dos veces"
suele marcar como falso positivo. Clasificación: **D (legacy necesario)**,
no C.

---

## 7. D — Legacy pero necesario (relevante)

| Elemento | Por qué parece legacy | Por qué se mantiene |
|---|---|---|
| `SuiteControllerFacade` | Misma interfaz que `SuiteController`, parece redundante | Documentado como decorador explícito para tests/throttling (`CODEBASE_INDEX.md`) |
| Inyección local de mundo `cfw` en `MainWindow.xaml.cs` (líneas ~229-243) | `TODO(Fase 3 - CFW)` marcado para eliminar | Sigue siendo necesario hasta que el Gist remoto declare `Tipo: "cfw_hub"` (Fase 3 de la migración de mundos, aún pendiente según `CODEBASE_INDEX.md`) |
| Comentario histórico "Fase 2 legacy" en `CODEBASE_INDEX.md` sobre `HekateSeccionCardTemplateSelector`/`SlotTemplateSelector` | Etiquetado como "legacy" en el propio índice | Ambos selectores siguen activos y consumidos por `VistaAsistida.xaml` — el rótulo "legacy" en el índice se refiere solo a que la documentación de esa sección es antigua, no a que el código esté muerto |

---

## 8. E — Código dinámico que NO debe eliminarse

| Elemento | Mecanismo dinámico |
|---|---|
| Los 17 `IPasoPipeline` en `Core/Pipeline/Pasos/` | Resueltos por `TipoAccion` (string) desde el JSON del Gist vía `RegistroPasos`/`ReglasLogic`. Confirmado 17/17 registrados — ninguno huérfano, pero **ninguno debe evaluarse por referencias C# directas**, ya que su único "consumidor" real es la cadena `TipoAccion` en el pipeline JSON remoto. |
| `HexToBrushConverter`, `HexToNeonBrushConverter`, `ConversorIconoCache`, `ContainsStringConverter` | Consumidos exclusivamente vía `x:Key`/`StaticResource` en XAML — invisibles a un grep de solo-C#. |
| `CategoriaModelo`, todas las VM de `UI/Controles/VistaAsistida/` | Consumidas vía bindings/`DataTemplateSelector` en `VistaAsistida.xaml` y código detrás. |
| `FirmaDeteccion` / `ArchivoCritico` (`Models/Catalogo/FirmaDeteccion.cs`) | Poblado por deserialización del JSON del Gist (`ModuloConfig.FirmasDeteccion`), consumido en `SuiteController.cs` y `MainWindow.Detalle.cs`. |
| `MundoMenuConfig.SubMundosIds`, `Tipo = "cfw_hub"` | Config dinámica del Gist — ver sección de migración CFW en `CODEBASE_INDEX.md`. |
| `ToolsConfig` / `ConfiguracionRemota.Tools` | Mapeado desde la sección raíz `"tools"` del Gist, no desde `ConfiguracionUI`. |
| `TarjetaHubCfwConfig` (mencionado en `CODEBASE_INDEX.md`, sección "Hub CFW — tarjetas e imágenes remotas") | Nueva sección `tarjetasHubCfw` del Gist, aún no publicada en producción — no confundir con código muerto. |

---

## 9. TODOs y fallbacks relevantes

| Ubicación | Texto | Clasificación |
|---|---|---|
| `MainWindow.xaml.cs` ~L229 | `TODO(Fase 3 - CFW): quitar esta inyección temporal en cuanto el mundo "cfw" se dé de alta en el Gist remoto` | **ACTIVO / NECESARIO** — depende de un cambio en el Gist que el propio `CODEBASE_INDEX.md` marca como "Fase 3 — Pendiente". No eliminar ni el TODO ni el fallback. |
| `MainWindow.Navegacion.cs` ~L523 | Comentario referenciando "TODO en CODEBASE_INDEX.md" sobre la tarjeta "Herramientas" placeholder del hub CFW | **ACTIVO / NECESARIO** — la tarjeta Herramientas es un placeholder documentado a propósito, pendiente de decisión de producto. |
| `GestorHerramientaNxNandManager.cs` (nota de mantenimiento en `CODEBASE_INDEX.md`) | Advertencia sobre caché de DLLs vecinas no invalidándose si cambian sin bump de versión | **ACTIVO / NECESARIO** — es una advertencia operativa, no código muerto. |

No se encontraron TODOs obsoletos con alta confianza.

---

## 10. Anomalías de codificación de texto

Se detectó **mojibake generalizado** (carácter de reemplazo Unicode
`?`) en aproximadamente **54 archivos `.cs`** y más de **1000
ocurrencias** en todo `NX-Suite/`. Ejemplos representativos:

| Archivo | Texto encontrado | Texto esperado | Posible origen |
|---|---|---|---|
| `UI/Dialogos.cs` | `di?logos`, `t?tulos`, `Informaci?n`, `S?/No` | `diálogos`, `títulos`, `Información`, `Sí/No` | Guardado con codificación ANSI/Windows-1252 en algún commit histórico en lugar de UTF-8 |
| `MainWindow.Detalle.cs` | `m?dulo`, `Versi?n`, `CACH?`, `Posici?n`, `Bot?n "VER M?S"` | `módulo`, `Versión`, `CACHÉ`, `Posición`, `Botón "VER MÁS"` | Igual que arriba |
| `Models/NxNandManager/EstadoFirmwareEmummc.cs` | (11 ocurrencias, no inspeccionado carácter a carácter) | — | Igual que arriba |
| `UI/Controles/VistaAsistida.xaml.cs` | (36 ocurrencias) | — | Igual que arriba |
| `MainWindow.DependenciasOverlay.cs` | (32 ocurrencias) | — | Igual que arriba |
| `MainWindow.AsistidoCompleto.cs` | (24 ocurrencias) | — | Igual que arriba |

Este problema afecta principalmente a **comentarios de documentación
XML** (`///`) y, en menor medida, a algunos strings de texto de UI
visibles al usuario (p. ej. `"CACH? LOCAL"`, `"Versi?n: --"`). No se
detectaron errores de compilación asociados (el compilador C# tolera
el carácter de reemplazo dentro de comentarios y strings literales).

**No se corrigió nada** — se deja explícitamente para la futura
preparación del sistema multiidioma, tal como se indicó. Dado el
volumen (54 archivos), se recomienda tratarlo como una tarea dedicada
separada (posiblemente automatizable con una pasada de
re-codificación + revisión manual dirigida), no como parte de un
"CLEANUP" de código muerto.

---

## 11. Ruido principal para agentes IA

| Elemento | Impacto | Motivo |
|---|---|---|
| 5 archivos vacíos en `Hardware/` con nombres en inglés (`DiskPartitioner.cs`, `DiskNotifications.cs`, `DiskScanner.cs`, `WindowSniper.cs`, `Native/DiskNative.cs`) | **ALTO** | Nombres muy parecidos a sus equivalentes reales en español (`ParticionadorDiscos.cs`, `NotificadorDiscos.cs`, `EscanerDiscos.cs`, `CazadorVentanas.cs`, `Native/DiscoNativo.cs`). Cualquier búsqueda por concepto en inglés ("disk scanner", "window sniper") los devuelve como resultado vacío pero legítimo en apariencia, obligando al agente a investigar dos veces cuál es el archivo correcto. |
| Mojibake en comentarios `///` de casi todo el proyecto | **MEDIO** | No impide entender el código, pero degrada la calidad de la documentación XML que un agente usaría para entender rápidamente la intención de una clase/método sin leer el cuerpo completo. |
| `HexToBrushConverter` (raíz, sin carpeta) vs `HexToNeonBrushConverter` (en `UI/Converters/`) | **MEDIO** | Nombres casi idénticos, distinta carpeta, distinto propósito real (sólido vs gradiente animado) — alto riesgo de que un agente proponga "consolidar" y rompa la animación de rotación del halo neon. |
| Referencias cruzadas a "Fase 2 legacy" y "Fase 3 pendiente" dentro de `CODEBASE_INDEX.md` (migración CFW) | **BAJO** | Bien documentado y explícito, pero requiere que el agente lea con cuidado para no confundir "legacy" (etiqueta de sección antigua del índice) con "código muerto". |
| `SuiteControllerFacade` como aparente duplicado de `SuiteController` | **BAJO** | Ya está bien documentado en `CODEBASE_INDEX.md` como patrón intencional; solo genera ruido si el agente no lee esa nota antes de buscar duplicados de interfaz. |

---

## 12. Orden recomendado de CLEANUP (solo propuesta, no ejecutar aún)

**CLEANUP 1** — Eliminar los 5 archivos vacíos de `Hardware/`
- Riesgo: **BAJO** (archivos de 0 bytes, sin contenido, sin referencias).
- Acción: `remove_file` sobre los 5 archivos.
- Build antes/después.
- Prueba manual: ninguna funcional requerida (no hay código que
  ejecutar), solo confirmar que el build sigue limpio y que
  `Hardware/` ya no contiene los nombres en inglés duplicados.
- Actualizar `CODEBASE_INDEX.md` (tabla de `Hardware/`) para reflejar
  que esos archivos ya no existen.

**CLEANUP 2** (futuro, requiere decisión de producto, no solo técnica)
- Evaluar si renombrar `HexToBrushConverter.cs` (raíz) a
  `UI/Converters/HexToSolidBrushConverter.cs` por consistencia de
  carpetas — **NO es limpieza de código muerto**, es reorganización.
  Riesgo MEDIO (requiere actualizar `xmlns:local`/`ColoresGlobales.xaml`).
  Se menciona aquí solo como nota, no como parte de esta auditoría.

**CLEANUP 3** (futuro, fuera de alcance de esta auditoría)
- Corrección de mojibake en los ~54 archivos afectados, idealmente
  junto con la preparación del sistema multiidioma (según indicación
  explícita del usuario de no mezclar ambas tareas ahora).

No se proponen más fases de limpieza de código muerto: no se
encontraron más candidatos A o B en esta pasada.

---

## 13. Riesgos

- **CLEANUP 1** tiene riesgo prácticamente nulo: los archivos están
  vacíos y no participan en la compilación de ninguna manera más allá
  de ser incluidos vacíamente por el glob del SDK-style `.csproj`.
- Cualquier intento de "consolidar" converters (C.1) sin pruebas
  visuales manuales tiene riesgo **ALTO** de romper la animación de
  hover/halo neon en `VistaAsistida` y en los botones premium del hub.
- La corrección de mojibake, si se aborda en el futuro, debe hacerse
  con cuidado de no alterar accidentalmente literales usados en
  comparaciones de string (p. ej. claves de diccionario, aunque no se
  detectó ningún caso de eso en esta pasada — el mojibake encontrado
  fue mayormente en comentarios y textos de UI, no en claves lógicas).

---

## 14. Criterio para futuras eliminaciones

1. **Confianza > cantidad.** Solo eliminar cuando exista evidencia
   cruzada en C#, XAML y configuración dinámica (Gist/Pipeline) de que
   no hay consumidor.
2. Todo elemento bajo `Core/Pipeline/Pasos/` se considera **E** por
   defecto salvo que se confirme explícitamente que no está en
   `RegistroPasos.cs` **y** que ningún módulo del Gist lo referencia
   por `TipoAccion`.
3. Todo converter/estilo/recurso XAML debe verificarse contra **todos**
   los `.xaml` del proyecto (no solo el más obvio) antes de marcarlo
   como A o B.
4. Los archivos de 0 bytes son el único tipo de hallazgo que puede
   clasificarse como A sin necesidad de búsqueda de referencias (no
   pueden tener consumidores porque no tienen contenido).
5. Cada fase de limpieza futura debe seguir el proceso ya establecido:
   build antes ? cambio aprobado ? build después ? prueba manual ?
   actualizar `CODEBASE_INDEX.md` ? commit independiente ? detenerse.

---

## Higiene de raíz del repositorio

Mini-auditoría solicitada aparte, limitada exclusivamente a 6 archivos
sueltos en la raíz del repositorio (no se investigó el resto del árbol).
Elementos conocidos y explícitamente excluidos de este análisis:
`.publish-beta.ps1.txt`, `dist/`, `publish-beta.ps1` (intencionales,
no tocar).

| Archivo | Tamaño | Tracked (Git) | `.gitignore` | Referencias encontradas | Clasificación |
|---|---|---|---|---|---|
| `scan_template.ps1` | 0 bytes | Sí | No ignorado | Ninguna (scripts, docs, proyectos, GitHub Actions) | B — residuo seguro |
| `scan2.ps1` | 0 bytes | Sí | No ignorado | Ninguna | B — residuo seguro |
| `scan3.ps1` | 0 bytes | Sí | No ignorado | Ninguna | B — residuo seguro |
| `scan4.ps1` | 0 bytes | Sí | No ignorado | Ninguna | B — residuo seguro |
| `temp_restore.txt` | 0 bytes | Sí | No ignorado | Ninguna | B — residuo seguro |
| `write_xaml.py` | 0 bytes | Sí | No ignorado | Ninguna | B — residuo seguro |

**Método usado:** `git ls-files`, `git check-ignore -v`, `git status --porcelain`
y `git grep -i` (búsqueda del nombre de cada archivo en todo el árbol
versionado, excluyendo los propios archivos) — sin resultados en
ningún caso. Adicionalmente se consultó `git log --follow` de cada
archivo: todos aparecen introducidos vacíos en commits históricos de
renombrado/reestructuración general del proyecto (p.ej. "Cambios de VS",
"Se cambia el nombre general NX-SWITE"), no en commits que sugieran una
herramienta activa o un script todavía en uso.

**Conclusión:** los 6 archivos están vacíos (0 bytes), versionados por
Git, no excluidos por `.gitignore`, y no tienen ninguna referencia
directa desde scripts, documentación, proyectos/solución ni GitHub
Actions. Por nombre y contexto histórico parecen residuos de scripts de
escaneo (`scan*.ps1`), una nota de restauración temporal
(`temp_restore.txt`) y una utilidad puntual de generación de XAML
(`write_xaml.py`), probablemente creados por un agente de IA o durante
una sesión de trabajo puntual y nunca completados ni utilizados.

**Recomendación — ROOT CLEANUP 1:** los 6 archivos son candidatos
seguros para eliminación (clasificación B en todos los casos).

**? ROOT CLEANUP 1 — COMPLETADO.** Aprobado y ejecutado por el usuario.
Se eliminaron exclusivamente `scan_template.ps1`, `scan2.ps1`,
`scan3.ps1`, `scan4.ps1`, `temp_restore.txt` y `write_xaml.py`. Ninguno
de los 6 aparecía documentado en `CODEBASE_INDEX.md`, por lo que no
requirió actualización. Build limpio verificado antes y después de la
eliminación. Ningún otro archivo de producción, `.gitignore`, `dist/`,
`.publish-beta.ps1.txt`, `publish-beta.ps1` ni `NX-SWITE-Switch` fue
tocado.
