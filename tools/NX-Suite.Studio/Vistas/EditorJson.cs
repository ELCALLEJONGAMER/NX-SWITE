using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Edición directa en JSON de secciones poco frecuentes (ConfiguracionUI,
    /// Sonidos, tools, tarjetasHubCfw, titleDB). Valida la sintaxis antes de aplicar.
    /// </summary>
    internal sealed class EditorJson : DockPanel
    {
        public EditorJson(DocumentoCatalogo doc, string seccion, string ayuda, Action alCambiar)
        {
            var cabecera = new StackPanel();
            cabecera.Children.Add(Formularios.Titulo(seccion));
            cabecera.Children.Add(Formularios.Ayuda(ayuda));
            SetDock(cabecera, Dock.Top);
            Children.Add(cabecera);

            var estado = new TextBlock { Margin = new Thickness(0, 6, 0, 0) };
            var aplicar = new Button { Content = "Aplicar cambios", HorizontalAlignment = HorizontalAlignment.Left };
            var pie = new StackPanel { Orientation = Orientation.Horizontal };
            pie.Children.Add(aplicar);
            pie.Children.Add(estado);
            SetDock(pie, Dock.Bottom);
            Children.Add(pie);

            var txt = new TextBox
            {
                Text = doc.Raiz[seccion]?.ToJsonString(DocumentoCatalogo.OpcionesEscritura) ?? "{}",
                AcceptsReturn = true,
                AcceptsTab = true,
                FontFamily = new FontFamily("Consolas"),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 10, 0, 6),
            };
            Children.Add(txt);

            aplicar.Click += (_, _) =>
            {
                try
                {
                    doc.Raiz[seccion] = JsonNode.Parse(txt.Text);
                    alCambiar();
                    estado.Text = "  ✓ Aplicado";
                    estado.Foreground = Brushes.LightGreen;
                }
                catch (JsonException ex)
                {
                    estado.Text = $"  ✗ JSON no válido: línea {ex.LineNumber + 1}, {ex.Message}";
                    estado.Foreground = Brushes.OrangeRed;
                }
            };
        }
    }
}
