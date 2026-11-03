using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NX_Suite.Catalogo;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Editor de módulos: lista con buscador, ficha, versiones, pasos de
    /// instalación y simulación del árbol final de la microSD.
    /// </summary>
    internal sealed partial class VistaModulos : Grid
    {
        private static readonly string[] TiposPaso =
        {
            "DESCARGAR", "EXTRAER", "COPIARSD", "MOVERARCHIVO", "LIMPIAR_CACHE",
            "CREARINI", "CREARTXT", "BORRARCARPETAS",
        };

        /// <summary>Parámetros por defecto al crear un paso nuevo de cada tipo.</summary>
        private static JsonObject ParametrosPorDefecto(string tipo) => tipo switch
        {
            "DESCARGAR"      => new() { ["Url"] = "https://", ["ArchivoDestino"] = "" },
            "EXTRAER"        => new() { ["ArchivoZip"] = "", ["CarpetaDestinoTemp"] = "" },
            "COPIARSD"       => new() { ["OrigenTemp"] = "", ["DestinoSD"] = "/" },
            "MOVERARCHIVO"   => new() { ["Origen"] = "", ["Destino"] = "", ["IgnorarSiNoExiste"] = false },
            "LIMPIAR_CACHE"  => new() { ["ArchivoZip"] = "" },
            "CREARINI"       => new() { ["RutaSD"] = "", ["SobreescribirSiExiste"] = false, ["Secciones"] = new JsonArray() },
            "CREARTXT"       => new() { ["RutaSD"] = "", ["Contenido"] = "", ["SobreescribirSiExiste"] = false },
            "BORRARCARPETAS" => new() { ["CarpetasSD"] = new JsonArray() },
            _                => new(),
        };

        private static readonly Campo[] CamposModulo =
        {
            new("Nombre", "Nombre visible"),
            new("Id", "Id (único, sin espacios)"),
            new("GitHubRepo", "Repositorio GitHub (usuario/repo)"),
            new("UrlOficial", "Web oficial"),
            new("IconoUrl", "Icono", TipoCampo.Imagen),
            new("BannerUrl", "Banner", TipoCampo.Imagen),
            new("Descripcion", "Descripción", TipoCampo.Multilinea),
            new("Etiquetas", "Etiquetas (coma)", TipoCampo.ListaComas),
            new("Dependencias", "Dependencias (Ids)", TipoCampo.ListaComas),
            new("Complementos", "Complementos (Ids)", TipoCampo.ListaComas),
            new("IncompatibleCon", "Incompatible con (Ids)", TipoCampo.ListaComas),
        };

        private readonly DocumentoCatalogo _doc;
        private readonly Action _alCambiar;
        private readonly TextBox _buscar = new() { Margin = new Thickness(0, 0, 0, 6) };
        private readonly ListBox _lista = new();
        private readonly ContentControl _ficha = new();
        private List<JsonObject> _filtrados = new();

        private VistaModulos(DocumentoCatalogo doc, Action alCambiar, bool soloFicha)
        {
            _doc = doc;
            _alCambiar = alCambiar;
        }

        public VistaModulos(DocumentoCatalogo doc, Action alCambiar)
        {
            _doc = doc;
            _alCambiar = alCambiar;

            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var izquierda = new DockPanel { Margin = new Thickness(0, 0, 16, 0) };
            var cab = new StackPanel();
            cab.Children.Add(Formularios.Titulo("Módulos"));
            cab.Children.Add(new TextBlock { Text = "Buscar:", Style = (Style)Application.Current.Resources["Etiqueta"] });
            cab.Children.Add(_buscar);
            DockPanel.SetDock(cab, Dock.Top);
            izquierda.Children.Add(cab);

            var botones = new UniformGrid4(
                Boton("+ Nuevo", () => NuevoModulo(vacio: true)),
                Boton("⧉ Duplicar", () => NuevoModulo(vacio: false)),
                new BotonPeligro("× Eliminar", EliminarModulo));
            DockPanel.SetDock(botones, Dock.Bottom);
            izquierda.Children.Add(botones);
            izquierda.Children.Add(_lista);
            Children.Add(izquierda);

            SetColumn(_ficha, 1);
            Children.Add(_ficha);

            _buscar.TextChanged += (_, _) => Filtrar(null);
            _lista.SelectionChanged += (_, _) => MostrarModulo();
            Filtrar(null);
        }

        private JsonArray Modulos => _doc.Lista("Modulos");

        /// <summary>Versiones publicadas en el catálogo de los módulos indicados (sin sufijos tipo "+v2").</summary>
        private IEnumerable<string> VersionesDeModulo(params string[] ids) =>
            Modulos.OfType<JsonObject>()
                .Where(m => ids.Contains(DocumentoCatalogo.Texto(m["Id"])))
                .SelectMany(m => (m["Versiones"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
                .Select(v => DocumentoCatalogo.Texto(v["Version"]).Split('+')[0].Trim())
                .Where(v => v.Length > 0);

        private static string Rotulo(JsonObject m)
        {
            string ver = (m["Versiones"] as JsonArray)?.FirstOrDefault() is JsonObject v ? DocumentoCatalogo.Texto(v["Version"]) : "—";
            return $"{DocumentoCatalogo.Texto(m["Nombre"])}   [{ver}]";
        }

        private void Filtrar(JsonObject? seleccionar)
        {
            string q = _buscar.Text.Trim();
            _filtrados = Modulos.OfType<JsonObject>()
                .Where(m => q.Length == 0 ||
                            DocumentoCatalogo.Texto(m["Nombre"]).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            DocumentoCatalogo.Texto(m["Id"]).Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();
            _lista.ItemsSource = _filtrados.Select(Rotulo).ToList();
            _lista.SelectedIndex = seleccionar != null ? _filtrados.IndexOf(seleccionar) : (_filtrados.Count > 0 ? 0 : -1);
        }

        private JsonObject? Actual => _lista.SelectedIndex >= 0 && _lista.SelectedIndex < _filtrados.Count
            ? _filtrados[_lista.SelectedIndex] : null;

        // ── Ficha del módulo ───────────────────────────────────────────────

        private void MostrarModulo()
        {
            _ficha.Content = Actual is { } m ? CrearFicha(m) : null;
        }

        private UIElement CrearEditorVersion(JsonObject version)
        {
            var panel = new StackPanel();
            panel.Children.Add(Formularios.CrearCompacto(version, new[]
            {
                new Campo("Version", "Número de versión"),
                new Campo("SoloDeteccion", "Solo detección (no se instala desde la app)", TipoCampo.Booleano),
            }, () => { _alCambiar(); RefrescarRotulo(); }));

            panel.Children.Add(Separador("VERSIONES COMPATIBLES"));
            panel.Children.Add(Ui.Rejilla(2,
                EditorRestriccion.Crear(version, "Firmware", "Firmware de la consola",
                    VersionesDeModulo("firmware"), _alCambiar),
                EditorRestriccion.Crear(version, "Atmos", "Atmosphere",
                    VersionesDeModulo("atmosphere_mod", "atmosphere"), _alCambiar)));

            if (version["PipelineInstalacion"] is not JsonArray pasos) version["PipelineInstalacion"] = pasos = new JsonArray();

            panel.Children.Add(Separador("PASOS DE INSTALACIÓN"));
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var listaPasos = new ListBox { Height = 260 };
            var izquierda = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            izquierda.Children.Add(listaPasos);
            var nuevoTipo = new ComboBox { ItemsSource = TiposPaso, SelectedIndex = 0, Margin = new Thickness(0, 6, 0, 0) };
            izquierda.Children.Add(nuevoTipo);
            izquierda.Children.Add(new UniformGrid4(
                Boton("↑", () => MoverPaso(-1)), Boton("↓", () => MoverPaso(1)),
                Boton("+ Añadir paso", () => AnadirPaso()), new BotonPeligro("× Quitar paso", QuitarPaso)));
            grid.Children.Add(izquierda);

            var detallePaso = new ContentControl();
            SetColumn(detallePaso, 1);
            grid.Children.Add(detallePaso);
            panel.Children.Add(grid);

            void Refrescar(int sel)
            {
                Renumerar(pasos);
                listaPasos.ItemsSource = pasos.OfType<JsonObject>().Select(DescribirPaso).ToList();
                listaPasos.SelectedIndex = Math.Min(sel, pasos.Count - 1);
            }
            void MoverPaso(int d)
            {
                int i = listaPasos.SelectedIndex, j = i + d;
                if (i < 0 || j < 0 || j >= pasos.Count) return;
                var n = pasos[i]!; pasos.RemoveAt(i); pasos.Insert(j, n);
                _alCambiar(); Refrescar(j);
            }
            void AnadirPaso()
            {
                string tipo = (string)nuevoTipo.SelectedItem;
                int pos = listaPasos.SelectedIndex + 1;
                pasos.Insert(pos, new JsonObject
                {
                    ["Paso"] = 0, ["TipoAccion"] = tipo, ["MensajeUI"] = "", ["Parametros"] = ParametrosPorDefecto(tipo),
                });
                _alCambiar(); Refrescar(pos);
            }
            void QuitarPaso()
            {
                int i = listaPasos.SelectedIndex;
                if (i < 0) return;
                pasos.RemoveAt(i);
                _alCambiar(); Refrescar(Math.Max(0, i - 1));
            }

            listaPasos.SelectionChanged += (_, _) =>
            {
                int i = listaPasos.SelectedIndex;
                if (i < 0 || pasos[i] is not JsonObject paso) { detallePaso.Content = null; return; }
                detallePaso.Content = CrearEditorPaso(paso, () =>
                {
                    _alCambiar();
                    var items = (List<string>)listaPasos.ItemsSource;
                    items[i] = DescribirPaso(paso);
                    listaPasos.Items.Refresh();
                });
            };
            Refrescar(0);

            return panel;
        }

        private static UIElement CrearEditorPaso(JsonObject paso, Action alCambiar)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = DocumentoCatalogo.Texto(paso["TipoAccion"]),
                Foreground = Formularios.Pincel("#00D2FF"), FontWeight = FontWeights.Bold,
            });
            panel.Children.Add(Formularios.Crear(paso, new[] { new Campo("MensajeUI", "Mensaje que ve el usuario") }, alCambiar));

            if (paso["Parametros"] is not JsonObject param) paso["Parametros"] = param = new JsonObject();
            var campos = param.Select(kv => new Campo(kv.Key, kv.Key, kv.Value switch
            {
                JsonValue v when v.TryGetValue<bool>(out _) => TipoCampo.Booleano,
                JsonArray or JsonObject                     => TipoCampo.Json,
                _                                           => TipoCampo.Texto,
            })).ToArray();
            panel.Children.Add(Formularios.Crear(param, campos, alCambiar));
            return panel;
        }

        private static string DescribirPaso(JsonObject p)
        {
            string tipo = DocumentoCatalogo.Texto(p["TipoAccion"]);
            var par = p["Parametros"] as JsonObject;
            string S(string k) => DocumentoCatalogo.Texto(par?[k]);
            string detalle = tipo switch
            {
                "DESCARGAR"     => S("ArchivoDestino"),
                "EXTRAER"       => $"{S("ArchivoZip")} → {S("CarpetaDestinoTemp")}",
                "COPIARSD"      => $"{S("OrigenTemp")} → {S("DestinoSD")}",
                "MOVERARCHIVO"  => $"{S("Origen")} → {S("Destino")}",
                "CREARINI" or "CREARTXT" => S("RutaSD"),
                _               => "",
            };
            return $"{DocumentoCatalogo.Texto(p["Paso"])}. {tipo}  {detalle}";
        }

        private static void Renumerar(JsonArray pasos)
        {
            int n = 1;
            foreach (var p in pasos.OfType<JsonObject>()) p["Paso"] = n++;
        }

        private static async Task SimularAsync(JsonObject modulo, JsonObject version, TextBox salida, Button boton)
        {
            boton.IsEnabled = false;
            salida.Text = "Descargando y simulando…";
            try
            {
                // Módulo temporal con solo la versión elegida: el simulador usa la primera.
                var temporal = new JsonObject
                {
                    ["Id"] = DocumentoCatalogo.Texto(modulo["Id"]),
                    ["Versiones"] = new JsonArray(version.DeepClone()),
                };
                using var gh = new GitHubReleases(ConfiguracionStudio.TokenGitHub);
                var inf = await new ActualizadorCatalogo(gh, validarPaquetes: true).PrevisualizarAsync(temporal);
                salida.Text = $"{inf.Detalle}\n\n{inf.Vista?.Replace("```", "").Trim()}";
            }
            catch (Exception ex)
            {
                salida.Text = $"No se pudo simular: {ex.Message}";
            }
            finally
            {
                boton.IsEnabled = true;
            }
        }

        // ── Acciones de módulos y versiones ────────────────────────────────

        private void NuevoModulo(bool vacio)
        {
            JsonObject nuevo;
            if (vacio || Actual == null)
            {
                nuevo = new JsonObject
                {
                    ["Id"] = "nuevo_modulo", ["Nombre"] = "Nuevo módulo", ["Descripcion"] = "",
                    ["IconoUrl"] = "", ["BannerUrl"] = "", ["UrlOficial"] = "", ["GitHubRepo"] = "",
                    ["Etiquetas"] = new JsonArray(), ["Dependencias"] = new JsonArray(),
                    ["Complementos"] = new JsonArray(), ["IncompatibleCon"] = new JsonArray(),
                    ["Versiones"] = new JsonArray(new JsonObject
                    {
                        ["Version"] = "1.0.0", ["SoloDeteccion"] = false,
                        ["PipelineInstalacion"] = new JsonArray(), ["PipelineDesinstalacion"] = new JsonArray(),
                    }),
                    ["FirmasDeteccion"] = new JsonArray(), ["RutasDesinstalacion"] = new JsonArray(),
                };
            }
            else
            {
                nuevo = (JsonObject)Actual.DeepClone();
                nuevo["Id"] = DocumentoCatalogo.Texto(Actual["Id"]) + "_copia";
                nuevo["Nombre"] = DocumentoCatalogo.Texto(Actual["Nombre"]) + " (copia)";
                nuevo["FirmasDeteccion"] = new JsonArray();
            }
            Modulos.Add(nuevo);
            _buscar.Text = "";
            _alCambiar();
            Filtrar(nuevo);
        }

        private void EliminarModulo()
        {
            var m = Actual;
            if (m == null) return;
            if (MessageBox.Show($"¿Eliminar el módulo «{DocumentoCatalogo.Texto(m["Nombre"])}» del catálogo?",
                    "NX-STUDIO", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            Modulos.Remove(m);
            _alCambiar();
            Filtrar(null);
        }

        private bool DuplicarVersion(JsonArray versiones, int i)
        {
            if (i < 0 || i >= versiones.Count) return false;
            var copia = (JsonObject)versiones[i]!.DeepClone();
            copia["Version"] = DocumentoCatalogo.Texto(copia["Version"]) + "-nueva";
            versiones.Insert(0, copia);
            _alCambiar();
            RefrescarRotulo();
            return true;
        }

        private bool HacerPrincipal(JsonArray versiones, int i)
        {
            if (i <= 0 || i >= versiones.Count) return false;
            var v = versiones[i]!;
            versiones.RemoveAt(i);
            versiones.Insert(0, v);
            _alCambiar();
            RefrescarRotulo();
            return true;
        }

        private bool EliminarVersion(JsonArray versiones, int i)
        {
            if (i < 0 || versiones.Count <= 1) { MessageBox.Show("Un módulo necesita al menos una versión.", "NX-STUDIO"); return false; }
            if (MessageBox.Show($"¿Eliminar la versión {DocumentoCatalogo.Texto(versiones[i]?["Version"])}?",
                    "NX-STUDIO", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return false;
            versiones.RemoveAt(i);
            _alCambiar();
            RefrescarRotulo();
            return true;
        }

        private void RefrescarRotulo()
        {
            int i = _lista.SelectedIndex;
            if (i < 0 || _lista.ItemsSource is not List<string> items) return;
            items[i] = Rotulo(_filtrados[i]);
            _lista.Items.Refresh();
        }

        private static TextBlock Separador(string texto) => new()
        {
            Text = texto, Foreground = Formularios.Pincel("#B266FF"), FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 22, 0, 4),
        };

        private static Button Boton(string texto, Action accion)
        {
            var b = new Button { Content = texto };
            b.Click += (_, _) => accion();
            return b;
        }
    }
}
