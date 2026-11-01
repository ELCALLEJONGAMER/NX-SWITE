using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Editor nombre → valor para secciones planas del catálogo (ConfiguracionUI,
    /// Sonidos, tools, tarjetasHubCfw). El JSON (comillas, dos puntos, escapes) se
    /// genera solo; las URLs de imagen muestran una vista previa.
    /// </summary>
    internal sealed class EditorClaves : DockPanel
    {
        private static readonly string[] NoImagen = { ".mp3", ".wav", ".ogg", ".zip", ".exe", ".uf2", ".7z", ".rar", ".bin" };

        private readonly JsonObject _seccion;
        private readonly Action _alCambiar;
        private readonly StackPanel _filas = new();

        public EditorClaves(DocumentoCatalogo doc, string seccion, string ayuda, Action alCambiar)
        {
            _alCambiar = alCambiar;
            if (doc.Raiz[seccion] is not JsonObject obj) doc.Raiz[seccion] = obj = new JsonObject();
            _seccion = obj;

            var cabecera = new StackPanel();
            cabecera.Children.Add(Formularios.Titulo(seccion));
            cabecera.Children.Add(Formularios.Ayuda(ayuda));
            var anadir = new Button { Content = "➕ Añadir campo", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 8) };
            anadir.Click += (_, _) => Anadir();
            cabecera.Children.Add(anadir);
            SetDock(cabecera, Dock.Top);
            Children.Add(cabecera);

            Children.Add(new ScrollViewer { Content = _filas, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Pintar();
        }

        private void Pintar()
        {
            _filas.Children.Clear();
            foreach (var clave in _seccion.Select(p => p.Key).ToList()) _filas.Children.Add(CrearFila(clave));
        }

        private void Anadir()
        {
            string clave = "NuevoCampo";
            for (int i = 2; _seccion.ContainsKey(clave); i++) clave = "NuevoCampo" + i;
            _seccion[clave] = "";
            _alCambiar();
            Pintar();
        }

        private UIElement CrearFila(string claveInicial)
        {
            string clave = claveInicial;
            var nodo = _seccion[clave];
            bool esTexto = nodo is JsonValue jv && jv.TryGetValue<string>(out _);
            string valor = nodo is null ? "" : esTexto ? DocumentoCatalogo.Texto(nodo) : nodo.ToJsonString();

            var borde = new Border
            {
                Background = Formularios.Pincel("#1C1C24"), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 8),
            };
            var rejilla = new Grid();
            rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
            rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var txtClave = new TextBox { Text = clave, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top, ToolTip = "Nombre" };
            var txtValor = new TextBox
            {
                Text = valor, TextWrapping = TextWrapping.Wrap, AcceptsReturn = valor.Length > 80,
                MaxHeight = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, ToolTip = "Valor",
            };
            var vista = new Image { Width = 40, Height = 40, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            var borrar = new BotonPeligro("🗑", () => { _seccion.Remove(clave); _alCambiar(); Pintar(); })
            {
                Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top, ToolTip = "Mantén pulsado para eliminar",
            };
            Grid.SetColumn(txtValor, 1); Grid.SetColumn(vista, 2); Grid.SetColumn(borrar, 3);
            rejilla.Children.Add(txtClave); rejilla.Children.Add(txtValor); rejilla.Children.Add(vista); rejilla.Children.Add(borrar);
            borde.Child = rejilla;
            Previsualizar(vista, valor);

            txtClave.LostFocus += (_, _) =>
            {
                string nueva = txtClave.Text.Trim();
                if (nueva == clave) return;
                if (nueva.Length == 0 || _seccion.ContainsKey(nueva))
                {
                    txtClave.BorderBrush = Brushes.OrangeRed;
                    txtClave.ToolTip = nueva.Length == 0 ? "El nombre no puede estar vacío" : "Ya existe un campo con ese nombre";
                    return;
                }
                RenombrarConservandoOrden(clave, nueva);
                clave = nueva;
                txtClave.ClearValue(Control.BorderBrushProperty);
                txtClave.ToolTip = "Nombre";
                _alCambiar();
            };

            txtValor.TextChanged += (_, _) =>
            {
                _seccion[clave] = Convertir(txtValor.Text, esTexto);
                Previsualizar(vista, txtValor.Text);
                _alCambiar();
            };
            return borde;
        }

        /// <summary>Respeta el tipo original: números, booleanos y objetos siguen siéndolo si el texto es válido.</summary>
        private static JsonNode? Convertir(string texto, bool eraTexto)
        {
            if (eraTexto) return texto;
            try { return JsonNode.Parse(texto); }
            catch (System.Text.Json.JsonException) { return texto; }
        }

        private void RenombrarConservandoOrden(string vieja, string nueva)
        {
            var pares = _seccion.ToList();
            _seccion.Clear();
            foreach (var (k, v) in pares) _seccion[k == vieja ? nueva : k] = v;
        }

        private static void Previsualizar(Image img, string valor)
        {
            bool url = valor.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                       && !NoImagen.Any(e => valor.Split('?')[0].EndsWith(e, StringComparison.OrdinalIgnoreCase));
            img.Visibility = url ? Visibility.Visible : Visibility.Collapsed;
            if (url) Formularios.MostrarImagen(img, valor); else img.Source = null;
        }
    }
}
