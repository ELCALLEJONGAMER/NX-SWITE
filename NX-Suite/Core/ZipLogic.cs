using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NX_Suite.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace NX_Suite.Core
{
    public class ZipLogic
    {
        /// <summary>
        /// Formatos de archivo comprimido reconocidos por el pipeline.
        /// Incluye ZIP, 7-Zip, RAR, Tar y sus variantes comprimidas,
        /// Zstandard, LZip y los alias cortos de tar.
        /// </summary>
        public static readonly HashSet<string> ExtensionesComprimidas =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".zip", ".7z", ".rar",
                ".gz",  ".tgz",
                ".bz2", ".tbz2", ".tbz",
                ".xz",  ".txz",
                ".tar",
                ".zst",
                ".lz",
            };

        /// <summary>
        /// Extrae un archivo comprimido (.zip, .7z, .rar, .tar.gz, .zst…)
        /// usando SharpCompress con detección automática de formato.
        /// Reporta progreso cada 50 entradas para no saturar la UI.
        /// Respeta el <paramref name="ct"/> entre entradas.
        /// </summary>
        public async Task<bool> ExtraerTodoAsync(
            string rutaArchivo,
            string rutaCarpetaDestino,
            IProgress<EstadoProgreso>? progreso = null,
            CancellationToken ct = default)
        {
            if (!File.Exists(rutaArchivo)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(rutaCarpetaDestino))
                        Directory.Delete(rutaCarpetaDestino, true);
                    Directory.CreateDirectory(rutaCarpetaDestino);

                    using var archive = ArchiveFactory.OpenArchive(rutaArchivo);

                    var entradas = archive.Entries.Where(e => !e.IsDirectory).ToList();
                    int total     = entradas.Count;
                    int extraidos = 0;

                    var opciones = new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite       = true,
                    };

                    foreach (var entry in entradas)
                    {
                        ct.ThrowIfCancellationRequested();

                        entry.WriteToDirectory(rutaCarpetaDestino, opciones);
                        extraidos++;

                        // Actualizar UI cada 50 archivos o en el último para no congelar la app
                        if (progreso != null && (extraidos % 50 == 0 || extraidos == total))
                        {
                            double pct    = total > 0 ? (double)extraidos / total * 100 : 100;
                            string nombre = Path.GetFileName(entry.Key ?? "archivo");
                            progreso.Report(new EstadoProgreso
                            {
                                Porcentaje  = pct,
                                TareaActual = $"Extrayendo ({extraidos}/{total}): {nombre}",
                                PasoActual  = 2,
                            });
                        }
                    }

                    return true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    return false;
                }
            }, ct);
        }

        public void LimpiarTemporales(string rutaArchivo, string rutaCarpetaExtraida)
        {
            try
            {
                if (File.Exists(rutaArchivo))             File.Delete(rutaArchivo);
                if (Directory.Exists(rutaCarpetaExtraida)) Directory.Delete(rutaCarpetaExtraida, true);
            }
            catch { /* Ignorar errores de bloqueo temporal */ }
        }
    }
}