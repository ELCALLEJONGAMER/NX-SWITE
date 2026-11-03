using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace NX_Suite.Studio
{
    /// <summary>
    /// Piezas visuales reutilizables de NX-STUDIO (tarjetas, chips, pastillas, botones)
    /// para que todas las vistas compartan el mismo aspecto.
    /// </summary>
    internal static class Ui
    {
        public const string Acento = "#00D2FF";
        public const string Violeta = "#B266FF";
        public const string Verde = "#3DDC84";
        public const string Ambar = "#FFA640";
        public const string Rojo = "#FF5C5C";
        public const string Tenue = "#9A9AA8";

        public static Border Tarjeta(UIElement hijo, string? acento = null) => new()
        {
            Background = Formularios.Pincel("#1A1A22"),
            BorderBrush = Formularios.Pincel(acento ?? "#2A2A36"),
            BorderThickness = acento == null ? new Thickness(1) : new Thickness(3, 1, 1, 1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 12),
            Child = hijo,
        };

        public static TextBlock Titulo(string texto, double tamano = 20) => new()
        {
            Text = texto, FontSize = tamano, FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap,
        };

        public static TextBlock Seccion(string texto) => new()
        {
            Text = texto, Foreground = Formularios.Pincel(Violeta), FontWeight = FontWeights.Bold, FontSize = 11,
            Margin = new Thickness(0, 18, 0, 6),
        };

        public static TextBlock Texto(string texto, string color = "#B8B8C4", double tamano = 12) => new()
        {
            Text = texto, Foreground = Formularios.Pincel(color), FontSize = tamano, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2),
        };

        public static TextBlock Etiqueta(string texto) =>
            new() { Text = texto, Style = (Style)Application.Current.Resources["Etiqueta"] };

        public static Button Boton(string texto, Action accion, bool primario = false, string? ayuda = null)
        {
            var b = new Button { Content = texto, ToolTip = ayuda };
            if (primario) b.Style = (Style)Application.Current.Resources["Primario"];
            b.Click += (_, _) => accion();
            return b;
        }

        /// <summary>Chip conmutable (etiquetas, dependencias, selección de firma).</summary>
        public static ToggleButton Chip(string texto, bool activo, Action<bool> alCambiar, string? ayuda = null)
        {
            var t = new ToggleButton
            {
                Content = texto, IsChecked = activo, ToolTip = ayuda,
                Style = (Style)Application.Current.Resources["Chip"],
            };
            t.Click += (_, _) => alCambiar(t.IsChecked == true);
            return t;
        }

        /// <summary>Pastilla de estado de solo lectura.</summary>
        public static Border Pastilla(string texto, string color) => new()
        {
            Background = new SolidColorBrush(Color.FromArgb(0x26, 0, 0, 0)) { Opacity = 1 },
            BorderBrush = Formularios.Pincel(color), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 1, 8, 2), Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = texto, Foreground = Formularios.Pincel(color), FontSize = 11 },
        };

        /// <summary>Rejilla de N columnas iguales; los elementos se colocan en orden y saltan de fila solos.</summary>
        public static Grid Rejilla(int columnas, params UIElement[] hijos)
        {
            var g = new Grid();
            for (int c = 0; c < columnas; c++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int i = 0;
            foreach (var h in hijos)
            {
                int span = Math.Min(columnas, Grid.GetColumnSpan(h));
                if (i % columnas + span > columnas) i += columnas - i % columnas;
                int fila = i / columnas;
                while (g.RowDefinitions.Count <= fila) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(h, fila);
                Grid.SetColumn(h, i % columnas);
                if (h is FrameworkElement fe && fe.Margin == default) fe.Margin = new Thickness(0, 0, 10, 8);
                g.Children.Add(h);
                i += span;
            }
            return g;
        }

        /// <summary>Marca un elemento para ocupar varias columnas en <see cref="Rejilla"/>.</summary>
        public static T Ancho<T>(T e, int columnas) where T : UIElement
        {
            Grid.SetColumnSpan(e, columnas);
            return e;
        }

        /// <summary>Campo con etiqueta encima y ayuda opcional debajo.</summary>
        public static StackPanel Campo(string etiqueta, UIElement control, string? ayuda = null)
        {
            var p = new StackPanel();
            p.Children.Add(Etiqueta(etiqueta));
            p.Children.Add(control);
            if (ayuda != null) p.Children.Add(Formularios.Ayuda(ayuda));
            return p;
        }
    }
}
