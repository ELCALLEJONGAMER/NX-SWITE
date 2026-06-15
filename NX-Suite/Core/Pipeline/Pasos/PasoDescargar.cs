using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NX_Suite.Models;
using NX_Suite.Services;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Descarga un archivo desde una URL.
    /// Si la extensión pertenece a <see cref="ZipLogic.ExtensionesComprimidas"/>
    /// lo guarda en la caché de ZIPs; cualquier otro tipo va a la carpeta de
    /// extracción directamente.
    ///
    /// Valida que el archivo en caché corresponda a <see cref="ContextoPipeline.VersionModulo"/>
    /// mediante un archivo sidecar <c>&lt;archivo&gt;.version</c>. Si la versión no
    /// coincide, se borra el archivo obsoleto y se redescarga.
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

            string rutaSidecar = rutaDestino + ".version";

            // Invalidar caché si el archivo existe pero pertenece a otra versión
            if (File.Exists(rutaDestino) && !string.IsNullOrEmpty(ctx.VersionModulo))
            {
                string versionCacheada = File.Exists(rutaSidecar)
                    ? (await File.ReadAllTextAsync(rutaSidecar, ct)).Trim()
                    : string.Empty;

                if (!string.Equals(versionCacheada, ctx.VersionModulo, System.StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(rutaDestino);
                    if (File.Exists(rutaSidecar)) File.Delete(rutaSidecar);
                }
            }

            if (!File.Exists(rutaDestino))
            {
                // El ZIP puede haber sido limpiado por LIMPIAR_CACHE pero la carpeta extraída
                // podría seguir vigente. Si existe y tiene archivos, no es necesario re-descargar.
                string carpetaExtraida =
                    parametros.TryGetProperty("CarpetaExtraida", out var ceProp) && ceProp.GetString() is { } ce
                        ? ce
                        : Path.GetFileNameWithoutExtension(archivoDestino);

                if (!string.IsNullOrEmpty(carpetaExtraida))
                {
                    string rutaCarpeta        = Path.Combine(ctx.RutaCacheExtraccion, carpetaExtraida);
                    string rutaSidecarCarpeta = rutaCarpeta + ".version";

                    bool carpetaConArchivos = Directory.Exists(rutaCarpeta)
                        && Directory.GetFiles(rutaCarpeta, "*.*", SearchOption.AllDirectories).Length > 0;

                    // La carpeta es obsoleta solo si existe sidecar con versión diferente
                    bool carpetaObsoleta = carpetaConArchivos
                        && !string.IsNullOrEmpty(ctx.VersionModulo)
                        && File.Exists(rutaSidecarCarpeta)
                        && !string.Equals(
                            (await File.ReadAllTextAsync(rutaSidecarCarpeta, ct)).Trim(),
                            ctx.VersionModulo,
                            StringComparison.OrdinalIgnoreCase);

                    if (carpetaConArchivos && !carpetaObsoleta)
                    {
                        Logger.DescargaOmitida(archivoDestino, rutaCarpeta);
                        ctx.Progreso?.Report(new EstadoProgreso
                        {
                            Porcentaje  = 100,
                            TareaActual = $"En caché: {archivoDestino}",
                        });
                        return;
                    }
                }

                Logger.DescargaIniciada(archivoDestino, url);
                try
                {
                    await ctx.MotorDescarga.DescargarArchivoAsync(url, rutaDestino, ctx.Progreso, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.DescargaFallida(archivoDestino, url, ex);
                    // Eliminar archivo parcial si quedó en disco
                    try { if (File.Exists(rutaDestino)) File.Delete(rutaDestino); } catch { }
                    throw new InvalidOperationException(
                        $"No se pudo descargar '{archivoDestino}'.\nURL: {url}\nDetalle: {ex.Message}", ex);
                }

                long tamano = new FileInfo(rutaDestino).Length;
                Logger.DescargaCompletada(archivoDestino, tamano);

                // Escribir sidecar de versión tras descarga exitosa
                if (!string.IsNullOrEmpty(ctx.VersionModulo))
                    await File.WriteAllTextAsync(rutaSidecar, ctx.VersionModulo, ct);
            }
            else
            {
                Logger.DescargaOmitida(archivoDestino, rutaDestino);
                ctx.Progreso?.Report(new EstadoProgreso
                {
                    Porcentaje  = 100,
                    TareaActual = $"En caché: {archivoDestino}",
                });
            }
        }
    }
}
