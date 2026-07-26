using System.Text.Json.Serialization;

namespace NX_Swite.Models
{
    /// <summary>
    /// Sección independiente del Gist JSON, mapeada a la raíz <c>"tools"</c>.
    /// Agrupa la configuración de herramientas externas administradas por
    /// NX-Swite (descargadas, cacheadas y validadas por hash), separada de
    /// <see cref="ConfiguracionUI"/> porque no describe apariencia sino binarios.
    /// </summary>
    public class ToolsConfig
    {
        /// <summary>URL de descarga del paquete (ZIP) del CLI de NxNandManager.</summary>
        [JsonPropertyName("CLI_NX_NAND_MANAGER")]
        public string CliNxNandManagerUrl { get; set; } = string.Empty;

        /// <summary>SHA-256 esperado del ZIP descargado del CLI de NxNandManager.</summary>
        [JsonPropertyName("CLI_NX_NAND_MANAGER_SHA256")]
        public string CliNxNandManagerSha256 { get; set; } = string.Empty;

        /// <summary>Nombre de archivo del ZIP descargado (para caché en disco).</summary>
        [JsonPropertyName("CLI_NX_NAND_MANAGER_FILENAME")]
        public string CliNxNandManagerFilename { get; set; } = string.Empty;

        /// <summary>Nombre del ejecutable dentro del ZIP tras extraerlo (ej. "NxNandManager.exe").</summary>
        [JsonPropertyName("CLI_NX_NAND_MANAGER_EXECUTABLE")]
        public string CliNxNandManagerExecutable { get; set; } = string.Empty;

        /// <summary>Versión publicada del CLI de NxNandManager (para caché y subcarpeta).</summary>
        [JsonPropertyName("CLI_NX_NAND_MANAGER_VERSION")]
        public string CliNxNandManagerVersion { get; set; } = string.Empty;
    }
}
