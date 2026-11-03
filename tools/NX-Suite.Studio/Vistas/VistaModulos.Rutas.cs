using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Camino 2: corrección rápida de un módulo existente. Muestra en una sola
    /// pantalla el diagnóstico, las rutas de cada paso (editables en línea) y la
    /// firma de la versión con un interruptor de SHA256 por archivo.
    /// </summary>
    internal sealed partial class VistaModulos
    {
        /// <summary>Parámetros de ruta editables por tipo de paso, con su etiqueta.</summary>
        private static readonly Dictionary<string, (string Clave, string Etiqueta)[]> RutasPorTipo = new()
        {
            ["DESCARGAR"]     = new[] { ("Url", "URL"), ("ArchivoDestino", "Guardar como") },
            ["EXTRAER"]       = new[] { ("ArchivoZip", "ZIP"), ("CarpetaDestinoTemp", "Carpeta temporal") },
            ["COPIARSD"]      = new[] { ("OrigenTemp", "Origen (temporal)"), ("DestinoSD", "Destino en SD") },
            ["MOVERARCHIVO"]  = new[] { ("Origen", "Origen"), ("Destino", "Destino") },
            ["LIMPIAR_CACHE"] = new[] { ("ArchivoZip", "ZIP") },
            ["CREARINI"]      = new[] { ("RutaSD", "Ruta en SD") },
            ["CREARTXT"]      = new[] { ("RutaSD", "Ruta en SD") },
        };

        private UIElement CrearEditorRutas(JsonObject modulo, JsonObject version)
        {
            var panel = new StackPanel();
            var diagnostico = new StackPanel();
            panel.Children.Add(diagnostico);
            void Diagnosticar()
            {
                diagnostico.Children.Clear();
                var problemas = Diagnostico(modulo, version);
                var contenido = new StackPanel();
                contenido.Children.Add(new TextBlock
                {
                    Text = problemas.Count == 0 ? "✓ Sin problemas detectados en esta versión" : $"⚠ {problemas.Count} punto(s) a revisar",
                    Foreground = Formularios.Pincel(problemas.Count == 0 ? Ui.Verde : Ui.Ambar), FontWeight = FontWeights.Bold,
                });
                foreach (var p in problemas) contenido.Children.Add(Ui.Texto("• " + p));
                diagnostico.Children.Add(Ui.Tarjeta(contenido, problemas.Count == 0 ? Ui.Verde : Ui.Ambar));
            }
            void Cambio() { _alCambiar(); Diagnosticar(); }

            // ── Rutas de instalación ──
            panel.Children.Add(Ui.Seccion("RUTAS DE INSTALACIÓN"));
            panel.Children.Add(Formularios.Ayuda("Edita aquí cualquier ruta o URL. Los cambios se aplican al momento; usa «🧪 Probar» para verificar el árbol final."));
            if (version["PipelineInstalacion"] is not JsonArray pasos) version["PipelineInstalacion"] = pasos = new JsonArray();
            foreach (var paso in pasos.OfType<JsonObject>())
            {
                string tipo = DocumentoCatalogo.Texto(paso["TipoAccion"]);
                if (!RutasPorTipo.TryGetValue(tipo, out var claves)) continue;
                if (paso["Parametros"] is not JsonObject par) paso["Parametros"] = par = new JsonObject();
                var bloque = new StackPanel();
                var cab = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                cab.Children.Add(Ui.Pastilla($"{DocumentoCatalogo.Texto(paso["Paso"])}. {tipo}", Ui.Acento));
                bloque.Children.Add(cab);
                foreach (var (clave, etiqueta) in claves)
                {
                    var tb = new TextBox { Text = DocumentoCatalogo.Texto(par[clave]), FontFamily = new FontFamily("Consolas") };
                    string k = clave;
                    tb.TextChanged += (_, _) => { par[k] = tb.Text; Cambio(); };
                    var fila = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
                    var et = new TextBlock { Text = etiqueta, Width = 130, Foreground = Formularios.Pincel(Ui.Tenue), VerticalAlignment = VerticalAlignment.Center };
                    DockPanel.SetDock(et, Dock.Left);
                    fila.Children.Add(et);
                    fila.Children.Add(tb);
                    bloque.Children.Add(fila);
                }
                panel.Children.Add(Ui.Tarjeta(bloque));
            }

            // ── Firma ──
            panel.Children.Add(Ui.Seccion("FIRMA DE ESTA VERSIÓN"));
            panel.Children.Add(Formularios.Ayuda("Cada archivo de la firma debe existir en la SD. Con SHA256 activado además debe coincidir el contenido exacto " +
                                                 "(identifica la versión). Sin SHA solo se comprueba que exista. Para recalcular hashes desde el paquete usa «🔏 Firma»."));
            string ver = DocumentoCatalogo.Texto(version["Version"]);
            if (modulo["FirmasDeteccion"] is not JsonArray firmas) modulo["FirmasDeteccion"] = firmas = new JsonArray();
            var propias = firmas.OfType<JsonObject>().Where(f => DocumentoCatalogo.Texto(f["Version"]) == ver).ToList();
            var contFirmas = new StackPanel();
            panel.Children.Add(contFirmas);

            void PintarFirmas()
            {
                contFirmas.Children.Clear();
                propias = firmas.OfType<JsonObject>().Where(f => DocumentoCatalogo.Texto(f["Version"]) == ver).ToList();
                if (propias.Count == 0)
                    contFirmas.Children.Add(Ui.Texto("Esta versión no tiene firma: la app no podrá detectarla en la SD.", Ui.Ambar));
                for (int n = 0; n < propias.Count; n++)
                {
                    var firma = propias[n];
                    if (firma["Archivos"] is not JsonArray archivos) firma["Archivos"] = archivos = new JsonArray();
                    var bloque = new StackPanel();
                    var cab = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                    var quitar = new BotonPeligro("× Quitar firma", () => { firmas.Remove(firma); Cambio(); PintarFirmas(); });
                    DockPanel.SetDock(quitar, Dock.Right);
                    cab.Children.Add(quitar);
                    int conSha = archivos.OfType<JsonObject>().Count(a => !string.IsNullOrWhiteSpace(DocumentoCatalogo.Texto(a["SHA256"])));
                    var titulo = new StackPanel { Orientation = Orientation.Horizontal };
                    titulo.Children.Add(Ui.Pastilla(n == 0 ? "Firma principal" : $"Alternativa {n}", n == 0 ? Ui.Acento : Ui.Violeta));
                    titulo.Children.Add(Ui.Pastilla($"{archivos.Count} archivos", Ui.Tenue));
                    titulo.Children.Add(Ui.Pastilla($"{conSha} con SHA256", conSha > 0 ? Ui.Verde : Ui.Ambar));
                    cab.Children.Add(titulo);
                    bloque.Children.Add(cab);

                    foreach (var a in archivos.OfType<JsonObject>().ToList())
                        bloque.Children.Add(FilaArchivoFirma(archivos, a, Cambio, PintarFirmas));

                    var nueva = new TextBox { FontFamily = new FontFamily("Consolas"), ToolTip = "Ruta en la SD, ej. /switch/App/App.nro" };
                    var anadir = Ui.Boton("+ Añadir archivo", () =>
                    {
                        string r = nueva.Text.Trim().Replace('\\', '/');
                        if (r.Length == 0) return;
                        archivos.Add(new JsonObject { ["Ruta"] = r.StartsWith('/') ? r : "/" + r });
                        Cambio(); PintarFirmas();
                    });
                    var filaNueva = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
                    DockPanel.SetDock(anadir, Dock.Right);
                    filaNueva.Children.Add(anadir);
                    filaNueva.Children.Add(nueva);
                    bloque.Children.Add(filaNueva);
                    contFirmas.Children.Add(Ui.Tarjeta(bloque, n == 0 ? Ui.Acento : Ui.Violeta));
                }
                contFirmas.Children.Add(Ui.Boton("+ Nueva firma vacía", () =>
                {
                    firmas.Add(new JsonObject { ["Version"] = ver, ["Archivos"] = new JsonArray() });
                    Cambio(); PintarFirmas();
                }));
            }

            PintarFirmas();
            Diagnosticar();
            return panel;
        }

        private static UIElement FilaArchivoFirma(JsonArray archivos, JsonObject a, Action cambio, Action repintar)
        {
            string sha = DocumentoCatalogo.Texto(a["SHA256"]);
            var fila = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };

            var quitar = new Button { Content = "✕", Padding = new Thickness(8, 2, 8, 2), ToolTip = "Quitar de la firma" };
            quitar.Click += (_, _) => { archivos.Remove(a); cambio(); repintar(); };
            DockPanel.SetDock(quitar, Dock.Right);
            fila.Children.Add(quitar);

            var hash = new TextBox
            {
                Text = sha, Width = 200, FontFamily = new FontFamily("Consolas"), FontSize = 11,
                Visibility = sha.Length > 0 ? Visibility.Visible : Visibility.Collapsed, ToolTip = "SHA256 esperado (64 hex)",
            };
            hash.TextChanged += (_, _) =>
            {
                string h = hash.Text.Trim().ToLowerInvariant();
                if (h.Length == 0) a.Remove("SHA256"); else a["SHA256"] = h;
                hash.BorderBrush = Formularios.Pincel(h.Length == 0 || Regex.IsMatch(h, "^[0-9a-f]{64}$") ? "#33333F" : Ui.Rojo);
                cambio();
            };
            DockPanel.SetDock(hash, Dock.Right);
            fila.Children.Add(hash);

            var chip = Ui.Chip(sha.Length > 0 ? "SHA256" : "Presencia", sha.Length > 0, on =>
            {
                if (on) { hash.Visibility = Visibility.Visible; hash.Focus(); }
                else { a.Remove("SHA256"); cambio(); repintar(); }
            }, "Activado: debe coincidir el hash. Desactivado: basta con que exista.");
            chip.Margin = new Thickness(0, 0, 8, 0);
            chip.Width = 90;
            DockPanel.SetDock(chip, Dock.Left);
            fila.Children.Add(chip);

            var ruta = new TextBox { Text = DocumentoCatalogo.Texto(a["Ruta"]), FontFamily = new FontFamily("Consolas") };
            ruta.TextChanged += (_, _) => { a["Ruta"] = ruta.Text.Trim().Replace('\\', '/'); cambio(); };
            fila.Children.Add(ruta);
            return fila;
        }

        /// <summary>Comprobaciones locales (sin red) de una versión y su firma.</summary>
        private static List<string> Diagnostico(JsonObject modulo, JsonObject version)
        {
            var p = new List<string>();
            string ver = DocumentoCatalogo.Texto(version["Version"]);
            var pasos = (version["PipelineInstalacion"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new();
            string S(JsonObject paso, string k) => DocumentoCatalogo.Texto(paso["Parametros"]?[k]).Trim();

            bool soloDeteccion = version["SoloDeteccion"] is JsonValue sd && sd.TryGetValue<bool>(out bool b) && b;
            if (pasos.Count == 0 && !soloDeteccion) p.Add("No hay pasos de instalación.");

            var descargados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var temporales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var paso in pasos)
            {
                string n = DocumentoCatalogo.Texto(paso["Paso"]);
                switch (DocumentoCatalogo.Texto(paso["TipoAccion"]))
                {
                    case "DESCARGAR":
                        if (!Uri.TryCreate(S(paso, "Url"), UriKind.Absolute, out var u) || u.Scheme != "https")
                            p.Add($"Paso {n}: la URL no es https válida.");
                        if (S(paso, "ArchivoDestino").Length == 0) p.Add($"Paso {n}: falta «Guardar como».");
                        descargados.Add(S(paso, "ArchivoDestino"));
                        temporales.Add(S(paso, "ArchivoDestino"));
                        break;
                    case "EXTRAER":
                        if (!descargados.Contains(S(paso, "ArchivoZip"))) p.Add($"Paso {n}: extrae «{S(paso, "ArchivoZip")}», que ningún paso anterior descarga.");
                        temporales.Add(S(paso, "CarpetaDestinoTemp"));
                        break;
                    case "COPIARSD":
                        string origen = S(paso, "OrigenTemp"), destino = S(paso, "DestinoSD");
                        if (!temporales.Any(t => t.Length > 0 && (origen.Equals(t, StringComparison.OrdinalIgnoreCase) || origen.StartsWith(t.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))))
                            p.Add($"Paso {n}: el origen «{origen}» no coincide con nada descargado o extraído antes.");
                        if (!destino.StartsWith('/')) p.Add($"Paso {n}: el destino «{destino}» debe empezar por «/».");
                        if (destino.Contains('\\')) p.Add($"Paso {n}: usa «/» en lugar de «\\» en el destino.");
                        break;
                }
            }

            var firmas = (modulo["FirmasDeteccion"] as JsonArray)?.OfType<JsonObject>()
                .Where(f => DocumentoCatalogo.Texto(f["Version"]) == ver).ToList() ?? new();
            if (firmas.Count == 0) p.Add("Sin firma de detección: la app no reconocerá esta versión instalada.");
            foreach (var f in firmas)
            {
                var archivos = (f["Archivos"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new();
                if (archivos.Count == 0) p.Add("Hay una firma sin archivos.");
                else if (!archivos.Any(a => DocumentoCatalogo.Texto(a["SHA256"]).Length > 0) &&
                         (modulo["Versiones"] as JsonArray)?.Count > 1)
                    p.Add("La firma no tiene ningún SHA256: no se podrá distinguir esta versión de las demás.");
                foreach (var a in archivos)
                {
                    string r = DocumentoCatalogo.Texto(a["Ruta"]), h = DocumentoCatalogo.Texto(a["SHA256"]);
                    if (!r.StartsWith('/')) p.Add($"Firma: «{r}» debe empezar por «/».");
                    if (h.Length > 0 && !Regex.IsMatch(h, "^[0-9a-fA-F]{64}$")) p.Add($"Firma: el SHA256 de «{r}» no tiene 64 caracteres hexadecimales.");
                }
                var duplicadas = archivos.GroupBy(a => DocumentoCatalogo.Texto(a["Ruta"]), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1);
                foreach (var d in duplicadas) p.Add($"Firma: «{d.Key}» aparece repetida.");
            }

            // Misma firma exacta que otra versión: la detección sería ambigua.
            string Huella(JsonObject f) => string.Join("|", ((f["Archivos"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
                .Where(a => DocumentoCatalogo.Texto(a["SHA256"]).Length > 0)
                .Select(a => DocumentoCatalogo.Texto(a["Ruta"]).ToLowerInvariant() + "=" + DocumentoCatalogo.Texto(a["SHA256"]).ToLowerInvariant()).OrderBy(x => x));
            var otras = (modulo["FirmasDeteccion"] as JsonArray)?.OfType<JsonObject>().Where(f => DocumentoCatalogo.Texto(f["Version"]) != ver).ToList() ?? new();
            foreach (var f in firmas)
            {
                string h = Huella(f);
                if (h.Length == 0) continue;
                var igual = otras.FirstOrDefault(o => Huella(o) == h);
                if (igual != null) p.Add($"Los SHA256 son idénticos a los de la versión {DocumentoCatalogo.Texto(igual["Version"])}: se detectará como la más reciente.");
            }
            return p;
        }
    }
}
