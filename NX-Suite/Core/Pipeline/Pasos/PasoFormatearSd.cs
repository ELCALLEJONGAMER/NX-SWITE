using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.Services;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core.Pipeline.Pasos
{
    /// <summary>
    /// Formateo y/o particionado FAT32 de la SD. Delega 100% en
    /// <see cref="ParticionadorDiscos"/> — esta clase es solo el adaptador entre los
    /// parámetros del JSON y los 3 modos públicos del ParticionadorDiscos.
    ///
    /// Parámetros JSON:
    ///   UrlHerramienta : URL del ZIP con fat32format.exe (opcional si ya está cacheado)
    ///   Confirmacion   : "SI" → seguridad anti-clic accidental
    ///   SoloFormatear  : true → re-formatea sin tocar particiones
    ///   TamanoEmuMB    : tamaño en MB de la emuMMC oculta. >0 → emuMMC + SWITCH SD;
    ///                    0 (o ausente) → 1 partición FAT32 sola.
    ///   Etiqueta       : etiqueta de volumen (opcional, por defecto "SWITCH SD")
    /// </summary>
    public class PasoFormatearSd : IPasoPipeline
    {
        public string TipoAccion => "FORMATEARSD";

        public async Task EjecutarAsync(ContextoPipeline ctx, JsonElement parametros, CancellationToken ct)
        {
            // ─── Lectura de parámetros del JSON ──────────────────────────
            string urlTool       = parametros.TryGetProperty("UrlHerramienta", out var urlProp) ? urlProp.GetString() ?? "" : "";
            string conf          = parametros.GetProperty("Confirmacion").GetString() ?? "";
            bool   soloFormatear = parametros.TryGetProperty("SoloFormatear", out var sfProp) && sfProp.GetBoolean();
            string etiqueta      = parametros.TryGetProperty("Etiqueta", out var etProp) ? etProp.GetString() ?? "SWITCH SD" : "SWITCH SD";

            int emuSizeMB = 0;
            if (parametros.TryGetProperty("TamanoEmuMB", out var emuProp))
            {
                string val = emuProp.GetString() ?? "";
                if (!string.IsNullOrEmpty(val)) int.TryParse(val, out emuSizeMB);
            }

            if (!string.Equals(conf, "SI", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Formateo cancelado por falta de confirmación en el JSON.");

            // ??? Adaptador de progreso (Pct,Msg) ? EstadoProgreso ????????
            var disk     = new ParticionadorDiscos();
            var progDisk = new Progress<(int Pct, string Msg)>(p =>
                ctx.Progreso?.Report(new EstadoProgreso
                {
                    Porcentaje  = p.Pct,
                    TareaActual = p.Msg,
                    PasoActual  = 3
                }));

            ctx.Progreso?.Report(new EstadoProgreso { Porcentaje = 0, TareaActual = "Iniciando preparación...", PasoActual = 1 });
            string letraFija = ctx.LetraSD.Substring(0, 1) + ":\\";

            // ╔══ 3 modos: solo formatear / particionar simple / emuMMC ══
            try
            {
                if (soloFormatear)
                {
                    Logger.FormateoIniciado(letraFija, "Solo FAT32", etiqueta);
                    await disk.FormatearSoloFAT32Async(letraFija, urlTool, etiqueta, progDisk, ct);
                    Logger.FormateoCompletado(letraFija);
                }
                else
                {
                    int discoIndice = disk.ObtenerIndiceDiscoFisico(letraFija);
                    if (discoIndice == -1)
                        throw new InvalidOperationException("No se pudo identificar el disco físico de la SD para particionar.");

                    if (emuSizeMB > 0)
                    {
                        int gbEmu = (int)Math.Ceiling(emuSizeMB / 1024.0);
                        Logger.ParticionadoIniciado(letraFija, "emuMMC + SWITCH SD", emuSizeMB);
                        await disk.ParticionarYFormatearAsync(discoIndice, gbEmu, urlTool, etiqueta, progDisk, ct);
                        Logger.ParticionadoCompletado(letraFija);
                    }
                    else
                    {
                        Logger.ParticionadoIniciado(letraFija, "FAT32 simple", 0);
                        await disk.ParticionarSimpleYFormatearAsync(discoIndice, urlTool, etiqueta, progDisk, ct);
                        Logger.ParticionadoCompletado(letraFija);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (soloFormatear)
                    Logger.FormateoFallido(letraFija, ex);
                else
                    Logger.ParticionadoFallido(letraFija, ex);
                throw;
            }

            ctx.Progreso?.Report(new EstadoProgreso { Porcentaje = 100, TareaActual = "Formateo completado con éxito.", PasoActual = 4 });
        }
    }
}
