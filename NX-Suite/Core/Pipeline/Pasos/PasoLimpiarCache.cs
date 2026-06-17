using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Limpia entradas de las cachés locales de la app (ZIPs descargados y/o
    /// carpetas extraídas). Ambos parámetros son opcionales.
    ///
    /// Parámetros JSON:
    ///   ArchivoZip  : nombre del .zip a borrar de la caché de ZIPs (opcional)
    ///   CarpetaTemp : nombre de la subcarpeta a borrar de la caché de extracción (opcional)
    /// </summary>
    public class PasoLimpiarCache : IPasoPipeline
    {
        public string TipoAccion => "LIMPIAR_CACHE";

        public Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            if (parametros.TryGetProperty("ArchivoZip", out var zipProp))
            {
                string z    = Path.Combine(ctx.RutaCacheZips, zipProp.GetString()!);
                string zVer = z + ".version";
                if (File.Exists(z))    File.Delete(z);
                if (File.Exists(zVer)) File.Delete(zVer);
                // Nota: el sidecar .destino vive en RutaCacheExtraccion, no aquí.
                // Se borra junto con la carpeta extraída (CarpetaTemp), no con el ZIP.
            }

            if (parametros.TryGetProperty("CarpetaTemp", out var dirProp))
            {
                string d     = Path.Combine(ctx.RutaCacheExtraccion, dirProp.GetString()!);
                string dVer  = d + ".version";
                if (Directory.Exists(d)) Directory.Delete(d, true);
                if (File.Exists(dVer))   File.Delete(dVer);

                // Borrar también el sidecar .destino asociado al ZIP de este módulo.
                // Buscamos todos los *.destino en RutaCacheExtraccion cuyo contenido
                // coincida con el nombre de esta carpeta.
                string nombreCarpeta = dirProp.GetString()!;
                foreach (var sidecar in Directory.EnumerateFiles(ctx.RutaCacheExtraccion, "*.destino"))
                {
                    try
                    {
                        if (string.Equals(File.ReadAllText(sidecar).Trim(), nombreCarpeta, StringComparison.OrdinalIgnoreCase))
                            File.Delete(sidecar);
                    }
                    catch { }
                }
            }

            return Task.CompletedTask;
        }
    }
}
