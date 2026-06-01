using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NX_Suite.Services;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Extrae un archivo comprimido de la caché a la carpeta de extracción.
    /// Soporta todos los formatos de <see cref="NX_Suite.Core.ZipLogic.ExtensionesComprimidas"/>
    /// (.zip, .7z, .rar, .tar.gz, .zst, etc.).
    /// Si la carpeta destino ya tiene archivos, asume que ya se extrajo y omite.
    ///
    /// Parámetros JSON:
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
                throw new System.Exception("PasoExtraer: falta parámetro 'Archivo' o 'ArchivoZip'.");

            string carpetaTemp = parametros.GetProperty("CarpetaDestinoTemp").GetString()!;

            string rutaArchivo = Path.Combine(ctx.RutaCacheZips, archivo);
            string rutaDestino = Path.Combine(ctx.RutaCacheExtraccion, carpetaTemp);

            if (!Directory.Exists(rutaDestino) ||
                Directory.GetFiles(rutaDestino, "*.*", SearchOption.AllDirectories).Length == 0)
            {
                Logger.ExtraccionIniciada(archivo, rutaArchivo);
                bool ok = await ctx.MotorZip.ExtraerTodoAsync(rutaArchivo, rutaDestino, ctx.Progreso, ct);
                if (ok)
                {
                    int total = Directory.GetFiles(rutaDestino, "*.*", SearchOption.AllDirectories).Length;
                    Logger.ExtraccionCompletada(archivo, total);
                }
                else
                {
                    Logger.Error($"[{archivo}] ExtraerTodoAsync devolvió false ? {rutaArchivo}");
                }
            }
            else
            {
                Logger.ExtraccionOmitida(archivo, rutaDestino);
            }
        }
    }
}
