# Catálogo

`catalogo.legacy.json` es una copia congelada del Gist que la app consume hoy
(`ConfiguracionLocal.UrlGistPrincipal`). Sirve como referencia y punto de partida
para la automatización (recetas → catálogo generado). La app **no** lee este
archivo; sigue usando el Gist remoto.

No editar a mano. Para refrescarlo:

```powershell
Invoke-WebRequest 'https://gist.githubusercontent.com/ELCALLEJONGAMER/57949a130fdc307033492e365780a4bd/raw/NX-SWITE.json' -OutFile catalogo\catalogo.legacy.json
```

## Catálogo publicado

`catalogo.json` es el catálogo que descarga la app desde `raw.githubusercontent.com` (rama `main`). El workflow `Actualizar catalogo` lo regenera a diario con `tools/NX-Suite.Catalogo` y abre un PR con el informe; al fusionarlo, los usuarios reciben los paquetes nuevos (la app revalida con ETag). Los cambios manuales (módulos nuevos, casos marcados como *Manual*) se hacen editando `catalogo.json` en un PR normal.
