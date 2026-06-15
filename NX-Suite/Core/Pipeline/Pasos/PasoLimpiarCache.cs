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
            }

            if (parametros.TryGetProperty("CarpetaTemp", out var dirProp))
            {
                string d    = Path.Combine(ctx.RutaCacheExtraccion, dirProp.GetString()!);
                string dVer = d + ".version";
                if (Directory.Exists(d)) Directory.Delete(d, true);
                if (File.Exists(dVer))   File.Delete(dVer);
            }

            return Task.CompletedTask;
        }
    }
}
