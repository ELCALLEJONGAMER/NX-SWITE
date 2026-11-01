using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NX_Suite.Catalogo;

namespace NX_Suite.Studio.Vistas
{
    internal sealed partial class VistaModulos
    {
        private sealed class FilaFirma
        {
            public required string Ruta { get; init; }
            public string? Sha { get; init; }
            public required CheckBox Incluir { get; init; }
            public required CheckBox UsarSha { get; init; }
        }

        /// <summary>
        /// Editor de la firma de detección de una versión: lista los archivos que
        /// quedarán en la SD y permite elegir cuáles entran y si van por SHA256 o
        /// por presencia. Lo que no tiene historial entra por presencia.
        /// </summary>
        private UIElement CrearEditorFirma(JsonObject modulo, JsonObject version)
        {
            var panel = new StackPanel();
            panel.Children.Add(Separador("FIRMA DE DETECCIÓN"));
            panel.Children.Add(Formularios.Ayuda(
                "Así reconoce NX-SWITE esta versión en la microSD. SHA256 identifica la versión exacta: márcalo en 1–2 archivos principales " +
                "(.nro, package3, payload). Presencia solo confirma que está instalado. Lo que no tiene historial entra por presencia."));

            var estado = new TextBlock { Foreground = Formularios.Pincel("#9A9AA8"), Margin = new Thickness(0, 4, 0, 4), TextWrapping = TextWrapping.Wrap };
            var filas = new StackPanel();
            var items = new List<FilaFirma>();
            var analizar = Boton("🔍 Analizar paquete", () => { });
            var guardar = Boton("💾 Guardar firma", () => GuardarFirma(false));
            var alternativa = Boton("➕ Añadir como firma alternativa", () => GuardarFirma(true));
            guardar.IsEnabled = alternativa.IsEnabled = false;
            var barra = new WrapPanel();
            barra.Children.Add(analizar);
            barra.Children.Add(guardar);
            barra.Children.Add(alternativa);
            panel.Children.Add(barra);
            panel.Children.Add(estado);
            panel.Children.Add(new ScrollViewer { Content = filas, MaxHeight = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

            void Resumen()
            {
                int incl = items.Count(i => i.Incluir.IsChecked == true);
                int sha = items.Count(i => i.Incluir.IsChecked == true && i.UsarSha.IsChecked == true);
                estado.Text = $"{items.Count} archivo(s) · {incl} en la firma · {sha} con SHA256" +
                              (incl > 0 && sha == 0 ? "  ·  ℹ sin SHA256 no se distingue la versión exacta" : "");
            }

            void AnadirFila(string ruta, string? sha, bool incluir, bool usarSha, string? nota)
            {
                var fila = new FilaFirma
                {
                    Ruta = ruta, Sha = sha,
                    Incluir = new CheckBox { IsChecked = incluir, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Incluir en la firma" },
                    UsarSha = new CheckBox
                    {
                        IsChecked = usarSha && sha != null, IsEnabled = sha != null, VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = sha ?? "Sin hash disponible: solo presencia",
                    },
                };
                fila.UsarSha.Checked += (_, _) => { fila.Incluir.IsChecked = true; Resumen(); };
                fila.UsarSha.Unchecked += (_, _) => Resumen();
                fila.Incluir.Checked += (_, _) => Resumen();
                fila.Incluir.Unchecked += (_, _) => { fila.UsarSha.IsChecked = false; Resumen(); };
                items.Add(fila);

                var g = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.Children.Add(fila.Incluir);
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(fila.UsarSha);
                sp.Children.Add(new TextBlock { Text = " SHA", Foreground = Formularios.Pincel("#9A9AA8"), VerticalAlignment = VerticalAlignment.Center });
                SetColumn(sp, 1);
                g.Children.Add(sp);
                var texto = new TextBlock
                {
                    Text = nota == null ? ruta : $"{ruta}   ({nota})", FontFamily = new FontFamily("Consolas"),
                    Foreground = nota == null ? Brushes.White : Formularios.Pincel("#FFA640"), VerticalAlignment = VerticalAlignment.Center,
                };
                SetColumn(texto, 2);
                g.Children.Add(texto);
                filas.Children.Add(g);
            }

            analizar.Click += async (_, _) =>
            {
                analizar.IsEnabled = guardar.IsEnabled = alternativa.IsEnabled = false;
                filas.Children.Clear();
                items.Clear();
                estado.Text = "Descargando y analizando el paquete…";
                try
                {
                    using var gh = new GitHubReleases(ConfiguracionStudio.TokenGitHub);
                    var (archivos, avisos) = await new ActualizadorCatalogo(gh, validarPaquetes: true).AnalizarVersionAsync(version);

                    string ver = DocumentoCatalogo.Texto(version["Version"]);
                    var firmas = (modulo["FirmasDeteccion"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new();
                    var propia = firmas.FirstOrDefault(f => DocumentoCatalogo.Texto(f["Version"]) == ver);
                    // Historial: manda la firma de esta versión; si no existe, las de versiones anteriores.
                    var historial = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                    foreach (var f in propia != null ? new List<JsonObject> { propia } : firmas)
                        foreach (var a in (f["Archivos"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
                            historial.TryAdd("/" + DocumentoCatalogo.Texto(a["Ruta"]).Replace('\\', '/').Trim('/'),
                                             a["SHA256"] != null && !string.IsNullOrWhiteSpace(DocumentoCatalogo.Texto(a["SHA256"])));

                    bool sinHistorial = historial.Count == 0;
                    foreach (var a in archivos)
                    {
                        bool conocido = historial.TryGetValue(a.Ruta, out bool tenia);
                        AnadirFila(a.Ruta, a.Generado ? null : a.Sha256, conocido || sinHistorial, conocido && tenia,
                                   a.Generado ? "generado" : conocido || sinHistorial ? null : "nuevo");
                    }
                    var enSd = new HashSet<string>(archivos.Select(a => a.Ruta), StringComparer.OrdinalIgnoreCase);
                    if (propia != null)
                        foreach (var ruta in historial.Keys.Where(r => !enSd.Contains(r)))
                            AnadirFila(ruta, null, false, false, "ya no queda en la SD");

                    Resumen();
                    if (avisos.Count > 0) estado.Text += "\n⚠ " + string.Join("\n⚠ ", avisos);
                    guardar.IsEnabled = alternativa.IsEnabled = items.Count > 0;
                }
                catch (Exception ex) { estado.Text = "Error: " + ex.Message; }
                finally { analizar.IsEnabled = true; }
            };

            void GuardarFirma(bool comoAlternativa)
            {
                var archivos = new JsonArray();
                foreach (var i in items.Where(i => i.Incluir.IsChecked == true))
                {
                    var o = new JsonObject { ["Ruta"] = i.Ruta };
                    if (i.UsarSha.IsChecked == true && i.Sha != null) o["SHA256"] = i.Sha;
                    archivos.Add(o);
                }
                if (archivos.Count == 0)
                {
                    MessageBox.Show("Marca al menos un archivo para la firma.", "NX-STUDIO");
                    return;
                }

                string ver = DocumentoCatalogo.Texto(version["Version"]);
                if (modulo["FirmasDeteccion"] is not JsonArray firmas) modulo["FirmasDeteccion"] = firmas = new JsonArray();
                int idx = firmas.ToList().FindIndex(f => f is JsonObject o && DocumentoCatalogo.Texto(o["Version"]) == ver);
                if (!comoAlternativa && idx >= 0)
                    ((JsonObject)firmas[idx]!)["Archivos"] = archivos;
                else
                    firmas.Insert(Math.Max(idx, 0), new JsonObject { ["Version"] = ver, ["Archivos"] = archivos });
                _alCambiar();
                estado.Text = (comoAlternativa ? "✓ Firma alternativa añadida para " : "✓ Firma guardada para ") + ver +
                              $" ({archivos.Count} archivos). Pulsa «Guardar» para escribir catalogo.json.";
            }

            return panel;
        }
    }
}
