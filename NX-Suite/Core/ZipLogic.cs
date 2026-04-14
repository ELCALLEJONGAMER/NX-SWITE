using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NX_Suite.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace NX_Suite.Core
{
    public class ZipLogic
    {
        /// <summary>
        /// Extrae un archivo comprimido (.zip, .7z, .rar) y reporta el progreso de forma optimizada.
        /// </summary>
        public async Task<bool> ExtraerTodoAsync(string rutaArchivo, string rutaCarpetaDestino, IProgress<EstadoProgreso> progreso = null)
        {
            if (!File.Exists(rutaArchivo)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(rutaCarpetaDestino)) Directory.Delete(rutaCarpetaDestino, true);
                    Directory.CreateDirectory(rutaCarpetaDestino);

                    using (var archive = ArchiveFactory.OpenArchive(rutaArchivo))
                    {
                        var entradasValidas = archive.Entries.Where(e => !e.IsDirectory).ToList();
                        int totalArchivos = entradasValidas.Count;
                        int archivosExtraidos = 0;

                        foreach (var entry in entradasValidas)
                        {
                            entry.WriteToDirectory(rutaCarpetaDestino, new ExtractionOptions()
                            {
                                ExtractFullPath = true,
                                Overwrite = true
                            });

                            archivosExtraidos++;

                            // TRUCO DE RENDIMIENTO: Solo actualizamos la UI cada 50 archivos o si es el último.
                            // Esto evita que la aplicación se congele al extraer paquetes inmensos como RetroArch.
                            if (progreso != null && (archivosExtraidos % 50 == 0 || archivosExtraidos == totalArchivos))
                            {
                                double porcentaje = totalArchivos > 0 ? (double)archivosExtraidos / totalArchivos * 100 : 100;
                                string nombreArchivoUI = Path.GetFileName(entry.Key ?? "archivo_desconocido");

                                progreso.Report(new EstadoProgreso
                                {
                                    Porcentaje = porcentaje,
                                    TareaActual = $"Extrayendo ({archivosExtraidos}/{totalArchivos}): {nombreArchivoUI}",
                                    PasoActual = 2 // <---- AÑADE ESTA LÍNEA
                                });
                            }
                        }
                    }
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        public void LimpiarTemporales(string rutaZip, string rutaCarpetaExtraida)
        {
            try
            {
                if (File.Exists(rutaZip)) File.Delete(rutaZip);
                if (Directory.Exists(rutaCarpetaExtraida)) Directory.Delete(rutaCarpetaExtraida, true);
            }
            catch { /* Ignorar errores de bloqueo temporal */ }
        }
    }
}