using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NX_Suite.Studio
{
    /// <summary>
    /// Ajustes locales de NX-STUDIO guardados en %AppData%\NX-Studio\config.json,
    /// fuera del repositorio para que el token nunca se suba a GitHub.
    /// </summary>
    internal static class ConfiguracionStudio
    {
        private static readonly string Ruta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NX-Studio", "config.json");

        /// <summary>Token personal de GitHub (sube el límite de 60 a 5000 consultas/hora). Si no hay, usa GITHUB_TOKEN.</summary>
        public static string? TokenGitHub
        {
            get => Leer()["TokenGitHub"]?.GetValue<string>() is { Length: > 0 } t ? t : null;
            set
            {
                var o = Leer();
                o["TokenGitHub"] = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
                File.WriteAllText(Ruta, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        private static JsonObject Leer()
        {
            try { return File.Exists(Ruta) ? JsonNode.Parse(File.ReadAllText(Ruta)) as JsonObject ?? new() : new(); }
            catch (JsonException) { return new(); }
        }
    }
}
