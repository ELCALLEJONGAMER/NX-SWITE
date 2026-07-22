using System.IO;
using NX_Swite.Models;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NX_Swite.Services;

namespace NX_Swite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Copia el contenido de OrigenTemp (carpeta o archivo) a una ruta de la SD
    /// reportando progreso preciso: cuenta el total de archivos antes de copiar
    /// y emite un reporte por cada archivo copiado.
    ///
    /// Parametros JSON:
    ///   OrigenTemp : nombre de carpeta o archivo (relativo a las caches)
    ///   DestinoSD  : ruta destino dentro de la SD (ej. "/atmosphere/")
    /// </summary>
    public class PasoCopiarSD : IPasoPipeline
    {
        public string TipoAccion => "COPIARSD";

        public Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            string origenTemp    = parametros.GetProperty("OrigenTemp").GetString()!;
            string destinoSDJson = parametros.GetProperty("DestinoSD").GetString()!;
            string rutaDestinoSD = PipelineFsHelpers.RutaSDAbsoluta(ctx.LetraSD, destinoSDJson);

            string rutaOrigenExtraccion = System.IO.Path.Combine(ctx.RutaCacheExtraccion, origenTemp);

            if (System.IO.Directory.Exists(rutaOrigenExtraccion))
            {
                int total    = PipelineFsHelpers.ContarArchivos(rutaOrigenExtraccion);
                int copiados = 0;

                Logger.CopiadoIniciado(origenTemp, rutaDestinoSD);
                PipelineFsHelpers.CopiarDirectorio(
                    rutaOrigenExtraccion,
                    rutaDestinoSD,
                    onArchivoCopado: rutaArchivo =>
                    {
                        ct.ThrowIfCancellationRequested();
                        copiados++;

                        double pct = total > 0 ? copiados * 100.0 / total : 100.0;
                        ctx.Progreso?.Report(new EstadoProgreso
                        {
                            Porcentaje = pct,
                            TareaActual = $"Copiando en SD: {System.IO.Path.GetFileName(rutaArchivo)}  ({copiados}/{total})",
                            PasoActual  = 3
                        });
                    });
                Logger.CopiadoCompletado(origenTemp, total);
            }
            else if (System.IO.File.Exists(rutaOrigenExtraccion))
            {
                if (!System.IO.Directory.Exists(rutaDestinoSD))
                    System.IO.Directory.CreateDirectory(rutaDestinoSD);

                ct.ThrowIfCancellationRequested();
                System.IO.File.Copy(
                    rutaOrigenExtraccion,
                    System.IO.Path.Combine(rutaDestinoSD, System.IO.Path.GetFileName(origenTemp)),
                    overwrite: true);

                ctx.Progreso?.Report(new EstadoProgreso
                {
                    Porcentaje = 100.0,
                    TareaActual = $"Copiando en SD: {System.IO.Path.GetFileName(origenTemp)}",
                    PasoActual  = 3
                });
            }
            else
            {
                // Ultimo recurso: buscar en la cache de ZIPs
                string rutaOrigenZips = System.IO.Path.Combine(ctx.RutaCacheZips, origenTemp);
                if (System.IO.File.Exists(rutaOrigenZips))
                {
                    if (!System.IO.Directory.Exists(rutaDestinoSD))
                        System.IO.Directory.CreateDirectory(rutaDestinoSD);

                    ct.ThrowIfCancellationRequested();
                    System.IO.File.Copy(
                        rutaOrigenZips,
                        System.IO.Path.Combine(rutaDestinoSD, System.IO.Path.GetFileName(origenTemp)),
                        overwrite: true);

                    ctx.Progreso?.Report(new EstadoProgreso
                    {
                        Porcentaje = 100.0,
                        TareaActual = $"Copiando en SD: {System.IO.Path.GetFileName(origenTemp)}",
                        PasoActual  = 3
                    });
                }
            }

            return Task.CompletedTask;
        }
    }
}
