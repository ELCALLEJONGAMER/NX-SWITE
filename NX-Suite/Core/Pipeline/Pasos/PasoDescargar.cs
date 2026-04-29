using System.IO;
using NX_Suite.Core;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Descarga un archivo desde una URL.
    /// Si la extensión pertenece a <see cref="ZipLogic.ExtensionesComprimidas"/>
    /// (.zip, .7z, .rar, .tar, .gz, .tgz, .bz2, .tbz2, .xz, .txz, .zst, .lz…)
    /// lo guarda en la caché de ZIPs; cualquier otro tipo va a la carpeta de
    /// extracción directamente. Si el archivo ya existe localmente, se omite.
    ///
    /// Parámetros JSON:
    ///   Url             : URL completa
    ///   ArchivoDestino  : nombre de archivo local (con extensión)
    /// </summary>
    public class PasoDescargar : IPasoPipeline
    {
        public string TipoAccion => "DESCARGAR";

        public async Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            string url            = parametros.GetProperty("Url").GetString()!;
            string archivoDestino = parametros.GetProperty("ArchivoDestino").GetString()!;

            string ext        = Path.GetExtension(archivoDestino).ToLowerInvariant();
            bool esComprimido = ZipLogic.ExtensionesComprimidas.Contains(ext);

            string rutaDestino = esComprimido
                ? Path.Combine(ctx.RutaCacheZips, archivoDestino)
                : Path.Combine(ctx.RutaCacheExtraccion, archivoDestino);

            if (!File.Exists(rutaDestino))
                await ctx.MotorDescarga.DescargarArchivoAsync(url, rutaDestino, ctx.Progreso, ct);
        }
    }
}
