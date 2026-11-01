using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NX_Suite.Catalogo
{
    /// <summary>
    /// Caché en disco del análisis de cada paquete descargado (rutas internas y SHA256),
    /// no del archivo en sí: el firmware ocupa cientos de MB y su análisis unos KB.
    /// Antes de reutilizarlo se hace un HEAD y se compara la huella (ETag / tamaño /
    /// fecha); si el asset se resubió, la huella cambia y se vuelve a descargar.
    /// </summary>
    internal static class CachePaquetes
    {
        internal sealed record Entrada(string Url, string Huella, List<string> Rutas, Dictionary<string, string> Hashes);

        public static string Carpeta { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NX-Studio", "cache", "paquetes");

        public static async Task<string?> HuellaAsync(HttpClient http, string url)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, url);
                using var r = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                if (!r.IsSuccessStatusCode) return null;
                string etag = r.Headers.ETag?.Tag ?? "";
                string largo = r.Content.Headers.ContentLength?.ToString() ?? "";
                string fecha = r.Content.Headers.LastModified?.ToUnixTimeSeconds().ToString() ?? "";
                return etag.Length + largo.Length + fecha.Length == 0 ? null : $"{etag}|{largo}|{fecha}";
            }
            catch { return null; }
        }

        public static Entrada? Leer(string url, string huella)
        {
            try
            {
                string ruta = Ruta(url);
                if (!File.Exists(ruta)) return null;
                var e = JsonSerializer.Deserialize<Entrada>(File.ReadAllText(ruta, Encoding.UTF8));
                return e != null && e.Url == url && e.Huella == huella ? e : null;
            }
            catch { return null; }
        }

        public static void Guardar(Entrada e)
        {
            try
            {
                Directory.CreateDirectory(Carpeta);
                File.WriteAllText(Ruta(e.Url), JsonSerializer.Serialize(e), new UTF8Encoding(false));
            }
            catch { /* la caché es opcional */ }
        }

        public static void Vaciar()
        {
            try { if (Directory.Exists(Carpeta)) Directory.Delete(Carpeta, true); } catch { }
        }

        private static string Ruta(string url) =>
            Path.Combine(Carpeta, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..32] + ".json");
    }
}
