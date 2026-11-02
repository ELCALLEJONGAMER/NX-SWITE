using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Editor en tabla de <c>ConfiguracionUI.tabla_master_keys</c>. Cada fila es una
    /// master key con su rango de firmware y la Atmosphère mínima; el texto con
    /// separadores <c>|</c> que lee la app se genera solo.
    /// </summary>
    internal sealed partial class EditorMasterKeys : DockPanel
    {
        private const string Clave = "tabla_master_keys";

        private sealed class Fila
        {
            public string MasterKey = "";
            public string Firmware = "";
            public string Atmosphere = "";
        }

        private readonly JsonObject _seccion;
        private readonly Action _alCambiar;
        private readonly List<Fila> _filas;
        private readonly StackPanel _contenedor = new();

        public EditorMasterKeys(DocumentoCatalogo doc, Action alCambiar)
        {
            _alCambiar = alCambiar;
            _seccion = doc.Objeto("ConfiguracionUI");
            _filas = Leer(DocumentoCatalogo.Texto(_seccion[Clave]));

            var cabecera = new StackPanel();
            cabecera.Children.Add(Formularios.Titulo("Master keys"));
            cabecera.Children.Add(Formularios.Ayuda(
                "Relación entre la master_key más alta de prod.keys, el firmware compatible y la Atmosphère mínima. " +
                "Formato de versión: 23.0.0, 23.0.0-23.0.1 o 23.x. Cuando salga un firmware con clave nueva, pulsa «Añadir siguiente»."));
            var anadir = new Button { Content = "➕ Añadir siguiente master key", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 8) };
            anadir.Click += (_, _) => AnadirSiguiente();
            cabecera.Children.Add(anadir);
            cabecera.Children.Add(CrearEncabezado());
            SetDock(cabecera, Dock.Top);
            Children.Add(cabecera);

            Children.Add(new ScrollViewer { Content = _contenedor, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Pintar();
        }

        // ── Lectura / escritura ────────────────────────────────────────────

        private static List<Fila> Leer(string texto)
        {
            var filas = new List<Fila>();
            foreach (string trozo in texto.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var m = RegexEntrada().Match(trozo);
                if (!m.Success) continue;
                string[] partes = m.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries);
                filas.Add(new Fila
                {
                    MasterKey  = m.Groups[1].Value.ToLowerInvariant(),
                    Firmware   = partes.ElementAtOrDefault(0) ?? "",
                    Atmosphere = partes.ElementAtOrDefault(1) ?? "",
                });
            }
            return filas.OrderBy(f => Indice(f.MasterKey)).ToList();
        }

        private void Guardar()
        {
            _seccion[Clave] = string.Join(" | ", _filas.Select(f => $"{f.MasterKey}: {f.Firmware.Trim()}, {f.Atmosphere.Trim()}"));
            _alCambiar();
        }

        private static int Indice(string masterKey) =>
            int.TryParse(masterKey.Replace("master_key_", ""), NumberStyles.HexNumber, null, out int n) ? n : int.MaxValue;

        // ── UI ─────────────────────────────────────────────────────────────

        private static Grid CrearRejilla()
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            return g;
        }

        private static UIElement CrearEncabezado()
        {
            var g = CrearRejilla();
            g.Margin = new Thickness(10, 0, 10, 4);
            string[] titulos = { "Master key", "Firmware (HOS)", "Atmosphère mínima" };
            for (int i = 0; i < titulos.Length; i++)
            {
                var t = new TextBlock { Text = titulos[i], Style = (Style)Application.Current.Resources["Etiqueta"] };
                Grid.SetColumn(t, i);
                g.Children.Add(t);
            }
            return g;
        }

        private void Pintar()
        {
            _contenedor.Children.Clear();
            for (int i = 0; i < _filas.Count; i++) _contenedor.Children.Add(CrearFila(_filas[i], i == _filas.Count - 1));
        }

        private UIElement CrearFila(Fila fila, bool esUltima)
        {
            var borde = new Border
            {
                Background = Formularios.Pincel("#1C1C24"), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 4),
            };
            var g = CrearRejilla();

            var nombre = new TextBlock
            {
                Text = $"{fila.MasterKey}  ({Indice(fila.MasterKey)})", VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas"), Foreground = Formularios.Pincel("#C0C0D0"),
            };
            var firmware = CrearCaja(fila.Firmware, v => fila.Firmware = v, RegexRango());
            var atmos = CrearCaja(fila.Atmosphere, v => fila.Atmosphere = v, RegexVersion());
            Grid.SetColumn(firmware, 1);
            Grid.SetColumn(atmos, 2);
            g.Children.Add(nombre);
            g.Children.Add(firmware);
            g.Children.Add(atmos);

            if (esUltima)
            {
                var borrar = new BotonPeligro("🗑", () => { _filas.Remove(fila); Guardar(); Pintar(); })
                {
                    Margin = new Thickness(8, 0, 0, 0), ToolTip = "Mantén pulsado para eliminar la última fila",
                };
                Grid.SetColumn(borrar, 3);
                g.Children.Add(borrar);
            }

            borde.Child = g;
            return borde;
        }

        private TextBox CrearCaja(string valor, Action<string> asignar, Regex formato)
        {
            var txt = new TextBox { Text = valor, Margin = new Thickness(0, 0, 8, 0) };
            void Validar()
            {
                bool ok = formato.IsMatch(txt.Text.Trim());
                if (ok) txt.ClearValue(Control.BorderBrushProperty); else txt.BorderBrush = Brushes.OrangeRed;
                txt.ToolTip = ok ? null : "Formato no válido";
            }
            Validar();
            txt.TextChanged += (_, _) => { asignar(txt.Text); Validar(); Guardar(); };
            return txt;
        }

        private void AnadirSiguiente()
        {
            int siguiente = _filas.Count == 0 ? 0 : _filas.Max(f => Indice(f.MasterKey)) + 1;
            var ultima = _filas.LastOrDefault();
            _filas.Add(new Fila
            {
                MasterKey  = $"master_key_{siguiente:x2}",
                Firmware   = ultima != null && int.TryParse(ultima.Firmware.Split('.')[0], out int mayor) ? $"{mayor + 1}.0.0" : "",
                Atmosphere = "",
            });
            Guardar();
            Pintar();
        }

        [GeneratedRegex(@"^\s*(?:""[^""]*""\s*:\s*"")?(master_key_[0-9a-fA-F]{2})\s*:\s*(.*)$")]
        private static partial Regex RegexEntrada();

        [GeneratedRegex(@"^\d+\.(?:x|\d+\.\d+)(?:\s*-\s*\d+\.(?:x|\d+\.\d+))?$")]
        private static partial Regex RegexRango();

        [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
        private static partial Regex RegexVersion();
    }
}
