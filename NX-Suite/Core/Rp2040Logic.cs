using NX_Suite.Models;
using NX_Suite.Core.Configuracion;
using NX_Suite.Models;
using NX_Suite.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Core
{
    /// <summary>
    /// Lógica de detección y flasheo del chip RP2040 (Picofly).
    ///
    /// El RP2040 en modo bootloader se monta como una unidad FAT con la
    /// etiqueta de volumen <c>RPI-RP2</c> y contiene el archivo
    /// <c>INFO_UF2.TXT</c> en su raíz. Copiar un archivo <c>.uf2</c> a la
    /// raíz de esa unidad provoca el flasheo automático del chip.
    /// </summary>
    public class Rp2040Logic
    {
        private const string EtiquetaRp2040    = "RPI-RP2";
        private const string ArchivoInfoUf2    = "INFO_UF2.TXT";
        private const string ExtensionFirmware = ".uf2";

        // ?? Detección ????????????????????????????????????????????????????

        /// <summary>
        /// Devuelve <c>true</c> si la unidad cuya raíz es <paramref name="letraConDosP"/>
        /// corresponde a un RP2040 en modo bootloader.
        /// </summary>
        public bool EsRp2040(string letraConDosP)
        {
            try
            {
                var drive = DriveInfo.GetDrives()
                    .FirstOrDefault(d => string.Equals(d.RootDirectory.FullName,
                                                        letraConDosP,
                                                        StringComparison.OrdinalIgnoreCase));

                if (drive == null || !drive.IsReady) return false;

                bool etiquetaCoincide = string.Equals(drive.VolumeLabel, EtiquetaRp2040,
                                                      StringComparison.OrdinalIgnoreCase);
                bool tieneInfoUf2    = File.Exists(Path.Combine(letraConDosP, ArchivoInfoUf2));

                return etiquetaCoincide || tieneInfoUf2;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Busca entre todas las unidades removibles cuál corresponde a un RP2040.
        /// Devuelve la letra con dos puntos (ej. "E:") o <c>null</c> si no se encuentra.
        /// </summary>
        public string? DetectarLetraRp2040()
        {
            try
            {
                foreach (var drive in DriveInfo.GetDrives()
                             .Where(d => d.DriveType == DriveType.Removable && d.IsReady))
                {
                    if (EsRp2040(drive.RootDirectory.FullName))
                        return drive.RootDirectory.FullName.TrimEnd('\\');
                }
            }
            catch { }

            return null;
        }

        // ?? Lectura de versión ????????????????????????????????????????????

        // Archivos que Picofly escribe en la unidad al entrar en bootloader:
        // - PICOFLY.TXT  ? versiones recientes de Picofly escriben su versión aquí
        // - INFO_UF2.TXT ? campo "Model:" sobreescrito por Picofly con su versión
        // NO usar la línea "UF2 Bootloader vX.X" ya que esa es la versión del
        // bootloader RP2040 estándar, no la del firmware Picofly instalado.

        private const string ArchivoPicoflyTxt = "PICOFLY.TXT";

        /// <summary>
        /// Intenta leer la versión del firmware Picofly instalado en el chip.
        /// Busca primero en <c>PICOFLY.TXT</c>, luego en el campo <c>Model:</c>
        /// de <c>INFO_UF2.TXT</c>. Devuelve la versión o <c>null</c> si no
        /// puede determinarse (en ese caso NO se muestra versión, no se muestra bootloader).
        /// </summary>
        public string? LeerVersionFirmware(string letraConDosP)
        {
            try
            {
                // 1. PICOFLY.TXT (versiones recientes de Picofly)
                string rutaPicofly = Path.Combine(letraConDosP, ArchivoPicoflyTxt);
                if (File.Exists(rutaPicofly))
                {
                    foreach (var linea in File.ReadAllLines(rutaPicofly))
                    {
                        var l = linea.Trim();
                        if (string.IsNullOrEmpty(l)) continue;
                        // Formato: "Version: 2.7.3" o simplemente "2.7.3"
                        var sep = l.IndexOf(':');
                        var val = sep >= 0 ? l[(sep + 1)..].Trim() : l;
                        if (!string.IsNullOrEmpty(val)) return val;
                    }
                }

                // 2. INFO_UF2.TXT — campo "Model:" que Picofly sobreescribe
                string rutaInfo = Path.Combine(letraConDosP, ArchivoInfoUf2);
                if (File.Exists(rutaInfo))
                {
                    foreach (var linea in File.ReadAllLines(rutaInfo))
                    {
                        if (linea.StartsWith("Model:", StringComparison.OrdinalIgnoreCase))
                        {
                            var valor = linea["Model:".Length..].Trim();
                            // Si el valor es "RP2040" es el genérico del bootloader, no sirve
                            if (!string.IsNullOrEmpty(valor) &&
                                !valor.Equals("RP2040", StringComparison.OrdinalIgnoreCase))
                                return valor;
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        // ?? Caché del firmware ????????????????????????????????????????????

        /// <summary>
        /// Ruta local del sidecar de versión junto al firmware cacheado.
        /// Mismo patrón que PasoDescargar/PasoExtraer del pipeline.
        /// </summary>
        private static string RutaSidecarVersion =>
            ConfiguracionLocal.RutaCacheFirmwareRp2040 + ".version";

        /// <summary>
        /// Devuelve <c>true</c> si el firmware cacheado existe y su sidecar
        /// coincide con la versión remota indicada.
        /// </summary>
        private static bool FirmwareEnCacheEsValido(string versionRemota)
        {
            if (string.IsNullOrEmpty(versionRemota)) return false;
            string rutaUf2     = ConfiguracionLocal.RutaCacheFirmwareRp2040;
            string rutaSidecar = RutaSidecarVersion;
            if (!File.Exists(rutaUf2) || !File.Exists(rutaSidecar)) return false;

            try
            {
                string versionCacheada = File.ReadAllText(rutaSidecar).Trim();
                return string.Equals(versionCacheada, versionRemota.Trim(),
                                     StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>
        /// Versión pública para que la UI pueda consultar si el firmware ya está en caché.
        /// </summary>
        public static bool FirmwareDisponibleEnCache(string versionRemota)
            => FirmwareEnCacheEsValido(versionRemota);

        /// <summary>
        /// Escribe el sidecar de versión tras una descarga exitosa.
        /// </summary>
        private static void EscribirSidecarVersion(string version)
        {
            try { File.WriteAllText(RutaSidecarVersion, version.Trim()); }
            catch { }
        }

        // ?? Flasheo ??????????????????????????????????????????????????????

        /// <summary>
        /// Descarga el firmware desde <paramref name="urlFirmware"/> (o lo toma de caché
        /// si la versión coincide) y lo copia a la raíz de la unidad RP2040.
        /// </summary>
        /// <param name="letraConDosP">Ej. "E:"</param>
        /// <param name="urlFirmware">URL directa del archivo <c>.uf2</c>.</param>
        /// <param name="versionRemota">Versión del Gist — usada para validar la caché.</param>
        /// <param name="progreso">Progreso de descarga.</param>
        /// <param name="ct">Token de cancelación.</param>
        public async Task<Resultado> FlashearAsync(
            string letraConDosP,
            string urlFirmware,
            string versionRemota,
            IProgress<EstadoProgreso>? progreso = null,
            CancellationToken ct = default)
        {
            try
            {
                Logger.Rp2040FlasheoIniciado(letraConDosP, urlFirmware);

                string rutaCache = ConfiguracionLocal.RutaCacheFirmwareRp2040;

                // Asegurar que la carpeta Cache existe
                Directory.CreateDirectory(Path.GetDirectoryName(rutaCache)!);

                // Usar caché si la versión coincide; descargar si no
                if (FirmwareEnCacheEsValido(versionRemota))
                {
                    progreso?.Report(new EstadoProgreso
                        { TareaActual = "Usando firmware en caché…", Porcentaje = 100 });
                }
                else
                {
                    var dl = new DownloadLogic();
                    await dl.DescargarArchivoAsync(urlFirmware, rutaCache, progreso, ct);
                    if (!string.IsNullOrEmpty(versionRemota))
                        EscribirSidecarVersion(versionRemota);
                }

                ct.ThrowIfCancellationRequested();

                // Copiar a la unidad RP2040
                string destino = Path.Combine(letraConDosP + "\\", "picofly_firmware.uf2");
                File.Copy(rutaCache, destino, overwrite: true);

                Logger.Rp2040FlasheoCompletado(letraConDosP);
                return Resultado.Ok();
            }
            catch (OperationCanceledException)
            {
                return Resultado.Error("Operación cancelada.");
            }
            catch (Exception ex)
            {
                Logger.Rp2040FlasheoFallido(letraConDosP, ex);
                return Resultado.Error(ex.Message);
            }
        }

        /// <summary>
        /// Descarga el firmware (o lo toma de caché) y lo guarda en la ruta elegida por el usuario.
        /// </summary>
        public async Task<Resultado> GuardarEnPcAsync(
            string urlFirmware,
            string versionRemota,
            string rutaDestino,
            IProgress<EstadoProgreso>? progreso = null,
            CancellationToken ct = default)
        {
            try
            {
                string rutaCache = ConfiguracionLocal.RutaCacheFirmwareRp2040;
                Directory.CreateDirectory(Path.GetDirectoryName(rutaCache)!);

                if (FirmwareEnCacheEsValido(versionRemota))
                {
                    progreso?.Report(new EstadoProgreso
                        { TareaActual = "Usando firmware en caché…", Porcentaje = 100 });
                    File.Copy(rutaCache, rutaDestino, overwrite: true);
                }
                else
                {
                    var dl = new DownloadLogic();
                    await dl.DescargarArchivoAsync(urlFirmware, rutaCache, progreso, ct);
                    if (!string.IsNullOrEmpty(versionRemota))
                        EscribirSidecarVersion(versionRemota);
                    File.Copy(rutaCache, rutaDestino, overwrite: true);
                }

                Logger.Rp2040GuardadoEnPc(rutaDestino);
                return Resultado.Ok();
            }
            catch (OperationCanceledException)
            {
                return Resultado.Error("Operación cancelada.");
            }
            catch (Exception ex)
            {
                return Resultado.Error(ex.Message);
            }
        }
    }
}
