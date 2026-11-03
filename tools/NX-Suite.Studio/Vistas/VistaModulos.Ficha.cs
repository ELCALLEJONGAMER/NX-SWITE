using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NX_Suite.Catalogo;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Ficha de un módulo organizada en pestañas (Módulo, Versión y pasos, Firma,
    /// Probar) con la versión elegida arriba, para evitar un scroll interminable.
    /// </summary>
    internal sealed partial class VistaModulos
    {
        private static int _pestana;

        /// <summary>Ficha de un módulo fuera de la lista (la usa el panel de tarjetas).</summary>
        internal static UIElement Ficha(DocumentoCatalogo doc, Action alCambiar, JsonObject modulo) =>
            new VistaModulos(doc, alCambiar, soloFicha: true).CrearFicha(modulo);

        private UIElement CrearFicha(JsonObject m)
        {
            var host = new ContentControl();
            int versionSel = 0;

            void Dibujar()
            {
                if (m["Versiones"] is not JsonArray versiones) m["Versiones"] = versiones = new JsonArray();
                versionSel = Math.Clamp(versionSel, 0, Math.Max(0, versiones.Count - 1));
                var version = versiones.Count > 0 ? versiones[versionSel] as JsonObject : null;

                var raiz = new DockPanel();
                var cab = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
                var icono = new Image { Width = 48, Height = 48, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Top };
                Formularios.MostrarImagen(icono, DocumentoCatalogo.Texto(m["IconoUrl"]));
                DockPanel.SetDock(icono, Dock.Left);
                cab.Children.Add(icono);
                var titulo = new StackPanel();
                titulo.Children.Add(Formularios.Titulo(DocumentoCatalogo.Texto(m["Nombre"])));

                var barra = new WrapPanel();
                barra.Children.Add(new TextBlock { Text = "Versión:  ", Foreground = Formularios.Pincel("#9A9AA8"), VerticalAlignment = VerticalAlignment.Center });
                var combo = new ComboBox
                {
                    MinWidth = 200,
                    ItemsSource = versiones.OfType<JsonObject>().Select((v, i) => DocumentoCatalogo.Texto(v["Version"]) + (i == 0 ? "   ★ principal" : "")).ToList(),
                    SelectedIndex = version != null ? versionSel : -1,
                };
                combo.SelectionChanged += (_, _) => { if (combo.SelectedIndex >= 0 && combo.SelectedIndex != versionSel) { versionSel = combo.SelectedIndex; Dibujar(); } };
                barra.Children.Add(combo);
                barra.Children.Add(Boton("⧉ Duplicar como nueva", () => { if (DuplicarVersion(versiones, versionSel)) { versionSel = 0; Dibujar(); } }));
                barra.Children.Add(Boton("★ Hacer principal", () => { if (HacerPrincipal(versiones, versionSel)) { versionSel = 0; Dibujar(); } }));
                barra.Children.Add(new BotonPeligro("× Eliminar versión", () => { if (EliminarVersion(versiones, versionSel)) { versionSel = 0; Dibujar(); } }));
                titulo.Children.Add(barra);
                cab.Children.Add(titulo);
                DockPanel.SetDock(cab, Dock.Top);
                raiz.Children.Add(cab);

                var pestanas = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
                pestanas.Items.Add(Pestana("📋 Módulo", Formularios.CrearCompacto(m, CamposModulo, _alCambiar)));
                if (version != null)
                {
                    pestanas.Items.Add(Pestana("📦 Archivos y SHA", CrearReempaquetado(m, version, Dibujar)));
                    pestanas.Items.Add(Pestana("⚙ Versión y pasos", CrearEditorVersion(version)));
                    pestanas.Items.Add(Pestana("🛠 Rutas (avanzado)", CrearEditorRutas(m, version)));
                    pestanas.Items.Add(Pestana("🔏 Firma (avanzado)", CrearEditorFirma(m, version)));
                    pestanas.Items.Add(Pestana("🧪 Probar", CrearPruebas(m, version)));
                }
                pestanas.SelectedIndex = Math.Min(_pestana, pestanas.Items.Count - 1);
                pestanas.SelectionChanged += (s, e) => { if (e.OriginalSource == pestanas) _pestana = pestanas.SelectedIndex; };
                raiz.Children.Add(pestanas);
                host.Content = raiz;
            }

            Dibujar();
            return host;
        }

        private static TabItem Pestana(string titulo, UIElement contenido) => new()
        {
            Header = new TextBlock { Text = titulo, Margin = new Thickness(6, 2, 6, 2), FontSize = 13 },
            Content = new ScrollViewer
            {
                Content = contenido, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0, 10, 8, 0),
            },
        };

        /// <summary>
        /// Flujo rápido para corregir una versión: elegir/redescargar el archivo,
        /// ver cómo cae en la SD, ajustar rutas/SHA y guardar. Regenera el pipeline
        /// y sustituye la firma de esa versión.
        /// </summary>
        private UIElement CrearReempaquetado(JsonObject modulo, JsonObject version, Action redibujar)
        {
            var panel = new StackPanel();
            var descarga = (version["PipelineInstalacion"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault(p => DocumentoCatalogo.Texto(p["TipoAccion"]) == "DESCARGAR");
            string urlActual = DocumentoCatalogo.Texto((descarga?["Parametros"] as JsonObject)?["Url"]);
            string id = DocumentoCatalogo.Texto(modulo["Id"]);

            panel.Children.Add(Separador("1 · ARCHIVO DE ESTA VERSIÓN"));
            var selector = new SelectorDescarga(DocumentoCatalogo.Texto(modulo["GitHubRepo"]), urlActual);
            panel.Children.Add(selector);
            var limpiarCache = (version["PipelineInstalacion"] as JsonArray)?.OfType<JsonObject>()
                .Any(p => DocumentoCatalogo.Texto(p["TipoAccion"]) == "LIMPIAR_CACHE") ?? true;

            var estado = Ui.Texto("", Ui.Tenue);
            var zona = new ContentControl();
            EditorPaquete? editor = null;
            var guardar = Ui.Boton("💾 Guardar pipeline y firma", () => { }, primario: true);
            guardar.IsEnabled = false;
            var cargar = Ui.Boton("⬇ Descargar y analizar", () => { });
            var barra = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            barra.Children.Add(cargar);
            barra.Children.Add(guardar);
            panel.Children.Add(barra);
            panel.Children.Add(estado);
            panel.Children.Add(Separador("2 · CÓMO CAE EN LA MICROSD"));
            panel.Children.Add(Formularios.Ayuda("Se respetan los destinos y SHA actuales cuando el archivo coincide. Edita el destino para renombrar/mover, " +
                                                 "desmarca para excluir y pulsa el chip para firmar por SHA256."));
            panel.Children.Add(zona);

            cargar.Click += async (_, _) =>
            {
                string url = selector.Url, asset = selector.Asset;
                if (!Uri.TryCreate(url, UriKind.Absolute, out _)) { estado.Text = "⚠ URL no válida."; return; }
                cargar.IsEnabled = guardar.IsEnabled = false;
                estado.Text = $"Descargando y analizando {asset}…";
                try
                {
                    var archivos = await CargaPaquete.CargarAsync(asset, url, id, modulo, version);
                    editor = new EditorPaquete(asset, id, archivos, limpiarCache);
                    zona.Content = editor;
                    guardar.IsEnabled = true;
                    estado.Text = $"✓ {archivos.Count} archivo(s) en {asset}.";
                }
                catch (Exception ex) { estado.Text = "⚠ No se pudo descargar: " + ex.Message; }
                finally { cargar.IsEnabled = true; }
            };

            guardar.Click += (_, _) =>
            {
                if (editor == null) return;
                if (editor.Validar() is { } error) { estado.Text = "⚠ " + error; return; }
                string ver = DocumentoCatalogo.Texto(version["Version"]);
                if (selector.VersionPropuesta.Length > 0 && selector.VersionPropuesta != ver.Split('+')[0] &&
                    MessageBox.Show($"La release elegida es {selector.VersionPropuesta} y esta versión es {ver}.\n¿Cambiar el número de versión a {selector.VersionPropuesta}?",
                                    "NX-STUDIO", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    foreach (var f in (modulo["FirmasDeteccion"] as JsonArray)?.OfType<JsonObject>().Where(f => DocumentoCatalogo.Texto(f["Version"]) == ver).ToList() ?? new())
                        f["Version"] = selector.VersionPropuesta;
                    ver = selector.VersionPropuesta;
                    version["Version"] = ver;
                }

                var (inst, desins) = MapaPaquete.GenerarPipelines(selector.Url, selector.Asset, $"{id}-{ver}", editor.Archivos, editor.LimpiarCache);
                version["PipelineInstalacion"] = inst;
                version["PipelineDesinstalacion"] = desins;

                if (modulo["FirmasDeteccion"] is not JsonArray firmas) modulo["FirmasDeteccion"] = firmas = new JsonArray();
                var nueva = MapaPaquete.GenerarFirma(ver, editor.Archivos);
                var propia = firmas.OfType<JsonObject>().FirstOrDefault(f => DocumentoCatalogo.Texto(f["Version"]) == ver);
                if (propia != null) propia["Archivos"] = nueva["Archivos"]!.DeepClone();
                else firmas.Insert(0, nueva);

                _alCambiar();
                redibujar();
            };
            return panel;
        }

        /// <summary>Simulación de la microSD e instalación de prueba de una versión.</summary>
        private UIElement CrearPruebas(JsonObject modulo, JsonObject version)
        {
            var panel = new StackPanel();
            panel.Children.Add(Separador("CÓMO QUEDARÁ EN LA MICROSD"));
            panel.Children.Add(Formularios.Ayuda("Descarga los archivos de esta versión (o usa la caché) y simula los pasos sin tocar ninguna SD. Los avisos indican rutas que no existen en el paquete."));
            var resultado = new TextBox
            {
                IsReadOnly = true, FontFamily = new FontFamily("Consolas"), MinHeight = 160, MaxHeight = 460,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap,
                Margin = new Thickness(0, 6, 0, 0),
            };
            var barra = new WrapPanel();
            var simular = Boton("▶ Simular microSD", () => { });
            simular.Click += async (_, _) => await SimularAsync(modulo, version, resultado, simular);
            barra.Children.Add(simular);
            barra.Children.Add(Boton("💾 Instalar en SD de prueba", async () =>
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Elige la microSD o carpeta donde instalar la versión de prueba" };
                if (dlg.ShowDialog() != true) return;
                try
                {
                    string log = await InstaladorPrueba.InstalarAsync(version, dlg.FolderName, new Progress<string>(_ => { }));
                    MessageBox.Show(log + "\nPruébalo en la consola.", "NX-STUDIO");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo instalar: " + ex.Message, "NX-STUDIO", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }));
            panel.Children.Add(barra);
            panel.Children.Add(resultado);
            return panel;
        }
    }
}
