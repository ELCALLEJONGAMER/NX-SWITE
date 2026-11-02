using NX_Swite.Core.Configuracion;
using NX_Swite.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

namespace NX_Swite.Core
{
    /// <summary>
    /// Servicio observable que mantiene el estado de la actualización de la app.
    /// Accesible desde XAML mediante <c>Servicios.Actualizacion</c>.
    /// </summary>
    public class ServicioActualizacion : INotifyPropertyChanged
    {
        private bool   _hayActualizacion;
        private string _versionActual   = string.Empty;
        private string _versionRemota   = string.Empty;
        private string _urlDescarga     = string.Empty;
        private string _notasVersion    = string.Empty;

        public bool HayActualizacion
        {
            get => _hayActualizacion;
            private set { _hayActualizacion = value; OnPropertyChanged(); }
        }

        public string VersionActual
        {
            get => _versionActual;
            private set { _versionActual = value; OnPropertyChanged(); }
        }

        public string VersionRemota
        {
            get => _versionRemota;
            private set { _versionRemota = value; OnPropertyChanged(); }
        }

        public string UrlDescarga
        {
            get => _urlDescarga;
            private set { _urlDescarga = value; OnPropertyChanged(); }
        }

        public string NotasVersion
        {
            get => _notasVersion;
            private set { _notasVersion = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Evalúa si hay una versión más reciente disponible y actualiza las propiedades.
        /// </summary>
        public void Evaluar(string versionRemota, string urlDescarga, string notas)
        {
            AplicarSimulacionLocal(ref versionRemota, ref urlDescarga, ref notas);

            VersionActual    = ConfiguracionLocal.VersionActual;
            VersionRemota    = versionRemota ?? string.Empty;
            UrlDescarga      = urlDescarga   ?? string.Empty;
            NotasVersion     = notas         ?? string.Empty;
            HayActualizacion = GestorActualizacion.EsVersionNueva(VersionActual, VersionRemota);
        }

        private const string ApiReleases = "https://api.github.com/repos/ELCALLEJONGAMER/NX-SWITE/releases?per_page=10";

        /// <summary>
        /// Busca la release más reciente del repositorio (incluidas prereleases) que
        /// tenga un ZIP adjunto y usa su versión, enlace y descripción. Si GitHub no
        /// responde se conservan los datos del catálogo.
        /// </summary>
        public async Task EvaluarDesdeGitHubAsync(string respaldoVersion, string respaldoUrl, string respaldoNotas)
        {
            string version = respaldoVersion, url = respaldoUrl, notas = respaldoNotas;
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("NX-Swite");
                using var doc = JsonDocument.Parse(await http.GetStringAsync(ApiReleases));
                foreach (var r in doc.RootElement.EnumerateArray())
                {
                    if (r.GetProperty("draft").GetBoolean()) continue;
                    var m = Regex.Match(r.GetProperty("tag_name").GetString() ?? "", @"\d+(\.\d+){1,3}");
                    var zip = r.GetProperty("assets").EnumerateArray()
                        .FirstOrDefault(a => (a.GetProperty("name").GetString() ?? "").EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                    if (!m.Success || zip.ValueKind == JsonValueKind.Undefined) continue;

                    version = m.Value;
                    url     = zip.GetProperty("browser_download_url").GetString() ?? url;
                    notas   = r.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                    break;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"No se pudo consultar GitHub Releases; se usa el catálogo: {ex.Message}");
            }
            Evaluar(version, url, notas);
        }

        /// <summary>
        /// Simulación de actualización solo para el desarrollador: si existe
        /// <c>%AppData%\NX-Swite\actualizacion-prueba.json</c> con
        /// <c>{ "version": "9.9.9", "url": "C:\\ruta\\update.zip", "notas": "..." }</c>
        /// se usa en lugar de los datos del catálogo. La url admite ruta local o http(s).
        /// </summary>
        private static void AplicarSimulacionLocal(ref string versionRemota, ref string urlDescarga, ref string notas)
        {
            string ruta = Path.Combine(ConfiguracionLocal.RutaAppData, "actualizacion-prueba.json");
            if (!File.Exists(ruta)) return;

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(ruta));
                var raiz = doc.RootElement;
                if (raiz.TryGetProperty("version", out var v)) versionRemota = v.GetString() ?? versionRemota;
                if (raiz.TryGetProperty("url",     out var u)) urlDescarga   = u.GetString() ?? urlDescarga;
                if (raiz.TryGetProperty("notas",   out var n)) notas         = n.GetString() ?? notas;
                Logger.Warning($"Simulación de actualización activa ({ruta}): v{versionRemota} desde {urlDescarga}");
            }
            catch (Exception ex)
            {
                Logger.Error("No se pudo leer actualizacion-prueba.json", ex);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
