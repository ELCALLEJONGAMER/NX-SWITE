using NX_Swite.Core.Configuracion;
using NX_Swite.Core.Configuracion;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NX_Swite.Core
{
    /// <summary>
    /// Excepción específica para fallos al asegurar el CLI de NxNandManager
    /// (configuración incompleta, descarga fallida, hash inválido, extracción
    /// fallida). Permite a <see cref="NxNandManagerLogic"/> mapear el fallo al
    /// estado de UI correcto sin depender de mensajes de texto.
    /// </summary>
    public sealed class HerramientaNoDisponibleException : Exception
    {
        public HerramientaNoDisponibleException(string mensaje) : base(mensaje) { }
        public HerramientaNoDisponibleException(string mensaje, Exception inner) : base(mensaje, inner) { }
    }

    /// <summary>
    /// Administra la descarga, verificación (SHA-256) y caché por versión del
    /// CLI de NxNandManager, usado para leer el firmware interno de la emuMMC.
    ///
    /// El asset publicado en el Gist es un <c>.zip</c> (no el ejecutable
    /// directo) que además incluye dependencias nativas necesarias en tiempo
    /// de ejecución (p.ej. <c>dokan1.dll</c>). El flujo es: descargar ZIP ?
    /// validar SHA-256 del ZIP ? extraer ? localizar la carpeta que contiene
    /// el ejecutable indicado por <c>CLI_NX_NAND_MANAGER_EXECUTABLE</c> ?
    /// mover TODO el contenido de esa carpeta (ejecutable + DLLs vecinas) de
    /// forma atómica a <c>%AppData%\NX-Swite\Tools\NxNandManager\&lt;version&gt;\</c>
    /// (vía <see cref="ConfiguracionLocal"/>). La versión anterior nunca se borra
    /// hasta que la nueva descarga se ha validado y movido a su carpeta definitiva.
    /// </summary>
    public static class GestorHerramientaNxNandManager
    {
        /// <summary>
        /// Devuelve la ruta al ejecutable de NxNandManager, descargándolo y
        /// validándolo si es necesario. Lanza <see cref="HerramientaNoDisponibleException"/>
        /// si la configuración remota está incompleta o la validación falla.
        /// </summary>
        public static async Task<string> ObtenerRutaEjecutableAsync(CancellationToken ct)
        {
            var tools = ConfiguracionRemota.Tools;

            string url              = tools.CliNxNandManagerUrl;
            string version           = tools.CliNxNandManagerVersion;
            string sha256Esperado    = tools.CliNxNandManagerSha256;
            string nombreArchivo     = tools.CliNxNandManagerFilename;
            string nombreEjecutable  = tools.CliNxNandManagerExecutable;

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(version) ||
                string.IsNullOrWhiteSpace(sha256Esperado) || string.IsNullOrWhiteSpace(nombreArchivo) ||
                string.IsNullOrWhiteSpace(nombreEjecutable))
            {
                throw new HerramientaNoDisponibleException(
                    "Configuración de NxNandManager incompleta en el Gist (sección \"tools\").");
            }

            string rutaVersion  = ConfiguracionLocal.RutaVersionNxNandManager(version);
            string rutaExeFinal = ConfiguracionLocal.RutaEjecutableNxNandManager(version, nombreEjecutable);

            // 1. Reutilizar caché si el ejecutable ya fue extraído y activado.
            if (File.Exists(rutaExeFinal))
                return rutaExeFinal;

            Directory.CreateDirectory(rutaVersion);

            string rutaZip = ConfiguracionLocal.RutaZipNxNandManager(version, nombreArchivo) + $".tmp_{Guid.NewGuid():N}";
            string rutaExtraccion = Path.Combine(rutaVersion, $"_extraido_{Guid.NewGuid():N}");

            try
            {
                // 2. Descargar el ZIP (normalizando URLs "blob" de GitHub a "raw").
                string urlDescarga = NormalizarUrlDescargaDirecta(url);

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                byte[] bytes;
                try
                {
                    bytes = await http.GetByteArrayAsync(urlDescarga, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new HerramientaNoDisponibleException(
                        $"No se pudo descargar NxNandManager: {ex.Message}", ex);
                }

                await File.WriteAllBytesAsync(rutaZip, bytes, ct);

                // 3. Validar SHA-256 del ZIP descargado antes de extraerlo.
                string hashDescarga = new SHA256Logic().ObtenerHashArchivo(rutaZip);
                if (!string.Equals(hashDescarga, sha256Esperado, StringComparison.OrdinalIgnoreCase))
                {
                    throw new HerramientaNoDisponibleException(
                        "El hash SHA-256 de la descarga de NxNandManager no coincide con el esperado.");
                }

                // 4. Extraer el ZIP a una carpeta temporal dentro de la misma versión.
                bool extraido = await new ZipLogic().ExtraerTodoAsync(rutaZip, rutaExtraccion, ct: ct);
                if (!extraido)
                {
                    throw new HerramientaNoDisponibleException(
                        "No se pudo extraer el paquete ZIP de NxNandManager.");
                }

                // 5. Localizar el ejecutable dentro del contenido extraído (puede
                //    venir en una subcarpeta junto a sus DLLs, como dokan1.dll).
                string? rutaExeExtraido = Directory
                    .EnumerateFiles(rutaExtraccion, nombreEjecutable, SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (rutaExeExtraido == null)
                {
                    throw new HerramientaNoDisponibleException(
                        $"El ZIP de NxNandManager no contiene el ejecutable esperado ({nombreEjecutable}).");
                }

                // 6. Mover TODO el contenido de la carpeta que contiene al ejecutable
                //    (no solo el .exe), ya que el CLI depende de DLLs vecinas
                //    (p.ej. dokan1.dll) para funcionar correctamente.
                string carpetaOrigen = Path.GetDirectoryName(rutaExeExtraido) ?? rutaExtraccion;
                foreach (string archivoOrigen in Directory.EnumerateFiles(carpetaOrigen, "*", SearchOption.AllDirectories))
                {
                    string rutaRelativa = Path.GetRelativePath(carpetaOrigen, archivoOrigen);
                    string archivoDestino = Path.Combine(rutaVersion, rutaRelativa);

                    string? subcarpetaDestino = Path.GetDirectoryName(archivoDestino);
                    if (!string.IsNullOrEmpty(subcarpetaDestino))
                        Directory.CreateDirectory(subcarpetaDestino);

                    File.Move(archivoOrigen, archivoDestino, overwrite: true);
                }

                return rutaExeFinal;
            }
            finally
            {
                try { if (File.Exists(rutaZip)) File.Delete(rutaZip); } catch { }
                try { if (Directory.Exists(rutaExtraccion)) Directory.Delete(rutaExtraccion, true); } catch { }
            }
        }

        /// <summary>
        /// Convierte una URL de GitHub en formato "blob" (vista web, HTML) en su
        /// equivalente "raw" (contenido binario directo), necesaria para descargar
        /// el archivo con <see cref="HttpClient"/>. Si la URL ya es directa
        /// (raw.githubusercontent.com u otro host), se devuelve sin cambios.
        /// </summary>
        private static string NormalizarUrlDescargaDirecta(string url)
        {
            if (url.Contains("github.com/", StringComparison.OrdinalIgnoreCase) &&
                url.Contains("/blob/", StringComparison.OrdinalIgnoreCase))
            {
                return url
                    .Replace("github.com/", "raw.githubusercontent.com/", StringComparison.OrdinalIgnoreCase)
                    .Replace("/blob/", "/", StringComparison.OrdinalIgnoreCase);
            }

            return url;
        }
    }
}

