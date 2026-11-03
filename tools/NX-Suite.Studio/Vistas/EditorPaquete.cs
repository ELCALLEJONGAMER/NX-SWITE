using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NX_Suite.Catalogo;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Editor visual de cómo cae un paquete en la microSD: a la izquierda cada
    /// archivo del paquete con su destino editable (renombrar/mover), si se
    /// incluye y si se firma por SHA256; a la derecha el árbol final de la SD.
    /// Lo usan el asistente de módulos nuevos y la ficha de módulos existentes.
    /// </summary>
    internal sealed class EditorPaquete : DockPanel
    {
        private readonly string _asset;
        private readonly string _id;
        private readonly List<ArchivoMapa> _archivos;
        private readonly StackPanel _filas = new();
        private readonly TextBox _arbol = new()
        {
            IsReadOnly = true, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 300, MaxHeight = 520,
        };
        private readonly TextBlock _resumen = Ui.Texto("", Ui.Tenue);
        private readonly TextBox _padre = new() { FontFamily = new FontFamily("Consolas") };
        private readonly CheckBox _quitarRaiz = new() { Content = "Quitar carpeta raíz del paquete", VerticalAlignment = VerticalAlignment.Center };
        private readonly CheckBox _limpiar = new() { Content = "Limpiar caché tras instalar", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };

        public IReadOnlyList<ArchivoMapa> Archivos => _archivos;
        public bool LimpiarCache => _limpiar.IsChecked == true;
        public event Action? Cambiado;

        public EditorPaquete(string asset, string idModulo, List<ArchivoMapa> archivos, bool limpiarCache = true)
        {
            _asset = asset;
            _id = idModulo;
            _archivos = archivos;
            _limpiar.IsChecked = limpiarCache;

            var aplicar = Ui.Boton("↻ Recalcular destinos", Recalcular,
                ayuda: "Vuelve a proponer todos los destinos aplicando la carpeta padre y la opción de raíz.");
            var todoSha = Ui.Boton("SHA en principales", () =>
            {
                foreach (var a in _archivos) a.UsarSha = a.Incluir && a.Sha != null && EsPrincipal(a.Origen);
                Pintar();
            }, ayuda: "Marca SHA256 en .nro/.ovl/.bin/.nsp/package3; el resto queda por presencia.");
            var opciones = Ui.Rejilla(4,
                Ui.Campo("Carpeta padre en la SD (opcional)", _padre, "Ej. /firmware para paquetes servidos en crudo."),
                Ui.Campo("Opciones", Apilar(_quitarRaiz, _limpiar)),
                Ui.Campo(" ", aplicar),
                Ui.Campo(" ", todoSha));
            SetDock(opciones, Dock.Top);
            Children.Add(opciones);
            SetDock(_resumen, Dock.Top);
            Children.Add(_resumen);
            _limpiar.Click += (_, _) => Cambiado?.Invoke();

            var cuerpo = new Grid();
            cuerpo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            cuerpo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            var izq = new StackPanel();
            izq.Children.Add(Cabecera());
            izq.Children.Add(new ScrollViewer { Content = _filas, MaxHeight = 520, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            cuerpo.Children.Add(Ui.Tarjeta(izq));
            var der = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            der.Children.Add(Ui.Etiqueta("Así quedará la microSD"));
            der.Children.Add(_arbol);
            Grid.SetColumn(der, 1);
            cuerpo.Children.Add(der);
            Children.Add(cuerpo);
            Pintar();
        }

        private static StackPanel Apilar(params UIElement[] e)
        {
            var p = new StackPanel();
            foreach (var x in e) p.Children.Add(x);
            return p;
        }

        private static bool EsPrincipal(string ruta)
        {
            string ext = System.IO.Path.GetExtension(ruta).ToLowerInvariant();
            return ext is ".nro" or ".ovl" or ".bin" or ".nsp" || ruta.EndsWith("package3", StringComparison.OrdinalIgnoreCase);
        }

        private void Recalcular()
        {
            string padre = _padre.Text.Trim();
            if (padre.Length > 0) padre = MapaPaquete.Normalizar(padre);
            foreach (var a in _archivos)
                a.Destino = MapaPaquete.DestinoPorDefecto(_asset, a.Origen, _id, padre, _quitarRaiz.IsChecked == true);
            Pintar();
        }

        private static Grid Fila(UIElement a, UIElement b, UIElement c, UIElement d)
        {
            var g = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            Grid.SetColumn(b, 1); Grid.SetColumn(c, 2); Grid.SetColumn(d, 3);
            g.Children.Add(a); g.Children.Add(b); g.Children.Add(c); g.Children.Add(d);
            return g;
        }

        private static UIElement Cabecera()
        {
            TextBlock T(string s) => new() { Text = s, Foreground = Formularios.Pincel(Ui.Tenue), FontSize = 11, FontWeight = FontWeights.Bold };
            var f = Fila(T("✓"), T("EN EL PAQUETE"), T("DESTINO EN LA SD (edita para renombrar/mover)"), T("FIRMA"));
            f.Margin = new Thickness(0, 0, 0, 6);
            return f;
        }

        private void Pintar()
        {
            _filas.Children.Clear();
            foreach (var a in _archivos)
            {
                var incluir = new CheckBox { IsChecked = a.Incluir, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Desmarca para excluirlo de la microSD" };
                var origen = new TextBlock
                {
                    Text = a.Origen, FontFamily = new FontFamily("Consolas"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = a.Origen, Margin = new Thickness(0, 0, 8, 0),
                    Foreground = a.Incluir ? Brushes.White : Formularios.Pincel("#5A5A68"),
                };
                var destino = new TextBox { Text = a.Destino, FontFamily = new FontFamily("Consolas"), FontSize = 11, IsEnabled = a.Incluir };
                var sha = Ui.Chip(a.UsarSha ? "SHA256" : "Presencia", a.UsarSha, on => { a.UsarSha = on; Pintar(); },
                                  a.Sha ?? "Sin hash disponible");
                sha.IsEnabled = a.Incluir && a.Sha != null;
                sha.Margin = new Thickness(6, 0, 0, 0);

                incluir.Click += (_, _) => { a.Incluir = incluir.IsChecked == true; if (!a.Incluir) a.UsarSha = false; Pintar(); };
                destino.LostFocus += (_, _) =>
                {
                    string d = destino.Text.Trim();
                    if (d.Length == 0) { destino.Text = a.Destino; return; }
                    if (d.EndsWith('/')) d += System.IO.Path.GetFileName(a.Origen);
                    d = MapaPaquete.Normalizar(d);
                    if (d == a.Destino) return;
                    a.Destino = d;
                    Pintar();
                };
                _filas.Children.Add(Fila(incluir, origen, destino, sha));
            }
            PintarArbol();
            Cambiado?.Invoke();
        }

        private void PintarArbol()
        {
            var incl = _archivos.Where(a => a.Incluir).ToList();
            var duplicados = incl.GroupBy(a => a.Destino, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var sb = new System.Text.StringBuilder();
            string[] previo = Array.Empty<string>();
            foreach (var a in incl.OrderBy(a => a.Destino, StringComparer.OrdinalIgnoreCase))
            {
                var partes = a.Destino.Trim('/').Split('/');
                int comun = 0;
                while (comun < partes.Length - 1 && comun < previo.Length - 1 && partes[comun].Equals(previo[comun], StringComparison.OrdinalIgnoreCase)) comun++;
                for (int i = comun; i < partes.Length - 1; i++) sb.AppendLine(new string(' ', i * 2) + "📁 " + partes[i]);
                sb.Append(new string(' ', (partes.Length - 1) * 2)).Append(a.UsarSha ? "🔒 " : "· ").Append(partes[^1]);
                if (duplicados.Contains(a.Destino)) sb.Append("   ⚠ duplicado");
                sb.AppendLine();
                previo = partes;
            }
            _arbol.Text = sb.Length == 0 ? "(no se copiará nada)" : sb.ToString();
            int sha = incl.Count(a => a.UsarSha);
            _resumen.Text = $"{_archivos.Count} en el paquete · {incl.Count} a la microSD · {_archivos.Count - incl.Count} excluidos · {sha} con SHA256" +
                            (duplicados.Count > 0 ? $"  ·  ⚠ {duplicados.Count} destino(s) repetido(s)" : "") +
                            (incl.Count > 0 && sha == 0 ? "  ·  ℹ sin SHA256 no se distingue la versión exacta" : "");
        }

        /// <summary>Error que impide guardar, o null.</summary>
        public string? Validar()
        {
            var incl = _archivos.Where(a => a.Incluir).ToList();
            if (incl.Count == 0) return "No se copia ningún archivo a la microSD.";
            if (incl.GroupBy(a => a.Destino, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) return "Hay destinos repetidos en la SD.";
            return null;
        }
    }
}
