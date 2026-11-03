using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using NX_Suite.Catalogo;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Elección del archivo a descargar: releases y assets de GitHub (si hay
    /// repositorio) o una URL directa. Expone la URL, el nombre del asset y la
    /// versión propuesta (tag sin «v»).
    /// </summary>
    internal sealed class SelectorDescarga : StackPanel
    {
        private readonly ComboBox _releases = new() { IsEnabled = false };
        private readonly ComboBox _assets = new() { IsEnabled = false };
        private readonly TextBox _url = new();
        private readonly TextBlock _estado = Ui.Texto("", Ui.Tenue);
        private List<ReleaseGitHub>? _lista;

        public string Url => _url.Text.Trim();
        public string Asset => NombreDesdeUrl(Url);
        public string VersionPropuesta { get; private set; } = "";
        public string UrlRelease { get; private set; } = "";
        public event Action? Cambiado;

        public SelectorDescarga(string repo, string urlInicial)
        {
            _url.Text = urlInicial;
            Children.Add(Ui.Rejilla(3,
                Ui.Campo("Release", _releases),
                Ui.Campo("Archivo de la release", _assets, "Se prefieren .zip/.7z/.rar/.nro/.ovl/.bin/.nsp."),
                Ui.Campo("URL de descarga (directa)", _url, "Puedes pegar cualquier enlace https.")));
            Children.Add(_estado);

            _releases.SelectionChanged += (_, _) => LlenarAssets();
            _assets.SelectionChanged += (_, _) =>
            {
                if (_assets.Tag is List<AssetGitHub> l && _assets.SelectedIndex >= 0) _url.Text = l[_assets.SelectedIndex].Url;
            };
            _url.TextChanged += (_, _) => Cambiado?.Invoke();
            if (repo.Length > 0) _ = CargarAsync(repo);
            else _estado.Text = "Sin repositorio de GitHub: pega la URL directa del archivo.";
        }

        private static readonly string[] Preferidas = { ".zip", ".7z", ".rar", ".nro", ".ovl", ".bin", ".nsp" };

        private async Task CargarAsync(string repo)
        {
            _estado.Text = $"Consultando releases de {repo}…";
            try
            {
                using var gh = new GitHubReleases(ConfiguracionStudio.TokenGitHub);
                var r = await gh.ObtenerAsync(repo);
                if (r == null || r.Count == 0) { _estado.Text = "⚠ El repositorio no existe o no tiene releases: usa la URL directa."; return; }
                _lista = r.ToList();
                _releases.ItemsSource = _lista.Select(x => $"{x.Tag}{(x.Prerelease ? "  (pre)" : "")}  ·  {x.Publicada:dd/MM/yyyy}").ToList();
                _releases.IsEnabled = true;
                // Si la URL actual pertenece a una release, se selecciona esa; si no, la última estable.
                int actual = _lista.FindIndex(x => x.Assets.Any(a => a.Url == Url));
                _releases.SelectedIndex = actual >= 0 ? actual : Math.Max(0, _lista.FindIndex(x => !x.Prerelease));
                _estado.Text = $"✓ {_lista.Count} release(s).";
            }
            catch (Exception ex) { _estado.Text = "Error consultando GitHub: " + ex.Message; }
        }

        private void LlenarAssets()
        {
            if (_lista == null || _releases.SelectedIndex < 0) return;
            var r = _lista[_releases.SelectedIndex];
            var assets = r.Assets
                .OrderBy(a => Array.FindIndex(Preferidas, e => a.Nombre.EndsWith(e, StringComparison.OrdinalIgnoreCase)) is var i && i < 0 ? 99 : i)
                .ToList();
            string urlPrevia = Url;
            _assets.ItemsSource = assets.Select(a => a.Nombre).ToList();
            _assets.Tag = assets;
            _assets.IsEnabled = assets.Count > 0;
            VersionPropuesta = r.Tag.TrimStart('v', 'V');
            UrlRelease = r.Url ?? "";
            int previa = assets.FindIndex(a => a.Url == urlPrevia);
            _assets.SelectedIndex = assets.Count == 0 ? -1 : Math.Max(0, previa);
            if (assets.Count == 0) _estado.Text = "⚠ Esta release no tiene archivos adjuntos.";
            Cambiado?.Invoke();
        }

        public static string NombreDesdeUrl(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var u) ? Uri.UnescapeDataString(System.IO.Path.GetFileName(u.AbsolutePath)) : "";
    }

    /// <summary>Descarga un asset y prepara el mapa de archivos, reutilizando lo que ya sabe la versión existente.</summary>
    internal static class CargaPaquete
    {
        public static async Task<List<ArchivoMapa>> CargarAsync(string asset, string url, string idModulo,
                                                               JsonObject? modulo = null, JsonObject? version = null)
        {
            using var gh = new GitHubReleases(ConfiguracionStudio.TokenGitHub);
            var act = new ActualizadorCatalogo(gh, validarPaquetes: true);
            var paquete = await act.LeerAssetAsync(asset, url);

            // Destinos y SHA de la versión actual (simulando su pipeline sobre el paquete nuevo).
            var previos = new List<ArchivoAnalizado>();
            var conSha = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var enFirma = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (version != null)
            {
                var copia = (JsonObject)version.DeepClone();
                var descarga = (copia["PipelineInstalacion"] as JsonArray)?.OfType<JsonObject>()
                    .FirstOrDefault(p => DocumentoCatalogo.Texto(p["TipoAccion"]) == "DESCARGAR");
                if (descarga?["Parametros"] is JsonObject par) par["Url"] = url;
                try { previos = (await act.AnalizarVersionAsync(copia)).Archivos; } catch { /* sin pipeline válido: destinos por defecto */ }

                string ver = DocumentoCatalogo.Texto(version["Version"]);
                foreach (var f in (modulo?["FirmasDeteccion"] as JsonArray)?.OfType<JsonObject>()
                                  .Where(f => DocumentoCatalogo.Texto(f["Version"]) == ver) ?? Enumerable.Empty<JsonObject>())
                    foreach (var a in (f["Archivos"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
                    {
                        string r = MapaPaquete.Normalizar(DocumentoCatalogo.Texto(a["Ruta"]));
                        enFirma.Add(r);
                        if (!string.IsNullOrWhiteSpace(DocumentoCatalogo.Texto(a["SHA256"]))) conSha.Add(r);
                    }
            }

            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lista = new List<ArchivoMapa>();
            foreach (var (ruta, sha) in paquete)
            {
                // Mismo contenido y, si hay varios, mismo nombre de archivo.
                var candidatos = previos.Where(p => !p.Generado && !usados.Contains(p.Ruta) &&
                                                    string.Equals(p.Sha256, sha, StringComparison.OrdinalIgnoreCase)).ToList();
                var previo = candidatos.FirstOrDefault(p => System.IO.Path.GetFileName(p.Ruta).Equals(System.IO.Path.GetFileName(ruta), StringComparison.OrdinalIgnoreCase))
                             ?? candidatos.FirstOrDefault();
                var a = new ArchivoMapa { Origen = ruta, Sha = sha };
                if (previo != null)
                {
                    usados.Add(previo.Ruta);
                    a.Destino = MapaPaquete.Normalizar(previo.Ruta);
                    a.UsarSha = conSha.Contains(a.Destino);
                }
                else
                {
                    a.Destino = MapaPaquete.DestinoPorDefecto(asset, ruta, idModulo);
                    a.Incluir = previos.Count == 0;
                    string ext = System.IO.Path.GetExtension(ruta).ToLowerInvariant();
                    a.UsarSha = version == null && ext is ".nro" or ".ovl" or ".bin" or ".nsp";
                }
                lista.Add(a);
            }
            return lista;
        }
    }
}
