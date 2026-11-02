using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using NX_Suite.Catalogo;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Editor guiado de una restricción de versión ("Firmware" o "Atmos") de una
    /// versión de módulo. Genera el texto que entiende NX-Swite ("&lt;=22.5.0",
    /// "&gt;=1.11.2") a partir de un tipo de límite y una versión elegida de la
    /// lista del catálogo (o escrita a mano).
    /// </summary>
    internal static class EditorRestriccion
    {
        private static readonly (string Texto, string Operador)[] Tipos =
        {
            ("Sin restricción", ""),
            ("Hasta (máximo compatible, <=)", "<="),
            ("Desde (mínimo requerido, >=)", ">="),
            ("Anterior a (<)", "<"),
            ("Posterior a (>)", ">"),
        };

        public static UIElement Crear(JsonObject version, string clave, string etiqueta,
                                      IEnumerable<string> versionesConocidas, Action alCambiar)
        {
            var bloque = new StackPanel();
            bloque.Children.Add(new TextBlock { Text = etiqueta, Style = (Style)Application.Current.Resources["Etiqueta"] });

            var (opActual, verActual) = Separar(DocumentoCatalogo.Texto(version[clave]));

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var tipo = new ComboBox { ItemsSource = Tipos.Select(t => t.Texto).ToList(), Margin = new Thickness(0, 0, 8, 0) };
            int idx = Array.FindIndex(Tipos, t => t.Operador == opActual);
            tipo.SelectedIndex = verActual.Length == 0 ? 0 : (idx < 0 ? 2 : idx);

            var numero = new ComboBox
            {
                IsEditable = true,
                ItemsSource = versionesConocidas.Distinct().ToList(),
                Text = verActual,
                IsEnabled = tipo.SelectedIndex != 0,
            };
            Grid.SetColumn(numero, 1);
            grid.Children.Add(tipo);
            grid.Children.Add(numero);
            bloque.Children.Add(grid);

            var resumen = Formularios.Ayuda(Resumen(DocumentoCatalogo.Texto(version[clave])));
            bloque.Children.Add(resumen);

            void Aplicar()
            {
                string op = Tipos[Math.Max(0, tipo.SelectedIndex)].Operador;
                string ver = numero.Text.Trim();
                numero.IsEnabled = tipo.SelectedIndex > 0;
                string valor = tipo.SelectedIndex <= 0 || ver.Length == 0 ? "" : op + ver;
                if (valor == DocumentoCatalogo.Texto(version[clave])) return;

                if (valor.Length == 0) version.Remove(clave);
                else version[clave] = valor;

                resumen.Text = Resumen(valor);
                alCambiar();
            }

            tipo.SelectionChanged += (_, _) => Aplicar();
            numero.SelectionChanged += (_, _) => numero.Dispatcher.BeginInvoke(Aplicar);
            numero.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler((_, _) => Aplicar()));
            return bloque;
        }

        private static (string Operador, string Version) Separar(string valor)
        {
            valor = valor.Trim();
            foreach (var op in new[] { "<=", ">=", "<", ">" })
                if (valor.StartsWith(op, StringComparison.Ordinal))
                    return (op, valor[op.Length..].Trim());
            return (">=", valor);
        }

        private static string Resumen(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return "Compatible con cualquier versión.";
            var (op, ver) = Separar(valor);
            return op switch
            {
                "<=" => $"Compatible hasta la {ver} (incluida). Se guarda como \"{valor}\".",
                "<"  => $"Compatible solo con versiones anteriores a la {ver}. Se guarda como \"{valor}\".",
                ">"  => $"Compatible solo con versiones posteriores a la {ver}. Se guarda como \"{valor}\".",
                _    => $"Requiere la {ver} o superior. Se guarda como \"{valor}\".",
            };
        }
    }
}
