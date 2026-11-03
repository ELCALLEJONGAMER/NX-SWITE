using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NX_Suite.Catalogo;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Asistente de módulo nuevo en tres pasos:
    /// 1) Datos: se pega el enlace (repo o descarga) y se rellenan solos nombre, web, descripción, icono…
    /// 2) Descarga: se elige release/archivo (o URL directa) y se descarga.
    /// 3) Microsd: se ve y edita cómo cae cada archivo, qué va por SHA256, y se genera el pipeline.
    /// </summary>
    internal sealed class VistaAsistente : DockPanel
    {
        private static readonly string[] Pasos = { "Datos del módulo", "Descarga", "Archivos en la microSD" };

        private readonly DocumentoCatalogo _doc;
        private readonly Action _alCambiar;
        private readonly Action<JsonObject> _alCrear;
        private readonly StackPanel _indicador = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        private readonly ContentControl _cuerpo = new();
        private readonly Button _atras, _siguiente;
        private readonly TextBlock _error = new() { Foreground = Formularios.Pincel(Ui.Rojo), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        private int _paso;

        // Borrador
        private string _enlace = "", _repo = "", _url = "", _urlRelease = "", _version = "";
        private string _id = "", _nombre = "", _descripcion = "", _icono = "", _banner = "", _web = "";
        private readonly HashSet<string> _etiquetas = new(StringComparer.OrdinalIgnoreCase) { "homebrew" };
        private readonly HashSet<string> _dependencias = new(StringComparer.OrdinalIgnoreCase);
        private readonly JsonObject _compat = new();
        private SelectorDescarga? _selector;
        private EditorPaquete? _editor;
        private string _assetEditor = "";

        public VistaAsistente(DocumentoCatalogo doc, Action alCambiar, Action<JsonObject> alCrear, Action alCancelar)
        {
            _doc = doc;
            _alCambiar = alCambiar;
            _alCrear = alCrear;

            var cab = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var cancelar = Ui.Boton("✕ Cancelar", alCancelar);
            DockPanel.SetDock(cancelar, Dock.Right);
            cab.Children.Add(cancelar);
            var t = new StackPanel();
            t.Children.Add(Ui.Titulo("Nuevo módulo", 24));
            t.Children.Add(Ui.Texto("Pega el enlace, revisa los datos, elige la descarga y coloca los archivos. NX-STUDIO genera instalación, desinstalación y firma.", Ui.Tenue));
            cab.Children.Add(t);
            SetDock(cab, Dock.Top);
            Children.Add(cab);
            SetDock(_indicador, Dock.Top);
            Children.Add(_indicador);

            var pie = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            _siguiente = Ui.Boton("Siguiente ›", () => _ = AvanzarAsync(), primario: true);
            _atras = Ui.Boton("‹ Atrás", () => { _paso--; Pintar(); });
            DockPanel.SetDock(_siguiente, Dock.Right);
            DockPanel.SetDock(_atras, Dock.Right);
            pie.Children.Add(_siguiente);
            pie.Children.Add(_atras);
            pie.Children.Add(_error);
            SetDock(pie, Dock.Bottom);
            Children.Add(pie);

            Children.Add(new ScrollViewer { Content = _cuerpo, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 8, 0) });
            Pintar();
        }

        // ── Navegación ─────────────────────────────────────────────────────

        private void Pintar()
        {
            _error.Text = "";
            _indicador.Children.Clear();
            for (int i = 0; i < Pasos.Length; i++)
            {
                string color = i < _paso ? Ui.Verde : i == _paso ? Ui.Acento : "#4A4A58";
                _indicador.Children.Add(new Border
                {
                    BorderBrush = Formularios.Pincel(color), BorderThickness = new Thickness(0, 0, 0, 3),
                    Padding = new Thickness(4, 4, 18, 6), Margin = new Thickness(0, 0, 6, 0),
                    Child = new TextBlock
                    {
                        Text = (i < _paso ? "✓ " : $"{i + 1}  ") + Pasos[i], Foreground = Formularios.Pincel(color),
                        FontWeight = i == _paso ? FontWeights.Bold : FontWeights.Normal, FontSize = 13,
                    },
                });
            }
            _atras.Visibility = _paso > 0 ? Visibility.Visible : Visibility.Collapsed;
            _siguiente.Content = _paso switch { 0 => "💾 Guardar y elegir descarga ›", 1 => "⬇ Descargar y ver archivos ›", _ => "✓ Crear módulo" };
            _cuerpo.Content = _paso switch { 0 => PasoDatos(), 1 => PasoDescarga(), _ => PasoArchivos() };
        }

        private async Task AvanzarAsync()
        {
            string? problema = Validar();
            if (problema != null) { _error.Text = "⚠ " + problema; return; }
            if (_paso == 1)
            {
                _url = _selector!.Url;
                if (_selector.UrlRelease.Length > 0) _urlRelease = _selector.UrlRelease;
                if (_version.Length == 0) _version = _selector.VersionPropuesta;
                string asset = _selector.Asset;
                if (_editor == null || _assetEditor != _url)
                {
                    _siguiente.IsEnabled = false;
                    _error.Foreground = Formularios.Pincel(Ui.Tenue);
                    _error.Text = $"Descargando y analizando {asset}…";
                    try
                    {
                        var archivos = await CargaPaquete.CargarAsync(asset, _url, _id);
                        _editor = new EditorPaquete(asset, _id, archivos);
                        _assetEditor = _url;
                    }
                    catch (Exception ex)
                    {
                        _error.Foreground = Formularios.Pincel(Ui.Rojo);
                        _error.Text = "⚠ No se pudo descargar: " + ex.Message;
                        return;
                    }
                    finally { _siguiente.IsEnabled = true; _error.Foreground = Formularios.Pincel(Ui.Rojo); }
                }
            }
            if (_paso == Pasos.Length - 1) { Crear(); return; }
            _paso++;
            Pintar();
        }

        private string? Validar() => _paso switch
        {
            0 when string.IsNullOrWhiteSpace(_nombre) => "El nombre es obligatorio.",
            0 when !Regex.IsMatch(_id, "^[a-z0-9_\\-\\.]+$") => "Escribe un Id: solo minúsculas, números, '_', '-' y '.'.",
            0 when _doc.Lista("Modulos").OfType<JsonObject>().Any(m => string.Equals(DocumentoCatalogo.Texto(m["Id"]), _id, StringComparison.OrdinalIgnoreCase))
                => $"Ya existe un módulo con Id «{_id}».",
            1 when !Uri.TryCreate(_selector?.Url, UriKind.Absolute, out var u) || u.Scheme != "https" => "Elige un archivo o pega una URL https válida.",
            1 when string.IsNullOrWhiteSpace(_version) && string.IsNullOrWhiteSpace(_selector?.VersionPropuesta) => "Indica el número de versión en el paso 1.",
            2 => _editor?.Validar(),
            _ => null,
        };

        // ── Paso 1: datos ──────────────────────────────────────────────────

        private UIElement PasoDatos()
        {
            var p = new StackPanel();

            // Enlace
            var enlace = new TextBox { Text = _enlace };
            var leer = Ui.Boton("🔍 Leer enlace", () => { }, primario: true);
            var estado = Ui.Texto("", Ui.Tenue);
            var filaEnlace = new DockPanel();
            DockPanel.SetDock(leer, Dock.Right);
            leer.Margin = new Thickness(8, 0, 0, 0);
            filaEnlace.Children.Add(leer);
            filaEnlace.Children.Add(enlace);
            var bloqueEnlace = new StackPanel();
            bloqueEnlace.Children.Add(Ui.Campo("Enlace del repositorio o de la descarga", filaEnlace,
                "github.com/usuario/repo, URL de una release, o enlace directo a .zip/.7z/.rar/.nro/.ovl/.bin/.nsp. El tipo se decide por la extensión."));
            bloqueEnlace.Children.Add(estado);
            p.Children.Add(Ui.Tarjeta(bloqueEnlace, Ui.Acento));

            // Campos
            var nombre = new TextBox { Text = _nombre };
            var id = new TextBox { Text = _id, FontFamily = new FontFamily("Consolas") };
            var version = new TextBox { Text = _version };
            var web = new TextBox { Text = _web };
            var repo = new TextBox { Text = _repo };
            var desc = new TextBox { Text = _descripcion, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60 };
            var icono = new TextBox { Text = _icono };
            var banner = new TextBox { Text = _banner };
            var vIcono = new Image { Width = 72, Height = 72, Stretch = Stretch.UniformToFill };
            var vBanner = new Image { Height = 72, MaxWidth = 260, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
            Formularios.MostrarImagen(vIcono, _icono);
            Formularios.MostrarImagen(vBanner, _banner);

            nombre.TextChanged += (_, _) => _nombre = nombre.Text.Trim();
            id.TextChanged += (_, _) => _id = id.Text.Trim();
            version.TextChanged += (_, _) => _version = version.Text.Trim().TrimStart('v', 'V');
            web.TextChanged += (_, _) => _web = web.Text.Trim();
            repo.TextChanged += (_, _) => _repo = NormalizarRepo(repo.Text);
            desc.TextChanged += (_, _) => _descripcion = desc.Text;
            icono.TextChanged += (_, _) => { _icono = icono.Text.Trim(); Formularios.MostrarImagen(vIcono, _icono); };
            banner.TextChanged += (_, _) => { _banner = banner.Text.Trim(); Formularios.MostrarImagen(vBanner, _banner); };

            p.Children.Add(Ui.Seccion("IDENTIDAD"));
            p.Children.Add(Ui.Rejilla(4,
                Ui.Campo("Nombre visible", nombre),
                Ui.Campo("Id (único, sin espacios)", id, "Escríbelo tú; no debe cambiar nunca."),
                Ui.Campo("Versión", version, "Sin «v». Se propone desde la release."),
                Ui.Campo("Repositorio GitHub (usuario/repo)", repo),
                Ui.Ancho(Ui.Campo("Web oficial", web), 2),
                Ui.Ancho(Ui.Campo("Descripción", desc), 2)));

            p.Children.Add(Ui.Seccion("IMÁGENES"));
            var marcoIcono = new Border { Width = 72, Height = 72, CornerRadius = new CornerRadius(10), Background = Formularios.Pincel("#1A1A24"), Child = vIcono, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            var bIcono = new StackPanel(); bIcono.Children.Add(Ui.Campo("Icono (URL)", icono)); bIcono.Children.Add(marcoIcono);
            var bBanner = new StackPanel(); bBanner.Children.Add(Ui.Campo("Banner (URL, opcional)", banner)); bBanner.Children.Add(vBanner);
            p.Children.Add(Ui.Rejilla(2, bIcono, bBanner));

            p.Children.Add(Ui.Seccion("FILTRADO Y REQUISITOS"));
            var deps = new List<string> { "atmosphere_mod or atmosphere" };
            deps.AddRange(_doc.Lista("Modulos").OfType<JsonObject>()
                .SelectMany(m => (m["Dependencias"] as JsonArray)?.Select(d => DocumentoCatalogo.Texto(d)) ?? Enumerable.Empty<string>())
                .Where(d => d.Length > 0).GroupBy(d => d).OrderByDescending(x => x.Count()).Select(x => x.Key).Take(8));
            p.Children.Add(Ui.Rejilla(2,
                Ui.Campo("Etiquetas", Chips(EtiquetasConocidas(), _etiquetas)),
                Ui.Campo("Requiere", Chips(deps.Distinct().ToList(), _dependencias))));

            p.Children.Add(Ui.Seccion("VERSIONES COMPATIBLES"));
            p.Children.Add(Ui.Rejilla(2,
                EditorRestriccion.Crear(_compat, "Firmware", "Firmware de la consola", VersionesDe("firmware"), () => { }),
                EditorRestriccion.Crear(_compat, "Atmos", "Atmosphere", VersionesDe("atmosphere_mod", "atmosphere"), () => { })));

            enlace.TextChanged += (_, _) => _enlace = enlace.Text.Trim();
            leer.Click += async (_, _) =>
            {
                leer.IsEnabled = false;
                estado.Text = "Leyendo enlace…";
                try
                {
                    await LeerEnlaceAsync();
                    estado.Text = _repo.Length > 0 ? $"✓ Repositorio {_repo}. Revisa los datos rellenados." : "✓ Descarga directa detectada. Completa los datos.";
                    nombre.Text = _nombre; version.Text = _version; web.Text = _web; repo.Text = _repo;
                    desc.Text = _descripcion; icono.Text = _icono; banner.Text = _banner;
                }
                catch (Exception ex) { estado.Text = "Error: " + ex.Message; }
                finally { leer.IsEnabled = true; }
            };
            return p;
        }

        /// <summary>Detecta repo/descarga y rellena los campos vacíos.</summary>
        private async Task LeerEnlaceAsync()
        {
            string e = _enlace;
            var gh = Regex.Match(e, @"github\.com/(?<r>[^/\s]+/[^/\s#?]+)", RegexOptions.IgnoreCase);
            string asset = SelectorDescarga.NombreDesdeUrl(e);
            bool esArchivo = Regex.IsMatch(asset, @"\.(zip|7z|rar|nro|ovl|bin|nsp|nsz|xci)$", RegexOptions.IgnoreCase);
            if (esArchivo) _url = e;

            if (gh.Success)
            {
                _repo = gh.Groups["r"].Value.Replace(".git", "");
                var tag = Regex.Match(e, @"/(?:releases/(?:tag|download)/)(?<t>[^/]+)");
                if (tag.Success && _version.Length == 0) _version = Uri.UnescapeDataString(tag.Groups["t"].Value).TrimStart('v', 'V');
                if (_web.Length == 0) _web = $"https://github.com/{_repo}";
                string nombreRepo = _repo.Split('/').Last();
                if (_nombre.Length == 0) _nombre = nombreRepo;
                if (_icono.Length == 0) _icono = $"https://github.com/{_repo.Split('/')[0]}.png";
                using var cli = new GitHubReleases(ConfiguracionStudio.TokenGitHub);
                try
                {
                    using var resp = await cli.Http.GetAsync($"https://api.github.com/repos/{_repo}");
                    if (resp.IsSuccessStatusCode)
                    {
                        var j = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
                        if (_descripcion.Length == 0) _descripcion = DocumentoCatalogo.Texto(j?["description"]);
                        string home = DocumentoCatalogo.Texto(j?["homepage"]);
                        if (home.StartsWith("http") && _web == $"https://github.com/{_repo}") _web = home;
                    }
                }
                catch { /* Datos opcionales */ }
                if (_version.Length == 0 && await cli.ObtenerAsync(_repo) is { Count: > 0 } rel)
                    _version = (rel.FirstOrDefault(r => !r.Prerelease) ?? rel[0]).Tag.TrimStart('v', 'V');
            }
            else if (Uri.TryCreate(e, UriKind.Absolute, out var u))
            {
                if (_web.Length == 0) _web = $"{u.Scheme}://{u.Host}";
                if (_nombre.Length == 0 && esArchivo) _nombre = Regex.Replace(System.IO.Path.GetFileNameWithoutExtension(asset), @"[-_ ]?v?\d+(\.\d+)+.*$", "");
            }
        }

        private IEnumerable<string> VersionesDe(params string[] ids) =>
            _doc.Lista("Modulos").OfType<JsonObject>()
                .Where(m => ids.Contains(DocumentoCatalogo.Texto(m["Id"])))
                .SelectMany(m => (m["Versiones"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
                .Select(v => DocumentoCatalogo.Texto(v["Version"]).Split('+')[0].Trim())
                .Where(v => v.Length > 0);

        private List<string> EtiquetasConocidas() =>
            _doc.Lista("Modulos").OfType<JsonObject>()
                .SelectMany(m => (m["Etiquetas"] as JsonArray)?.Select(e => DocumentoCatalogo.Texto(e)) ?? Enumerable.Empty<string>())
                .Where(e => e.Length > 0).GroupBy(e => e, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count()).Select(g => g.Key).Take(14).ToList();

        private static WrapPanel Chips(List<string> opciones, HashSet<string> seleccion)
        {
            var w = new WrapPanel();
            foreach (var o in opciones.Union(seleccion, StringComparer.OrdinalIgnoreCase))
            {
                string valor = o;
                w.Children.Add(Ui.Chip(valor, seleccion.Contains(valor), on => { if (on) seleccion.Add(valor); else seleccion.Remove(valor); }));
            }
            return w;
        }

        private static string NormalizarRepo(string texto)
        {
            var m = Regex.Match(texto.Trim(), @"(?:github\.com/)?(?<r>[^/\s]+/[^/\s#?]+)");
            return m.Success ? m.Groups["r"].Value.Replace(".git", "") : "";
        }

        // ── Paso 2: descarga ───────────────────────────────────────────────

        private UIElement PasoDescarga()
        {
            var p = new StackPanel();
            p.Children.Add(Ui.Seccion("¿QUÉ ARCHIVO SE DESCARGA?"));
            _selector = new SelectorDescarga(_repo, _url);
            p.Children.Add(_selector);
            p.Children.Add(Formularios.Ayuda("Comprimidos (.zip/.7z/.rar) se extraen; los sueltos (.nro/.ovl/.bin/.nsp) se copian a su carpeta habitual. Todo se puede ajustar en el siguiente paso."));
            return p;
        }

        // ── Paso 3: archivos ───────────────────────────────────────────────

        private UIElement PasoArchivos()
        {
            var p = new StackPanel();
            p.Children.Add(Ui.Seccion("CÓMO CAEN LOS ARCHIVOS EN LA MICROSD"));
            p.Children.Add(Formularios.Ayuda("Edita el destino para renombrar o mover, desmarca para excluir y pulsa el chip para firmar por SHA256 (el resto, por presencia)."));
            if (_editor != null)
            {
                if (_editor.Parent is Panel viejo) viejo.Children.Remove(_editor);
                p.Children.Add(_editor);
            }
            return p;
        }

        // ── Generación ─────────────────────────────────────────────────────

        private void Crear()
        {
            if (_editor == null) return;
            string asset = SelectorDescarga.NombreDesdeUrl(_url);
            var (inst, desins) = MapaPaquete.GenerarPipelines(_url, asset, $"{_id}-{_version}", _editor.Archivos, _editor.LimpiarCache);
            var version = new JsonObject
            {
                ["Version"] = _version, ["SoloDeteccion"] = false,
                ["PipelineInstalacion"] = inst, ["PipelineDesinstalacion"] = desins,
            };
            foreach (var k in new[] { "Firmware", "Atmos" })
                if (_compat[k] is JsonNode n) version[k] = n.DeepClone();

            var modulo = new JsonObject
            {
                ["Id"] = _id, ["Nombre"] = _nombre, ["Descripcion"] = _descripcion,
                ["IconoUrl"] = _icono, ["BannerUrl"] = _banner,
                ["UrlOficial"] = _web.Length > 0 ? _web : _urlRelease,
                ["GitHubRepo"] = _repo,
                ["Etiquetas"] = new JsonArray(_etiquetas.Select(e => (JsonNode)JsonValue.Create(e)!).ToArray()),
                ["Complementos"] = new JsonArray(),
                ["Dependencias"] = new JsonArray(_dependencias.Select(e => (JsonNode)JsonValue.Create(e)!).ToArray()),
                ["IncompatibleCon"] = new JsonArray(),
                ["Versiones"] = new JsonArray(version),
                ["FirmasDeteccion"] = new JsonArray(MapaPaquete.GenerarFirma(_version, _editor.Archivos)),
                ["RutasDesinstalacion"] = new JsonArray(),
            };
            _doc.Lista("Modulos").Add(modulo);
            _alCambiar();
            _alCrear(modulo);
        }
    }
}
