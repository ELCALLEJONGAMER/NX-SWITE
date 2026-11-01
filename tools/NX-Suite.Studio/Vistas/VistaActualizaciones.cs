using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NX_Suite.Catalogo;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Pantalla principal de NX-STUDIO: revisa todos los módulos contra GitHub,
    /// los agrupa por urgencia (enlace roto, revisar, listas) y permite ver el
    /// árbol de la microSD, instalar en una SD de prueba y aprobar con un clic.
    /// Nada se publica hasta pulsar «Publicar».
    /// </summary>
    internal sealed class VistaActualizaciones : DockPanel
    {
        private enum Grupo { Urgente, Revisar, Lista, Manual, Validacion }

        private sealed record Tarjeta(JsonObject Modulo, InformeModulo Informe, Grupo Grupo);

        private readonly DocumentoCatalogo _doc;
        private readonly Action _alCambiar;
        private readonly StackPanel _lista = new();
        private readonly TextBlock _estado = new() { Foreground = Formularios.Pincel("#9A9AA8"), VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _aprobadas = new() { Foreground = Formularios.Pincel("#00D2FF"), FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
        private readonly Button _revisar;
        private readonly Button _validar;
        private static readonly List<string> _aprobadasIds = new();
        private static List<Tarjeta>? _ultimaRevision;
        private static string _ultimoEstado = "";

        public VistaActualizaciones(DocumentoCatalogo doc, Action alCambiar)
        {
            _doc = doc;
            _alCambiar = alCambiar;

            // Cabecera
            var cab = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            _revisar = Boton("⟳ Revisar ahora", async () => await RevisarAsync());
            DockPanel.SetDock(_revisar, Dock.Right);
            cab.Children.Add(_revisar);
            _validar = Boton("🧪 Validar estructuras", async () => await ValidarEstructurasAsync());
            DockPanel.SetDock(_validar, Dock.Right);
            cab.Children.Add(_validar);
            var token = Boton("🔑 Token", () => { new VentanaToken().ShowDialog(); });
            DockPanel.SetDock(token, Dock.Right);
            cab.Children.Add(token);
            var titulos = new StackPanel();
            titulos.Children.Add(Formularios.Titulo("Actualizaciones"));
            titulos.Children.Add(_estado);
            cab.Children.Add(titulos);
            SetDock(cab, Dock.Top);
            Children.Add(cab);

            // Barra de publicación
            var pie = new Border
            {
                Background = Formularios.Pincel("#1A1A21"), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 12, 0, 0),
            };
            var pieDock = new DockPanel();
            var publicar = Boton("🚀 Publicar", async () => await PublicarAsync());
            DockPanel.SetDock(publicar, Dock.Right);
            pieDock.Children.Add(publicar);
            pieDock.Children.Add(_aprobadas);
            pie.Child = pieDock;
            SetDock(pie, Dock.Bottom);
            Children.Add(pie);

            Children.Add(new ScrollViewer { Content = _lista, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

            ActualizarPie();
            // Solo se consulta GitHub al pulsar «Revisar ahora»; al volver se muestra el último resultado.
            if (_ultimaRevision != null) { _estado.Text = _ultimoEstado; Pintar(_ultimaRevision); }
            else _estado.Text = "Pulsa «⟳ Revisar ahora» para buscar actualizaciones en GitHub.";
        }

        // ── Revisión ───────────────────────────────────────────────────────

        private async Task RevisarAsync()
        {
            _revisar.IsEnabled = false;
            _lista.Children.Clear();
            var tarjetas = new List<Tarjeta>();
            try
            {
                using var gh = new GitHubReleases(ConfiguracionStudio.TokenGitHub);
                var act = new ActualizadorCatalogo(gh, validarPaquetes: true);
                var modulos = _doc.Lista("Modulos").OfType<JsonObject>().ToList();
                int n = 0;
                foreach (var original in modulos)
                {
                    n++;
                    _estado.Text = $"Revisando {n}/{modulos.Count}: {DocumentoCatalogo.Texto(original["Nombre"])}…";
                    InformeModulo inf;
                    try
                    {
                        // Sobre una copia: el catálogo no cambia hasta aprobar.
                        inf = await act.ProcesarAsync((JsonObject)original.DeepClone(), aplicar: true);
                    }
                    catch (Exception ex)
                    {
                        inf = new InformeModulo(DocumentoCatalogo.Texto(original["Id"]), ResultadoModulo.Manual, ex.Message);
                        if (ex.Message.StartsWith("Límite de la API")) throw;
                    }

                    var grupo = Clasificar(inf);
                    if (grupo == null) continue;
                    tarjetas.Add(new Tarjeta(original, inf, grupo.Value));
                }
                _estado.Text = $"Última revisión: {DateTime.Now:HH:mm} · {modulos.Count} módulos · {tarjetas.Count} con novedades";
            }
            catch (Exception ex)
            {
                _estado.Text = "Error: " + ex.Message;
            }
            finally { _revisar.IsEnabled = true; }

            _ultimaRevision = tarjetas;
            _ultimoEstado = _estado.Text;
            Pintar(tarjetas);
        }

        private async Task ValidarEstructurasAsync()
        {
            _validar.IsEnabled = _revisar.IsEnabled = false;
            _lista.Children.Clear();
            var tarjetas = new List<Tarjeta>();
            int correctos = 0;
            try
            {
                using var gh = new GitHubReleases(ConfiguracionStudio.TokenGitHub);
                var act = new ActualizadorCatalogo(gh, validarPaquetes: true);
                var modulos = _doc.Lista("Modulos").OfType<JsonObject>().ToList();
                int n = 0;
                foreach (var original in modulos)
                {
                    n++;
                    _estado.Text = $"Validando {n}/{modulos.Count}: {DocumentoCatalogo.Texto(original["Nombre"])}…";
                    InformeModulo inf;
                    try { inf = await act.ValidarEstructuraAsync((JsonObject)original.DeepClone()); }
                    catch (Exception ex) { inf = new InformeModulo(DocumentoCatalogo.Texto(original["Id"]), ResultadoModulo.Manual, ex.Message); }

                    if (inf.Resultado == ResultadoModulo.Omitido) continue;
                    if (inf.Resultado == ResultadoModulo.AlDia) { correctos++; continue; }
                    tarjetas.Add(new Tarjeta(original, inf, inf.Propuesta?.EnlaceRoto == true ? Grupo.Urgente : Grupo.Validacion));
                }
                _estado.Text = $"Validación: {DateTime.Now:HH:mm} · {correctos} correctos · {tarjetas.Count} con problemas";
            }
            catch (Exception ex) { _estado.Text = "Error: " + ex.Message; }
            finally { _validar.IsEnabled = _revisar.IsEnabled = true; }

            _ultimaRevision = tarjetas;
            _ultimoEstado = _estado.Text;
            Pintar(tarjetas);
        }

        private static Grupo? Clasificar(InformeModulo inf) => inf switch
        {
            { Resultado: ResultadoModulo.AlDia or ResultadoModulo.Omitido } => null,
            { Propuesta.EnlaceRoto: true } => Grupo.Urgente,
            { Resultado: ResultadoModulo.Actualizado, Propuesta: not null } => Grupo.Lista,
            { Propuesta: not null } => Grupo.Revisar,
            _ => Grupo.Manual,
        };

        private void Pintar(List<Tarjeta> tarjetas)
        {
            _lista.Children.Clear();
            if (tarjetas.Count == 0)
            {
                _lista.Children.Add(new TextBlock
                {
                    Text = "✓ Todo al día. No hay actualizaciones pendientes.",
                    Foreground = Formularios.Pincel("#3DDC84"), FontSize = 16, Margin = new Thickness(0, 30, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
                return;
            }

            Seccion(tarjetas, Grupo.Urgente, "🔴 URGENTE · enlace publicado roto", "#FF4D4D");
            Seccion(tarjetas, Grupo.Revisar, "🟠 REVISAR · la estructura cambió", "#FFA640");
            Seccion(tarjetas, Grupo.Lista, "🟢 LISTAS · misma estructura", "#3DDC84");
            Seccion(tarjetas, Grupo.Manual, "⚪ ATENCIÓN MANUAL · no se pudo preparar", "#9A9AA8");
            Seccion(tarjetas, Grupo.Validacion, "🧪 VALIDACIÓN · firma o estructura con problemas", "#FFA640");
        }

        private void Seccion(List<Tarjeta> todas, Grupo grupo, string titulo, string color)
        {
            var del = todas.Where(t => t.Grupo == grupo).ToList();
            if (del.Count == 0) return;

            var cab = new DockPanel { Margin = new Thickness(0, 14, 0, 6) };
            if (grupo == Grupo.Lista && del.Count > 1)
            {
                var todasBtn = Boton($"✓ Aprobar las {del.Count}", () =>
                {
                    foreach (var t in del) Aprobar(t, preguntar: false);
                });
                DockPanel.SetDock(todasBtn, Dock.Right);
                cab.Children.Add(todasBtn);
            }
            cab.Children.Add(new TextBlock
            {
                Text = $"{titulo} · {del.Count}", Foreground = Formularios.Pincel(color),
                FontWeight = FontWeights.Bold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center,
            });
            _lista.Children.Add(cab);

            foreach (var t in del) _lista.Children.Add(CrearTarjeta(t, color));
        }

        private Border CrearTarjeta(Tarjeta t, string color)
        {
            var p = t.Informe.Propuesta;
            var borde = new Border
            {
                Background = Formularios.Pincel("#1C1C24"), BorderBrush = Formularios.Pincel(color),
                BorderThickness = new Thickness(3, 0, 0, 0), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 8),
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icono = new Image { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Top };
            Formularios.MostrarImagen(icono, DocumentoCatalogo.Texto(t.Modulo["IconoUrl"]));
            grid.Children.Add(icono);

            var info = new StackPanel { Margin = new Thickness(8, 0, 12, 0) };
            var nombre = new TextBlock { FontSize = 15, Foreground = Brushes.White };
            nombre.Inlines.Add(new System.Windows.Documents.Run(DocumentoCatalogo.Texto(t.Modulo["Nombre"])) { FontWeight = FontWeights.Bold });
            if (p != null)
                nombre.Inlines.Add(new System.Windows.Documents.Run($"   {p.VersionActual} → {p.VersionNueva}") { Foreground = Formularios.Pincel("#00D2FF") });
            info.Children.Add(nombre);
            info.Children.Add(new TextBlock
            {
                Text = Descripcion(t), TextWrapping = TextWrapping.Wrap,
                Foreground = Formularios.Pincel("#B8B8C4"), Margin = new Thickness(0, 4, 0, 0),
            });
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            var botones = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            if (p != null)
            {
                botones.Children.Add(Boton("📂 Ver SD", () => new VentanaArbolSD(DocumentoCatalogo.Texto(t.Modulo["Nombre"]), p).Show()));
                if (p.UrlRelease != null)
                    botones.Children.Add(Boton("📝 Notas", () => Process.Start(new ProcessStartInfo(p.UrlRelease) { UseShellExecute = true })));
            }
            if (p != null && t.Grupo != Grupo.Validacion)
            {
                botones.Children.Add(Boton("💾 Instalar en SD", async () => await InstalarPruebaAsync(t)));
                var aprobar = Boton(_aprobadasIds.Contains(t.Informe.Id) ? "✓ Aprobada" : "✓ Aprobar", () => Aprobar(t, preguntar: t.Grupo != Grupo.Lista));
                aprobar.Tag = t.Informe.Id;
                aprobar.IsEnabled = !_aprobadasIds.Contains(t.Informe.Id);
                botones.Children.Add(aprobar);
            }
            else if (p == null || t.Grupo == Grupo.Validacion)
            {
                botones.Children.Add(Boton("✎ Editar en Módulos", () => MessageBox.Show(
                    "Abre la sección «Módulos» para corregir este módulo a mano.", "NX-STUDIO")));
            }
            Grid.SetColumn(botones, 2);
            grid.Children.Add(botones);

            borde.Child = grid;
            return borde;
        }

        private static string Descripcion(Tarjeta t)
        {
            var p = t.Informe.Propuesta;
            if (p == null) return t.Informe.Detalle;
            if (t.Grupo == Grupo.Validacion || p.VersionActual == p.VersionNueva)
                return string.Join("\n", p.Avisos.Select(a => a.StartsWith("ℹ") ? a : "⚠ " + a));

            int nuevos = p.SdNueva.Count(r => !p.SdVieja.Contains(r));
            int quitados = p.SdVieja.Count(r => !p.SdNueva.Contains(r));
            var partes = new List<string>();
            if (p.EnlaceRoto) partes.Add("El enlace publicado ya no existe: los usuarios no pueden instalar este módulo.");
            if (p.ErrorEstructura != null) partes.Add("Estructura: " + p.ErrorEstructura);
            else if (!p.EnlaceRoto) partes.Add(nuevos == 0 && quitados == 0 ? "Estructura idéntica" : "Misma estructura");
            if (!p.EnlaceRoto && (nuevos > 0 || quitados > 0)) partes.Add($"+{nuevos} / −{quitados} archivos en la SD");
            partes.AddRange(p.Avisos.Select(a => "⚠ " + a));
            if (t.Grupo == Grupo.Revisar && p.ErrorEstructura == null && p.Avisos.Count == 0)
                partes.Add("⚠ " + t.Informe.Detalle);
            return string.Join("\n", partes);
        }

        // ── Acciones ───────────────────────────────────────────────────────

        private async Task InstalarPruebaAsync(Tarjeta t)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Elige la microSD o carpeta donde instalar la versión de prueba" };
            if (dlg.ShowDialog() != true) return;

            var version = (JsonObject)((JsonArray)t.Informe.Propuesta!.ModuloPropuesto["Versiones"]!)[0]!;
            _estado.Text = $"Instalando {DocumentoCatalogo.Texto(t.Modulo["Nombre"])} en {dlg.FolderName}…";
            try
            {
                var progreso = new Progress<string>(m => _estado.Text = m);
                string log = await InstaladorPrueba.InstalarAsync(version, dlg.FolderName, progreso);
                _estado.Text = "Instalación de prueba terminada.";
                MessageBox.Show(log + "\nPruébalo en la consola y, si funciona, pulsa «Aprobar».", "NX-STUDIO");
            }
            catch (Exception ex)
            {
                _estado.Text = "La instalación de prueba falló.";
                MessageBox.Show("No se pudo instalar: " + ex.Message, "NX-STUDIO", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Aprobar(Tarjeta t, bool preguntar)
        {
            var p = t.Informe.Propuesta;
            if (p == null || _aprobadasIds.Contains(t.Informe.Id)) return;
            if (preguntar && MessageBox.Show(
                    $"¿Aprobar {DocumentoCatalogo.Texto(t.Modulo["Nombre"])} {p.VersionNueva}?\n\nTiene avisos: revisa el árbol de la SD y pruébalo antes.",
                    "NX-STUDIO", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            var modulos = _doc.Lista("Modulos");
            int i = modulos.IndexOf(t.Modulo);
            if (i < 0) { MessageBox.Show("El módulo ya no está en el catálogo (¿se recargó?). Pulsa «Revisar ahora».", "NX-STUDIO"); return; }
            modulos[i] = p.ModuloPropuesto.DeepClone();

            _aprobadasIds.Add(t.Informe.Id);
            _alCambiar();
            ActualizarPie();
            MarcarBotonAprobado(_lista, t.Informe.Id);
        }

        private static void MarcarBotonAprobado(DependencyObject raiz, string id)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
            {
                var hijo = VisualTreeHelper.GetChild(raiz, i);
                if (hijo is Button { Tag: string tag } b && tag == id) { b.Content = "✓ Aprobada"; b.IsEnabled = false; }
                MarcarBotonAprobado(hijo, id);
            }
        }

        private void ActualizarPie() =>
            _aprobadas.Text = _aprobadasIds.Count == 0
                ? "Aprueba las versiones que hayas probado; se publican todas juntas."
                : $"{_aprobadasIds.Count} aprobada(s) sin publicar: {string.Join(", ", _aprobadasIds)}";

        private async Task PublicarAsync()
        {
            var errores = _doc.Validar();
            if (errores.Count > 0)
            {
                MessageBox.Show("No se puede publicar:\n\n• " + string.Join("\n• ", errores.Take(15)), "NX-STUDIO",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!_doc.Modificado)
            {
                MessageBox.Show("No hay cambios que publicar.", "NX-STUDIO");
                return;
            }
            if (MessageBox.Show("Se publicará el catálogo para TODOS los usuarios. ¿Continuar?",
                    "NX-STUDIO", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            _estado.Text = "Publicando…";
            string resumen = _aprobadasIds.Count > 0 ? string.Join(", ", _aprobadasIds) : "cambios manuales";
            var (ok, log) = await _doc.PublicarAsync($"catalogo: {resumen} (NX-STUDIO)");
            if (ok) _aprobadasIds.Clear();
            _alCambiar();
            ActualizarPie();
            _estado.Text = ok ? $"✓ Publicado a las {DateTime.Now:HH:mm}" : "✗ La publicación falló";
            if (!ok) MessageBox.Show(log, "NX-STUDIO", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static Button Boton(string texto, Action accion)
        {
            var b = new Button { Content = texto, Margin = new Thickness(4, 2, 0, 2) };
            b.Click += (_, _) => accion();
            return b;
        }

        private static Button Boton(string texto, Func<Task> accion)
        {
            var b = new Button { Content = texto, Margin = new Thickness(4, 2, 0, 2) };
            b.Click += async (_, _) =>
            {
                b.IsEnabled = false;
                try { await accion(); }
                finally { b.IsEnabled = true; }
            };
            return b;
        }
    }
}
