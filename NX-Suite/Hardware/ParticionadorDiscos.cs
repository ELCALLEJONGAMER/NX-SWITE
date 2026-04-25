using NX_Suite.Core.Configuracion;
using NX_Suite.Hardware.Native;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Suite.Hardware
{
    /// <summary>
    /// Particionado y formateo FAT32 silencioso. Es la ÚNICA implementación de
    /// estas operaciones en el proyecto: tanto el modo Asistido Completo como
    /// el paso "FORMATEARSD" del pipeline JSON delegan aquí.
    ///
    /// Tres modos públicos:
    /// <list type="bullet">
    ///   <item><see cref="ParticionarYFormatearAsync"/>          ? emuMMC oculta + SWITCH SD FAT32 (estilo Hekate).</item>
    ///   <item><see cref="ParticionarSimpleYFormatearAsync"/>    ? 1 partición primary FAT32 (sin emuMMC).</item>
    ///   <item><see cref="FormatearSoloFAT32Async"/>             ? re-formatea la unidad existente sin tocar particiones.</item>
    /// </list>
    /// </summary>
    public class ParticionadorDiscos
    {
        /// <summary>
        /// Devuelve el índice del disco físico al que pertenece la letra
        /// indicada (ej. "E:\") o -1 si no se pudo determinar. Wrapper público
        /// sobre <see cref="DiscoNativo"/> para callers que necesitan resolver
        /// la letra antes de llamar a los modos de particionado.
        /// </summary>
        public int ObtenerIndiceDiscoFisico(string letraSD) => DiscoNativo.GetPhysicalDiskNumber(letraSD);

        // ????????????????????????????????????????????????????????????????????
        //  API PÚBLICA — 3 modos
        // ????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Particiona el disco físico exactamente como lo hace Hekate:
        ///   - Partición 1 (emuMMC) : id=E0 + sin letra ? invisible para Windows.
        ///   - Partición 2 (SWITCH SD): FAT32, etiqueta "SWITCH SD", letra asignada por Windows.
        /// Todo el proceso es silencioso (sin ventanas ni diálogos al usuario).
        /// </summary>
        public async Task ParticionarYFormatearAsync(
            int    numeroDisco,
            int    gbEmuMMC,
            string urlFat32FormatZip,
            IProgress<(int Pct, string Msg)> progreso,
            CancellationToken ct = default)
        {
            // Orden crítico (igual que Hekate):
            //   • create partition primary size=N   ? crea emuMMC (queda seleccionada)
            //   • remove noerr                      ? fuerza quitar cualquier letra auto-asignada
            //   • set id=E0                         ? tipo desconocido ? Windows la ignora
            //   • create partition primary          ? crea SWITCH SD (queda seleccionada)
            //   • assign                            ? Windows asigna la siguiente letra libre
            string script = $@"select disk {numeroDisco}
clean
convert mbr
create partition primary size={gbEmuMMC * 1024}
remove noerr
set id=E0
create partition primary
assign
exit";

            progreso.Report((5, "Preparando diskpart…"));
            ct.ThrowIfCancellationRequested();

            progreso.Report((10, "Particionando disco (emuMMC + SWITCH SD)…"));
            await EjecutarScriptDiskpartAsync(script, ct);
            progreso.Report((40, "Particiones creadas. Esperando a Windows…"));

            await Task.Delay(3000, ct);

            progreso.Report((45, "Detectando letra de la partición SWITCH SD…"));
            string? letraRaiz = EncontrarLetraEnDisco(numeroDisco)
                ?? throw new InvalidOperationException(
                    "No se detectó ninguna partición con letra asignada en el disco. " +
                    "El paso 'assign' de diskpart pudo haber fallado.");

            await FormatearYEtiquetarAsync(letraRaiz, urlFat32FormatZip, ConfiguracionLocal.EtiquetaSwitchSd, progreso, ct);
        }

        /// <summary>
        /// Crea una única partición primary que ocupa todo el disco y la formatea
        /// como FAT32. Útil cuando no se necesita emuMMC (instalaciones sysNAND
        /// o reseteo total de la SD).
        /// </summary>
        public async Task ParticionarSimpleYFormatearAsync(
            int    numeroDisco,
            string urlFat32FormatZip,
            string etiqueta,
            IProgress<(int Pct, string Msg)> progreso,
            CancellationToken ct = default)
        {
            string script = $@"select disk {numeroDisco}
clean
convert mbr
create partition primary
assign
exit";

            progreso.Report((5, "Preparando diskpart…"));
            ct.ThrowIfCancellationRequested();

            progreso.Report((10, "Particionando disco (1 partición FAT32)…"));
            await EjecutarScriptDiskpartAsync(script, ct);
            progreso.Report((40, "Partición creada. Esperando a Windows…"));

            await Task.Delay(3000, ct);

            progreso.Report((45, "Detectando letra de la partición…"));
            string? letraRaiz = EncontrarLetraEnDisco(numeroDisco)
                ?? throw new InvalidOperationException(
                    "No se detectó ninguna partición con letra asignada en el disco. " +
                    "El paso 'assign' de diskpart pudo haber fallado.");

            await FormatearYEtiquetarAsync(letraRaiz, urlFat32FormatZip, etiqueta, progreso, ct);
        }

        /// <summary>
        /// Re-formatea la unidad indicada como FAT32 sin tocar la tabla de
        /// particiones. Útil cuando la SD ya está particionada correctamente
        /// y solo hay que limpiar el contenido.
        /// </summary>
        /// <param name="letraRaiz">Ruta raíz de la unidad (ej. "E:\").</param>
        public async Task FormatearSoloFAT32Async(
            string letraRaiz,
            string urlFat32FormatZip,
            string etiqueta,
            IProgress<(int Pct, string Msg)> progreso,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await FormatearYEtiquetarAsync(letraRaiz, urlFat32FormatZip, etiqueta, progreso, ct);
        }

        // ????????????????????????????????????????????????????????????????????
        //  HELPERS PRIVADOS — compartidos por los 3 modos
        // ????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Recorre todas las unidades del sistema y devuelve la ruta raíz
        /// (ej. "H:\") de la partición con letra asignada que vive en el disco
        /// físico indicado. Funciona con unidades RAW (recién asignadas, sin
        /// formatear) porque no depende de <see cref="DriveInfo.IsReady"/>.
        /// </summary>
        private static string? EncontrarLetraEnDisco(int numeroDisco)
        {
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try
                {
                    if (DiscoNativo.GetPhysicalDiskNumber(d.Name) == numeroDisco)
                        return d.Name; // ej. "H:\"
                }
                catch { /* la unidad no es accesible, continuamos */ }
            }
            return null;
        }

        /// <summary>
        /// Descarga fat32format.exe si no está, formatea la letra como FAT32 y
        /// aplica la etiqueta de volumen — todo silencioso. Reportes de progreso
        /// 50% (preparando) ? 60% (formateando) ? 90% (etiqueta) ? 100% (listo).
        /// </summary>
        private static async Task FormatearYEtiquetarAsync(
            string letraRaiz,
            string urlZip,
            string etiqueta,
            IProgress<(int Pct, string Msg)> progreso,
            CancellationToken ct)
        {
            progreso.Report((50, "Preparando fat32format.exe…"));
            string exePath = await AsegurarFat32FormatAsync(urlZip, ct);

            char letra = letraRaiz[0];
            progreso.Report((60, $"Formateando {letra}: como FAT32…"));

            // fat32format.exe acepta "y" por stdin para confirmar el formateo.
            // CreateNoWindow=true ? cero ventanas visibles para el usuario.
            var psiFmt = new ProcessStartInfo("cmd.exe", $"/c echo y | \"{exePath}\" {letra}:")
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            using (var procFmt = Process.Start(psiFmt)
                ?? throw new InvalidOperationException("No se pudo iniciar fat32format.exe."))
            {
                await procFmt.WaitForExitAsync(ct);
                if (procFmt.ExitCode != 0)
                {
                    string err = await procFmt.StandardError.ReadToEndAsync(ct);
                    throw new InvalidOperationException(
                        $"fat32format terminó con código {procFmt.ExitCode}. {err}");
                }
            }

            // Etiqueta vía API directa de Windows ? sin ventanas, sin procesos extra.
            progreso.Report((90, $"Aplicando etiqueta {etiqueta}…"));
            await Task.Delay(1500, ct);
            DiscoNativo.SetVolumeLabel(letraRaiz, etiqueta);

            progreso.Report((100, "Listo"));
        }

        /// <summary>
        /// Garantiza que fat32format.exe existe en la carpeta de la aplicación.
        /// Si ya existe lo reutiliza (caché). Si no, lo descarga del ZIP indicado.
        /// </summary>
        private static async Task<string> AsegurarFat32FormatAsync(string urlZip, CancellationToken ct)
        {
            string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfiguracionLocal.NombreFat32FormatExe);
            if (File.Exists(exePath)) return exePath;

            if (string.IsNullOrWhiteSpace(urlZip))
                throw new InvalidOperationException(
                    "fat32format.exe no encontrado y no hay URL de descarga en el JSON " +
                    "(ConfiguracionUI.UrlFat32Format o paso FORMATEARSD.UrlHerramienta).");

            string zipPath    = Path.Combine(Path.GetTempPath(), ConfiguracionLocal.NombreFat32FormatZip);
            string tempFolder = Path.Combine(Path.GetTempPath(), ConfiguracionLocal.NombreFat32FormatTemp);

            try
            {
                using var http = new System.Net.Http.HttpClient();
                http.Timeout = TimeSpan.FromSeconds(60);
                var bytes = await http.GetByteArrayAsync(urlZip, ct);
                await File.WriteAllBytesAsync(zipPath, bytes, ct);

                if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
                ZipFile.ExtractToDirectory(zipPath, tempFolder);

                string? found = Directory.GetFiles(tempFolder, ConfiguracionLocal.NombreFat32FormatExe, SearchOption.AllDirectories)
                                         .FirstOrDefault();
                if (found == null)
                    throw new InvalidOperationException("El ZIP descargado no contiene fat32format.exe.");

                File.Copy(found, exePath, overwrite: true);
            }
            finally
            {
                try { File.Delete(zipPath); }              catch { }
                try { Directory.Delete(tempFolder, true); } catch { }
            }

            return exePath;
        }

        /// <summary>
        /// Escribe el script de diskpart a un archivo temporal y lo ejecuta de
        /// forma silenciosa. La app tiene <c>requireAdministrator</c> en el
        /// manifest, por lo que diskpart hereda los permisos sin necesitar
        /// <c>Verb="runas"</c>. El exit code de diskpart NO se valida porque
        /// devuelve códigos no estándar para advertencias no fatales (ej.
        /// "remove noerr" sin letra). El éxito real se verifica al detectar la
        /// letra con <see cref="EncontrarLetraEnDisco"/>.
        /// </summary>
        private static async Task EjecutarScriptDiskpartAsync(string script, CancellationToken ct)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), ConfiguracionLocal.NombreDiskpartScript);
            await File.WriteAllTextAsync(scriptPath, script, System.Text.Encoding.ASCII, ct);

            try
            {
                var psi = new ProcessStartInfo("diskpart.exe", $"/s \"{scriptPath}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                };

                using var proc = Process.Start(psi)
                    ?? throw new InvalidOperationException("No se pudo iniciar diskpart.");

                await proc.WaitForExitAsync(ct);
            }
            finally
            {
                try { File.Delete(scriptPath); } catch { }
            }
        }
    }
}
