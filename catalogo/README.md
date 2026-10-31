# Catálogo

`catalogo.legacy.json` es una copia congelada del Gist que la app consume hoy
(`ConfiguracionLocal.UrlGistPrincipal`). Sirve como referencia y punto de partida
para la automatización (recetas → catálogo generado). La app **no** lee este
archivo; sigue usando el Gist remoto.

No editar a mano. Para refrescarlo:

```powershell
Invoke-WebRequest 'https://gist.githubusercontent.com/ELCALLEJONGAMER/57949a130fdc307033492e365780a4bd/raw/NX-SWITE.json' -OutFile catalogo\catalogo.legacy.json
```
