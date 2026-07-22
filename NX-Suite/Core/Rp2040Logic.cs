using NX_Swite.Models;
using NX_Swite.Core.Configuracion;
using NX_Swite.Models;
using NX_Swite.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Swite.Core
{
    /// <summary>
    /// L�gica de detecci�n y flasheo del chip RP2040 (Picofly).
    ///
    /// El RP2040 en modo bootloader se monta como una unidad FAT con la
    /// etiqueta de volumen <c>RPI-RP2</c> y contiene el archivo
    /// <c>INFO_UF2.TXT</c> en su ra�z. Copiar un archivo <c>.uf2</c> a la
    /// ra�z de esa unidad provoca el flasheo autom�tico del chip.
    /// </summary>
    public class Rp2040Logic
    {
        private const string EtiquetaRp2040    = "RPI-RP2";
        private const string ArchivoInfoUf2    = "INFO_UF2.TXT";
        private const string ExtensionFirmware = ".uf2";

        // ?? Detecci�n ????????????????????????????????????????????????????

        /// <summary>
        /// Devuelve <c>true</c> si la unidad cuya ra�z es <paramref name="letraConDosP"/>
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
        /// Busca entre todas las unidades removibles cu�l corresponde a un RP2040.
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

        // ?? Lectura de versi�n ????????????????????????????????????????????

        // Archivos que Picofly escribe en la unidad al entrar en bootloader:
        // - PICOFLY.TXT  ? versiones recientes de Picofly escriben su versi�n aqu�
        // - INFO_UF2.TXT ? campo "Model:" sobreescrito por Picofly con su versi�n
        // NO usar la l�nea "UF2 Bootloader vX.X" ya que esa es la versi�n del
        // bootloader RP2040 est�ndar, no la del firmware Picofly instalado.

        private const string ArchivoPicoflyTxt = "PICOFLY.TXT";

        /// <summary>
        /// Intenta leer la versi�n del firmware Picofly instalado en el chip.
        /// Busca primero en <c>PICOFLY.TXT</c>, luego en el campo <c>Model:</c>
        /// de <c>INFO_UF2.TXT</c>. Devuelve la versi�n o <c>null</c> si no
        /// puede determinarse (en ese caso NO se muestra versi�n, no se muestra bootloader).
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

                // 2. INFO_UF2.TXT � campo "Model:" que Picofly sobreescribe
                string rutaInfo = Path.Combine(letraConDosP, ArchivoInfoUf2);
                if (File.Exists(rutaInfo))
                {
                    foreach (var linea in File.ReadAllLines(rutaInfo))
                    {
                        if (linea.StartsWith("Model:", StringComparison.OrdinalIgnoreCase))
                        {
                            var valor = linea["Model:".Length..].Trim();
                            // Si el valor es "RP2040" es el gen�rico del bootloader, no sirve
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

        // ?? Cach� del firmware ????????????????????????????????????????????

        /// <summary>
        /// Ruta local del sidecar de versi�n junto al firmware cacheado.
        /// Mismo patr�n que PasoDescargar/PasoExtraer del pipeline.
        /// </summary>
        private static string RutaSidecarVersion =>
            ConfiguracionLocal.RutaCacheFirmwareRp2040 + ".version";

        /// <summary>
        /// Devuelve <c>true</c> si el firmware cacheado existe y su sidecar
        /// coincide con la versi�n remota indicada.
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
        /// Versi�n p�blica para que la UI pueda consultar si el firmware ya est� en cach�.
        /// </summary>
        public static bool FirmwareDisponibleEnCache(string versionRemota)
            => FirmwareEnCacheEsValido(versionRemota);

        /// <summary>
        /// Escribe el sidecar de versi�n tras una descarga exitosa.
        /// </summary>
        private static void EscribirSidecarVersion(string version)
        {
            try { File.WriteAllText(RutaSidecarVersion, version.Trim()); }
            catch { }
        }

        // ?? Flasheo ??????????????????????????????????????????????????????

        /// <summary>
        /// Descarga el firmware desde <paramref name="urlFirmware"/> (o lo toma de cach�
        /// si la versi�n coincide) y lo copia a la ra�z de la unidad RP2040.
        /// </summary>
        /// <param name="letraConDosP">Ej. "E:"</param>
        /// <param name="urlFirmware">URL directa del archivo <c>.uf2</c>.</param>
        /// <param name="versionRemota">Versi�n del Gist � usada para validar la cach�.</param>
        /// <param name="progreso">Progreso de descarga.</param>
        /// <param name="ct">Token de cancelaci�n.</param>
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

                // Usar cach� si la versi�n coincide; descargar si no
                if (FirmwareEnCacheEsValido(versionRemota))
                {
                    progreso?.Report(new EstadoProgreso
                        { TareaActual = "Usando firmware en cach�", Porcentaje = 100 });
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
                return Resultado.Error("Operaci�n cancelada.");
            }
            catch (Exception ex)
            {
                Logger.Rp2040FlasheoFallido(letraConDosP, ex);
                return Resultado.Error(ex.Message);
            }
        }

        /// <summary>
        /// Descarga el firmware (o lo toma de cach�) y lo guarda en la ruta elegida por el usuario.
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
                        { TareaActual = "Usando firmware en cach�", Porcentaje = 100 });
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
                return Resultado.Error("Operaci�n cancelada.");
            }
            catch (Exception ex)
            {
                return Resultado.Error(ex.Message);
            }
        }
    }
}
