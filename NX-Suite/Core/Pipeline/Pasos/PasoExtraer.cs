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
    /// Extrae un archivo comprimido de la cach� a la carpeta de extracci�n.
    /// Soporta todos los formatos de <see cref="NX_Swite.Core.ZipLogic.ExtensionesComprimidas"/>
    /// (.zip, .7z, .rar, .tar.gz, .zst, etc.).
    /// Si la carpeta destino ya tiene archivos, asume que ya se extrajo y omite.
    ///
    /// Par�metros JSON:
    ///   Archivo            : nombre del comprimido dentro de RutaCacheZips
    ///   ArchivoZip         : alias heredado de Archivo (retrocompatible)
    ///   CarpetaDestinoTemp : subcarpeta destino dentro de RutaCacheExtraccion
    /// </summary>
    public class PasoExtraer : IPasoPipeline
    {
        public string TipoAccion => "EXTRAER";

        public async Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            // Aceptar "Archivo" (nombre moderno) o "ArchivoZip" (alias legacy)
            string archivo =
                parametros.TryGetProperty("Archivo",    out var pA) && pA.GetString() is { } a ? a :
                parametros.TryGetProperty("ArchivoZip", out var pZ) && pZ.GetString() is { } z ? z :
                throw new System.Exception("PasoExtraer: falta par�metro 'Archivo' o 'ArchivoZip'.");

            string carpetaTemp = parametros.GetProperty("CarpetaDestinoTemp").GetString()!;

            string rutaArchivo        = Path.Combine(ctx.RutaCacheZips, archivo);
            string rutaDestino         = Path.Combine(ctx.RutaCacheExtraccion, carpetaTemp);
            string rutaSidecarCarpeta  = rutaDestino + ".version";
            // Sidecar de mapeo zip?carpeta: vive en RutaCacheExtraccion junto al extra�do,
            // no en RutaCacheZips, para sobrevivir a la eliminaci�n del ZIP.
            // Nombre: <archivoZip>.destino  ?  contiene el nombre de la carpeta extra�da.
            string rutaSidecarDestino  = Path.Combine(ctx.RutaCacheExtraccion, archivo + ".destino");

            // Invalidar carpeta extra�da si el sidecar de versi�n no coincide
            if (Directory.Exists(rutaDestino)
                && !string.IsNullOrEmpty(ctx.VersionModulo)
                && File.Exists(rutaSidecarCarpeta))
            {
                string versionCarpeta = (await File.ReadAllTextAsync(rutaSidecarCarpeta, ct)).Trim();
                if (!string.Equals(versionCarpeta, ctx.VersionModulo, StringComparison.OrdinalIgnoreCase))
                {
                    Directory.Delete(rutaDestino, true);
                    File.Delete(rutaSidecarCarpeta);
                    if (File.Exists(rutaSidecarDestino)) File.Delete(rutaSidecarDestino);
                }
            }

            // Forzar re-extracci�n si el ZIP fue descargado en esta sesi�n.
            // Cubre el caso de actualizaci�n silenciosa: mismo n�mero de versi�n
            // pero contenido distinto � el sidecar coincide pero el ZIP es nuevo.
            if (ctx.ZipsDescargadosEnEstaSesion.Contains(archivo) && Directory.Exists(rutaDestino))
            {
                Logger.Info($"[{archivo}] ZIP reci�n descargado ? invalidando extracci�n en cach� para re-extraer.");
                Directory.Delete(rutaDestino, true);
                if (File.Exists(rutaSidecarCarpeta)) File.Delete(rutaSidecarCarpeta);
                if (File.Exists(rutaSidecarDestino)) File.Delete(rutaSidecarDestino);
            }

            if (!Directory.Exists(rutaDestino) ||
                Directory.GetFiles(rutaDestino, "*.*", SearchOption.AllDirectories).Length == 0)
            {
                Logger.ExtraccionIniciada(archivo, rutaArchivo);
                bool ok = await ctx.MotorZip.ExtraerTodoAsync(rutaArchivo, rutaDestino, ctx.Progreso, ct);
                if (ok)
                {
                    int total = Directory.GetFiles(rutaDestino, "*.*", SearchOption.AllDirectories).Length;
                    Logger.ExtraccionCompletada(archivo, total);
                    if (!string.IsNullOrEmpty(ctx.VersionModulo))
                        await File.WriteAllTextAsync(rutaSidecarCarpeta, ctx.VersionModulo, ct);
                    // Registrar el mapeo zip ? carpeta para que PasoDescargar lo encuentre
                    // en sesiones futuras aunque el ZIP haya sido eliminado por LIMPIAR_CACHE.
                    await File.WriteAllTextAsync(rutaSidecarDestino, carpetaTemp, ct);
                }
                else
                {
                    Logger.Error($"[{archivo}] ExtraerTodoAsync devolvi� false ? {rutaArchivo}");
                }
            }
            else
            {
                Logger.ExtraccionOmitida(archivo, rutaDestino);
                ctx.Progreso?.Report(new EstadoProgreso
                {
                    Porcentaje  = 100,
                    TareaActual = $"En cach�: {carpetaTemp}",
                });
            }
        }
    }
}
