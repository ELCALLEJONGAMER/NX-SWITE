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
    internal sealed partial class VistaActualizaciones : DockPanel
    {
        private enum Grupo { Urgente, Revisar, Lista, Manual, Validacion }
        private enum Filtro { Pendientes, Listas, Revisar, Problemas, AlDia, Todos }

        private sealed record Tarjeta(JsonObject Modulo, InformeModulo Informe, Grupo Grupo);

        private readonly DocumentoCatalogo _doc;
        private readonly Action _alCambiar;
        private readonly StackPanel _lista = new();
        private readonly WrapPanel _chips = new() { Margin = new Thickness(0, 0, 0, 12) };
        private readonly TextBox _buscar = new() { Width = 220, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Buscar módulo" };
        private readonly ContentControl _cuerpo = new();
        private readonly ScrollViewer _scrollTarjetas;
        private readonly TextBlock _estado = new() { Foreground = Formularios.Pincel("#9A9AA8"), VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _aprobadas = new() { Foreground = Formularios.Pincel("#00D2FF"), FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
        private readonly Button _revisar;
        private static readonly List<string> _aprobadasIds = new();
        private static List<Tarjeta>? _ultimaRevision;
        private static string _ultimoEstado = "";
        private static Filtro _filtro = Filtro.Pendientes;

        public VistaActualizaciones(DocumentoCatalogo doc, Action alCambiar)
        {
            _doc = doc;
            _alCambiar = alCambiar;

            // Cabecera
            var cab = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            _revisar = Boton("⟳ Revisar todo", async () => await RevisarAsync());
            _revisar.ToolTip = "Busca versiones nuevas en GitHub y valida la estructura de los módulos al día. Usa la caché de paquetes.";
            DockPanel.SetDock(_revisar, Dock.Right);
            cab.Children.Add(_revisar);
            var nuevo = Boton("+ Nuevo módulo", NuevoModulo);
            DockPanel.SetDock(nuevo, Dock.Right);
            cab.Children.Add(nuevo);
            var token = Boton("🔑 Token", () => { new VentanaToken().ShowDialog(); });
            DockPanel.SetDock(token, Dock.Right);
            cab.Children.Add(token);
            var cache = new BotonPeligro("🗑 Caché", () =>
            {
                if (MessageBox.Show("¿Vaciar la caché de paquetes? La próxima revisión volverá a descargarlo todo.", "NX-STUDIO",
                        MessageBoxButton.YesNo) == MessageBoxResult.Yes) CachePaquetes.Vaciar();
            });
            DockPanel.SetDock(cache, Dock.Right);
            cab.Children.Add(cache);
            DockPanel.SetDock(_buscar, Dock.Right);
            _buscar.Margin = new Thickness(0, 2, 8, 2);
            cab.Children.Add(_buscar);
            var titulos = new StackPanel();
            titulos.Children.Add(Formularios.Titulo("Módulos"));
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

            var contenidoTarjetas = new DockPanel();
            DockPanel.SetDock(_chips, Dock.Top);
            contenidoTarjetas.Children.Add(_chips);
            contenidoTarjetas.Children.Add(_lista);
            _scrollTarjetas = new ScrollViewer { Content = contenidoTarjetas, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Children.Add(_cuerpo);
            _buscar.TextChanged += (_, _) => PintarTarjetas();

            ActualizarPie();
            // Solo se consulta GitHub al pulsar «Revisar todo»; al abrir se muestra la última revisión guardada.
            if (_ultimaRevision == null && CargarRevision(_doc) is { } guardada) { _ultimaRevision = guardada.Tarjetas; _ultimoEstado = guardada.Estado; }
            _estado.Text = _ultimaRevision != null ? _ultimoEstado : "Pulsa «⟳ Revisar todo» para buscar actualizaciones y validar estructuras.";
            PintarTarjetas();
        }

        // ── Revisión ───────────────────────────────────────────────────────

        /// <summary>
        /// Revisión unificada: busca versiones nuevas en GitHub y, en los módulos
        /// que ya están al día, valida que el paquete publicado siga cuadrando con
        /// el pipeline y la firma. Los paquetes se leen de la caché si no cambiaron.
        /// </summary>
        private async Task RevisarAsync()
        {
            _revisar.IsEnabled = false;
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
                    _estado.Text = $"Revisando {n}/{modulos.Count}: {DocumentoCatalogo.Texto(original["Nombre"])}…";
                    InformeModulo inf;
                    Grupo? grupo;
                    try
                    {
                        // Sobre una copia: el catálogo no cambia hasta aprobar.
                        inf = await act.ProcesarAsync((JsonObject)original.DeepClone(), aplicar: true);
                        grupo = Clasificar(inf);
                        if (grupo == null && inf.Resultado == ResultadoModulo.AlDia)
                        {
                            var val = await act.ValidarEstructuraAsync((JsonObject)original.DeepClone());
                            if (val.Resultado is not (ResultadoModulo.AlDia or ResultadoModulo.Omitido))
                            {
                                inf = val;
                                grupo = val.Propuesta?.EnlaceRoto == true ? Grupo.Urgente : Grupo.Validacion;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.StartsWith("Límite de la API")) throw;
                        inf = new InformeModulo(DocumentoCatalogo.Texto(original["Id"]), ResultadoModulo.Manual, ex.Message);
                        grupo = Grupo.Manual;
                    }

                    if (grupo == null) { if (inf.Resultado == ResultadoModulo.AlDia) correctos++; continue; }
                    tarjetas.Add(new Tarjeta(original, inf, grupo.Value));
                }
                _estado.Text = $"Revisión: {DateTime.Now:dd/MM HH:mm} · {correctos} al día · {tarjetas.Count} pendientes";
            }
            catch (Exception ex)
            {
                _estado.Text = "Error: " + ex.Message;
            }
            finally { _revisar.IsEnabled = true; }

            _ultimoEstado = _estado.Text;
            GuardarRevision(tarjetas, _ultimoEstado);
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
            _ultimaRevision = tarjetas;
            PintarTarjetas();
        }

        private static string NombreFiltro(Filtro f) => f switch
        {
            Filtro.Pendientes => "Pendientes",
            Filtro.Listas => "🟢 Listas",
            Filtro.Revisar => "🟠 Revisar",
            Filtro.Problemas => "🔴 Problemas",
            Filtro.AlDia => "✓ Al día",
            _ => "Todos",
        };

        private static bool Pasa(Grupo? g, Filtro f) => f switch
        {
            Filtro.Pendientes => g != null,
            Filtro.Listas => g == Grupo.Lista,
            Filtro.Revisar => g == Grupo.Revisar,
            Filtro.Problemas => g is Grupo.Urgente or Grupo.Manual or Grupo.Validacion,
            Filtro.AlDia => g == null,
            _ => true,
        };

        private static int Orden(Grupo? g) => g switch
        {
            Grupo.Urgente => 0, Grupo.Revisar => 1, Grupo.Validacion => 2, Grupo.Manual => 3, Grupo.Lista => 4, _ => 5,
        };

        private static string ColorDe(Grupo? g) => g switch
        {
            Grupo.Urgente => "#FF4D4D", Grupo.Revisar or Grupo.Validacion => "#FFA640",
            Grupo.Manual => "#9A9AA8", Grupo.Lista => "#3DDC84", _ => "#3A3A48",
        };

        private string EtiquetaDe(Grupo? g) => g switch
        {
            Grupo.Urgente => "ENLACE ROTO", Grupo.Revisar => "ESTRUCTURA CAMBIÓ", Grupo.Validacion => "FIRMA / ESTRUCTURA",
            Grupo.Manual => "ATENCIÓN MANUAL", Grupo.Lista => "LISTA PARA APROBAR",
            _ => _ultimaRevision == null ? "SIN REVISAR" : "AL DÍA",
        };

        private void PintarTarjetas()
        {
            _cuerpo.Content = _scrollTarjetas;
            _lista.Children.Clear();
            PintarChips();
            var porId = (_ultimaRevision ?? new()).GroupBy(t => t.Informe.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            string q = _buscar.Text.Trim();
            var visibles = _doc.Lista("Modulos").OfType<JsonObject>()
                .Select(m => (Modulo: m, Tarjeta: porId.GetValueOrDefault(DocumentoCatalogo.Texto(m["Id"]))))
                .Where(x => Pasa(x.Tarjeta?.Grupo, _filtro))
                .Where(x => q.Length == 0 || DocumentoCatalogo.Texto(x.Modulo["Nombre"]).Contains(q, StringComparison.OrdinalIgnoreCase)
                                          || DocumentoCatalogo.Texto(x.Modulo["Id"]).Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => EsFavorito(x.Modulo)).ThenBy(x => OrdenFavorito(x.Modulo))
                .ThenBy(x => Orden(x.Tarjeta?.Grupo)).ThenBy(x => DocumentoCatalogo.Texto(x.Modulo["Nombre"]))
                .ToList();

            if (visibles.Count == 0)
            {
                _lista.Children.Add(new TextBlock
                {
                    Text = _filtro == Filtro.Pendientes ? "✓ Todo al día. No hay nada pendiente." : "No hay módulos con este filtro.",
                    Foreground = Formularios.Pincel("#3DDC84"), FontSize = 16, Margin = new Thickness(0, 30, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
                return;
            }
            var rejilla = new WrapPanel();
            foreach (var (m, t) in visibles) rejilla.Children.Add(CrearTarjeta(m, t));
            _lista.Children.Add(rejilla);
        }

        private void PintarChips()
        {
            _chips.Children.Clear();
            foreach (var f in Enum.GetValues<Filtro>())
            {
                var filtro = f;
                var chip = Boton(NombreFiltro(f), () => { _filtro = filtro; PintarTarjetas(); });
                chip.Padding = new Thickness(12, 4, 12, 4);
                chip.Margin = new Thickness(0, 0, 6, 0);
                if (f == _filtro) { chip.Background = Formularios.Pincel("#00D2FF"); chip.Foreground = Brushes.Black; chip.FontWeight = FontWeights.Bold; }
                _chips.Children.Add(chip);
            }
            var listas = (_ultimaRevision ?? new()).Where(t => t.Grupo == Grupo.Lista && !_aprobadasIds.Contains(t.Informe.Id)).ToList();
            if (listas.Count > 0)
                _chips.Children.Add(Boton($"✓ Aprobar las {listas.Count} listas", () =>
                {
                    foreach (var t in listas) Aprobar(t, preguntar: false);
                    PintarTarjetas();
                }));
        }

        private Border CrearTarjeta(JsonObject m, Tarjeta? t)
        {
            var p = t?.Informe.Propuesta;
            string color = ColorDe(t?.Grupo);
            bool aprobada = t != null && _aprobadasIds.Contains(t.Informe.Id);
            var borde = new Border
            {
                Width = 300, Background = Formularios.Pincel("#1C1C24"), BorderBrush = Formularios.Pincel(color),
                BorderThickness = new Thickness(0, 3, 0, 0), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 0, 12, 12), Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = t != null ? Descripcion(t) : null,
            };
            var pila = new StackPanel();

            var cab = new DockPanel();
            bool favorito = EsFavorito(m);
            var estrella = new Button
            {
                Content = favorito ? "★" : "☆", FontSize = 20, Padding = new Thickness(4, 0, 4, 0),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Top,
                Foreground = Formularios.Pincel(favorito ? "#FFC83D" : "#5A5A68"),
                ToolTip = favorito ? "Favorito: lo instala el asistente. Pulsa para quitarlo." : "Marcar como favorito (lo instalará el asistente)",
            };
            estrella.Click += (_, _) => { AlternarFavorito(m); PintarTarjetas(); };
            DockPanel.SetDock(estrella, Dock.Right);
            cab.Children.Add(estrella);
            var icono = new Image { Width = 44, Height = 44, Margin = new Thickness(0, 0, 10, 0) };
            Formularios.MostrarImagen(icono, DocumentoCatalogo.Texto(m["IconoUrl"]));
            DockPanel.SetDock(icono, Dock.Left);
            cab.Children.Add(icono);
            var nombres = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            nombres.Children.Add(new TextBlock
            {
                Text = DocumentoCatalogo.Texto(m["Nombre"]), Foreground = Brushes.White, FontSize = 15,
                FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis,
            });
            string actual = (m["Versiones"] as JsonArray)?.FirstOrDefault() is JsonObject v ? DocumentoCatalogo.Texto(v["Version"]) : "—";
            nombres.Children.Add(new TextBlock
            {
                Text = p != null && p.VersionNueva != p.VersionActual ? $"{p.VersionActual}  →  {p.VersionNueva}" : actual,
                Foreground = Formularios.Pincel(p != null && p.VersionNueva != p.VersionActual ? "#00D2FF" : "#9A9AA8"),
            });
            cab.Children.Add(nombres);
            pila.Children.Add(cab);

            pila.Children.Add(new Border
            {
                Background = Formularios.Pincel(color), CornerRadius = new CornerRadius(4), Opacity = 0.9,
                Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = aprobada ? "✓ APROBADA (sin publicar)" : EtiquetaDe(t?.Grupo), FontSize = 11, FontWeight = FontWeights.Bold,
                    Foreground = t?.Grupo == null && !aprobada ? Formularios.Pincel("#B8B8C4") : Brushes.Black,
                },
            });
            if (t != null)
                pila.Children.Add(new TextBlock
                {
                    Text = Descripcion(t), Foreground = Formularios.Pincel("#B8B8C4"), TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 52, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 8, 0, 0), FontSize = 12,
                });

            var botones = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            if (t?.Grupo == Grupo.Lista && p != null)
            {
                var aprobar = Boton(aprobada ? "✓ Aprobada" : "✓ Aprobar", () => { Aprobar(t, preguntar: false); PintarTarjetas(); });
                aprobar.IsEnabled = !aprobada;
                botones.Children.Add(aprobar);
            }
            if (p != null) botones.Children.Add(Boton("📂 SD", () => new VentanaArbolSD(DocumentoCatalogo.Texto(m["Nombre"]), p).Show()));
            botones.Children.Add(Boton("Abrir ›", () => AbrirModulo(m, t)));
            pila.Children.Add(botones);

            borde.Child = pila;
            borde.MouseLeftButtonUp += (_, e) => { if (e.OriginalSource is not DependencyObject d || !EsBoton(d)) AbrirModulo(m, t); };
            return borde;
        }

        // Favoritos = lista «Recomendados» del catálogo (la que instala el asistente).
        private JsonObject? Favorito(JsonObject m)
        {
            string id = DocumentoCatalogo.Texto(m["Id"]);
            return _doc.Lista("Recomendados").OfType<JsonObject>()
                .FirstOrDefault(r => string.Equals(DocumentoCatalogo.Texto(r["Id"]), id, StringComparison.OrdinalIgnoreCase));
        }

        private bool EsFavorito(JsonObject m) => Favorito(m) != null;

        private int OrdenFavorito(JsonObject m) =>
            Favorito(m)?["Orden"] is JsonValue v && v.TryGetValue<int>(out int n) ? n : int.MaxValue;

        private void AlternarFavorito(JsonObject m)
        {
            var lista = _doc.Lista("Recomendados");
            if (Favorito(m) is { } existente)
                lista.Remove(existente);
            else
                lista.Add(new JsonObject { ["Id"] = DocumentoCatalogo.Texto(m["Id"]), ["Obligatorio"] = false });
            int i = 1;
            foreach (var r in lista.OfType<JsonObject>()) r["Orden"] = i++;
            _alCambiar();
        }

        private static bool EsBoton(DependencyObject d)
        {
            for (var x = d; x != null; x = VisualTreeHelper.GetParent(x)) if (x is System.Windows.Controls.Primitives.ButtonBase) return true;
            return false;
        }

        // ── Detalle de un módulo ───────────────────────────────────────────

        private void AbrirModulo(JsonObject m, Tarjeta? t)
        {
            var raiz = new DockPanel();
            var cab = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            cab.Children.Add(Boton("‹ Volver a las tarjetas", PintarTarjetas));
            var eliminar = new BotonPeligro("× Eliminar módulo", () =>
            {
                if (MessageBox.Show($"¿Eliminar el módulo «{DocumentoCatalogo.Texto(m["Nombre"])}» del catálogo?",
                        "NX-STUDIO", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                _doc.Lista("Modulos").Remove(m);
                _alCambiar();
                PintarTarjetas();
            });
            DockPanel.SetDock(eliminar, Dock.Right);
            cab.Children.Insert(0, eliminar);
            DockPanel.SetDock(cab, Dock.Top);
            raiz.Children.Add(cab);

            var p = t?.Informe.Propuesta;
            if (t != null && !_aprobadasIds.Contains(t.Informe.Id))
            {
                var aviso = new Border
                {
                    Background = Formularios.Pincel("#1C1C24"), BorderBrush = Formularios.Pincel(ColorDe(t.Grupo)),
                    BorderThickness = new Thickness(3, 0, 0, 0), CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 10),
                };
                var dock = new DockPanel();
                var acciones = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
                if (p != null)
                {
                    acciones.Children.Add(Boton("📂 Ver SD", () => new VentanaArbolSD(DocumentoCatalogo.Texto(m["Nombre"]), p).Show()));
                    if (p.UrlRelease != null)
                        acciones.Children.Add(Boton("🌐 Release", () => Process.Start(new ProcessStartInfo(p.UrlRelease) { UseShellExecute = true })));
                    if (t.Grupo != Grupo.Validacion)
                    {
                        acciones.Children.Add(Boton("💾 Instalar en SD", async () => await InstalarPruebaAsync(t)));
                        acciones.Children.Add(Boton($"✓ Aprobar {p.VersionNueva}", () =>
                        {
                            Aprobar(t, preguntar: t.Grupo != Grupo.Lista);
                            if (_aprobadasIds.Contains(t.Informe.Id) && _doc.Lista("Modulos").OfType<JsonObject>()
                                    .FirstOrDefault(x => DocumentoCatalogo.Texto(x["Id"]) == t.Informe.Id) is { } nuevo)
                                AbrirModulo(nuevo, t);
                        }));
                    }
                }
                DockPanel.SetDock(acciones, Dock.Right);
                dock.Children.Add(acciones);
                var textos = new StackPanel();
                textos.Children.Add(new TextBlock
                {
                    Text = EtiquetaDe(t.Grupo) + (p != null && p.VersionNueva != p.VersionActual ? $"  ·  {p.VersionActual} → {p.VersionNueva}" : ""),
                    Foreground = Formularios.Pincel(ColorDe(t.Grupo)), FontWeight = FontWeights.Bold,
                });
                textos.Children.Add(new TextBlock { Text = Descripcion(t), Foreground = Formularios.Pincel("#B8B8C4"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 12, 0) });
                dock.Children.Add(textos);
                aviso.Child = dock;
                DockPanel.SetDock(aviso, Dock.Top);
                raiz.Children.Add(aviso);
            }

            raiz.Children.Add(VistaModulos.Ficha(_doc, _alCambiar, m));
            _cuerpo.Content = raiz;
        }

        private void NuevoModulo()
        {
            var nuevo = new JsonObject
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
            _doc.Lista("Modulos").Add(nuevo);
            _alCambiar();
            AbrirModulo(nuevo, null);
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
            if (!await _doc.HayCambiosSinPublicarAsync())
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
