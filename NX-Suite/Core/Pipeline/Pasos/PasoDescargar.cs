using System.IO;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NX_Swite.Models;
using NX_Swite.Services;

namespace NX_Swite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Descarga un archivo desde una URL.
    /// Si la extensi�n pertenece a <see cref="ZipLogic.ExtensionesComprimidas"/>
    /// lo guarda en la cach� de ZIPs; cualquier otro tipo va a la carpeta de
    /// extracci�n directamente.
    ///
    /// Valida que el archivo en cach� corresponda a <see cref="ContextoPipeline.VersionModulo"/>
    /// mediante un archivo sidecar <c>&lt;archivo&gt;.version</c>. Si la versi�n no
    /// coincide, se borra el archivo obsoleto y se redescarga.
    ///
    /// Validaci�n h�brida de hash (GitHub �nicamente, no forzosa):
    ///   Si el contexto contiene un <see cref="GitHubAssetValidator"/> y la URL pertenece
    ///   a GitHub, se consulta el digest SHA256 del asset remoto. Si los hashes difieren
    ///   se invalida la cach� y se redescarga. Si no hay internet, token inv�lido o la API
    ///   no expone el digest, se contin�a desde cach� sin interrumpir la instalaci�n.
    ///   URLs fuera de GitHub y m�dulos sin paso DESCARGAR no activan esta l�gica.
    ///
    /// Par�metros JSON:
    ///   Url             : URL completa
    ///   ArchivoDestino  : nombre de archivo local (con extensi�n)
    ///   CarpetaExtraida : (opcional) nombre de la carpeta extra�da
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

            // Invalidar cach� si el archivo existe pero pertenece a otra versi�n
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

            // ?? Validaci�n h�brida de hash (GitHub, no forzosa) ??????????
            // Solo act�a si: el archivo sigue en cach� + URL es de GitHub + hay validador.
            // Si no hay red, token o digest disponible ? ResultadoValidacion.NoDisponible
            // ? se contin�a desde cach� sin interrumpir la instalaci�n.
            if (File.Exists(rutaDestino) && ctx.ValidadorAsset != null && GitHubAssetValidator.EsUrlGitHub(url))
            {
                try
                {
                    var resultadoHash = await ctx.ValidadorAsset.ValidarAsync(url, rutaDestino, ct);
                    if (resultadoHash == ResultadoValidacion.Desactualizado)
                    {
                        Logger.Info($"[Hash] Cache desactualizada para '{archivoDestino}' (hash remoto distinto). Se redescargar�.");
                        File.Delete(rutaDestino);
                        if (File.Exists(rutaSidecar)) File.Delete(rutaSidecar);
                    }
                    // Valido        ? cach� OK, no tocar.
                    // NoDisponible  ? sin red/token/digest: continuar desde cach�.
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exHash)
                {
                    Logger.Warning($"[Hash] Validaci�n omitida para '{archivoDestino}': {exHash.Message}");
                }
            }

            if (!File.Exists(rutaDestino))
            {
                // Prioridad para encontrar la carpeta extra�da:
                // 1. Sidecar .destino escrito por PasoExtraer (vive en RutaCacheExtraccion,
                //    sobrevive a la eliminaci�n del ZIP por LIMPIAR_CACHE)
                // 2. Par�metro opcional CarpetaExtraida del JSON
                // 3. Nombre deducido del ZIP (fallback, puede no coincidir)
                string rutaSidecarDestino = Path.Combine(ctx.RutaCacheExtraccion, archivoDestino + ".destino");
                string carpetaExtraida;

                if (File.Exists(rutaSidecarDestino))
                {
                    carpetaExtraida = (await File.ReadAllTextAsync(rutaSidecarDestino, ct)).Trim();
                }
                else if (parametros.TryGetProperty("CarpetaExtraida", out var ceProp) && ceProp.GetString() is { } ce)
                {
                    carpetaExtraida = ce;
                }
                else
                {
                    carpetaExtraida = Path.GetFileNameWithoutExtension(archivoDestino);
                }

                if (!string.IsNullOrEmpty(carpetaExtraida))
                {
                    string rutaCarpeta        = Path.Combine(ctx.RutaCacheExtraccion, carpetaExtraida);
                    string rutaSidecarCarpeta = rutaCarpeta + ".version";

                    bool carpetaConArchivos = Directory.Exists(rutaCarpeta)
                        && Directory.GetFiles(rutaCarpeta, "*.*", SearchOption.AllDirectories).Length > 0;

                    // La carpeta es obsoleta solo si existe sidecar con versi�n diferente
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
                            TareaActual = $"En cach�: {archivoDestino}",
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
                    // Eliminar archivo parcial si qued� en disco
                    try { if (File.Exists(rutaDestino)) File.Delete(rutaDestino); } catch { }
                    throw new InvalidOperationException(
                        $"No se pudo descargar '{archivoDestino}'.\nURL: {url}\nDetalle: {ex.Message}", ex);
                }

                long tamano = new FileInfo(rutaDestino).Length;
                Logger.DescargaCompletada(archivoDestino, tamano);

                // Registrar que este ZIP fue descargado en esta sesi�n.
                // PasoExtraer lo consulta para forzar re-extracci�n aunque
                // el sidecar de versi�n coincida (mismo tag, contenido distinto).
                ctx.ZipsDescargadosEnEstaSesion.Add(archivoDestino);

                // Escribir sidecar de versi�n tras descarga exitosa
                if (!string.IsNullOrEmpty(ctx.VersionModulo))
                    await File.WriteAllTextAsync(rutaSidecar, ctx.VersionModulo, ct);
            }
            else
            {
                Logger.DescargaOmitida(archivoDestino, rutaDestino);
                ctx.Progreso?.Report(new EstadoProgreso
                {
                    Porcentaje  = 100,
                    TareaActual = $"En cach�: {archivoDestino}",
                });
            }
        }
    }
}
