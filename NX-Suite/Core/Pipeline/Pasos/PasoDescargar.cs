using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Descarga un archivo desde una URL.
    /// Si es un comprimido (.zip/.7z/.rar/.gz/.tar/.bz2/.xz) lo guarda en la
    /// caché de ZIPs; cualquier otro archivo va directo a la carpeta de extracción.
    /// Si el archivo ya existe localmente, no se vuelve a descargar.
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

            string ext = Path.GetExtension(archivoDestino).ToLowerInvariant();
            bool esComprimido = ext is ".zip" or ".7z" or ".rar" or ".gz" or ".tar" or ".bz2" or ".xz";

            string rutaDestino = esComprimido
                ? Path.Combine(ctx.RutaCacheZips, archivoDestino)
                : Path.Combine(ctx.RutaCacheExtraccion, archivoDestino);

            if (!File.Exists(rutaDestino))
                await ctx.MotorDescarga.DescargarArchivoAsync(url, rutaDestino, ctx.Progreso, ct);
        }
    }
}
