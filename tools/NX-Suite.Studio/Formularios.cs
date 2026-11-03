using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NX_Suite.Studio
{
    /// <summary>Campo editable: clave JSON, etiqueta visible y tipo de control.</summary>
    internal sealed record Campo(string Clave, string Etiqueta, TipoCampo Tipo = TipoCampo.Texto, string? Ayuda = null);

    internal enum TipoCampo { Texto, Multilinea, Imagen, Booleano, Numero, ListaComas, Json }

    /// <summary>Construye formularios a partir de una lista de <see cref="Campo"/> sobre un JsonObject.</summary>
    internal static class Formularios
    {
        public static StackPanel Crear(JsonObject objeto, IEnumerable<Campo> campos, Action alCambiar)
        {
            var panel = new StackPanel();
            foreach (var c in campos) panel.Children.Add(CrearCampo(objeto, c, alCambiar));
            return panel;
        }

        /// <summary>Formulario en rejilla de <paramref name="columnas"/> columnas; multilínea, JSON e imágenes ocupan dos.</summary>
        public static Grid CrearCompacto(JsonObject objeto, IEnumerable<Campo> campos, Action alCambiar, int columnas = 4) =>
            Ui.Rejilla(columnas, campos.Select(c =>
            {
                var e = CrearCampo(objeto, c, alCambiar);
                return c.Tipo is TipoCampo.Multilinea or TipoCampo.Json or TipoCampo.Imagen ? Ui.Ancho(e, 2) : e;
            }).ToArray());

        private static UIElement CrearCampo(JsonObject o, Campo c, Action alCambiar)
        {
            var bloque = new StackPanel();

            if (c.Tipo == TipoCampo.Booleano)
            {
                var chk = new CheckBox { Content = c.Etiqueta, IsChecked = o[c.Clave] is JsonValue v && v.TryGetValue<bool>(out var b) && b };
                chk.Click += (_, _) => { o[c.Clave] = chk.IsChecked == true; alCambiar(); };
                bloque.Children.Add(chk);
                AgregarAyuda(bloque, c);
                return bloque;
            }

            bloque.Children.Add(new TextBlock { Text = c.Etiqueta, Style = (Style)Application.Current.Resources["Etiqueta"] });

            string valor = c.Tipo switch
            {
                TipoCampo.ListaComas => string.Join(", ", (o[c.Clave] as JsonArray)?.Select(x => DocumentoCatalogo.Texto(x)) ?? Enumerable.Empty<string>()),
                TipoCampo.Json       => o[c.Clave]?.ToJsonString(DocumentoCatalogo.OpcionesEscritura) ?? "",
                _                    => o[c.Clave] is null ? "" : DocumentoCatalogo.Texto(o[c.Clave]),
            };

            var txt = new TextBox { Text = valor };
            if (c.Tipo is TipoCampo.Multilinea or TipoCampo.Json)
            {
                txt.AcceptsReturn = true;
                txt.TextWrapping = TextWrapping.Wrap;
                txt.MinHeight = c.Tipo == TipoCampo.Json ? 120 : 60;
                txt.MaxHeight = 360;
                txt.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                if (c.Tipo == TipoCampo.Json) txt.FontFamily = new FontFamily("Consolas");
            }
            bloque.Children.Add(txt);

            Image? vista = null;
            if (c.Tipo == TipoCampo.Imagen)
            {
                vista = new Image { MaxHeight = 90, MaxWidth = 320, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) };
                bloque.Children.Add(vista);
                MostrarImagen(vista, valor);
            }

            txt.TextChanged += (_, _) =>
            {
                switch (c.Tipo)
                {
                    case TipoCampo.ListaComas:
                        o[c.Clave] = new JsonArray(txt.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                                          .Select(s => (JsonNode?)JsonValue.Create(s)).ToArray());
                        break;
                    case TipoCampo.Numero:
                        if (int.TryParse(txt.Text, out int n)) o[c.Clave] = n;
                        break;
                    case TipoCampo.Json:
                        try
                        {
                            o[c.Clave] = System.Text.Json.Nodes.JsonNode.Parse(txt.Text);
                            txt.BorderBrush = Pincel("#33333F");
                        }
                        catch (System.Text.Json.JsonException)
                        {
                            txt.BorderBrush = Brushes.OrangeRed; // no se aplica hasta que sea válido
                            return;
                        }
                        break;
                    default:
                        o[c.Clave] = txt.Text;
                        break;
                }
                if (vista != null) MostrarImagen(vista, txt.Text);
                alCambiar();
            };

            AgregarAyuda(bloque, c);
            return bloque;
        }

        private static void AgregarAyuda(Panel p, Campo c)
        {
            if (c.Ayuda != null)
                p.Children.Add(new TextBlock { Text = c.Ayuda, Style = (Style)Application.Current.Resources["Ayuda"] });
        }

        public static void MostrarImagen(Image img, string url)
        {
            img.Source = null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                img.Source = bmp;
            }
            catch { /* URL no válida como imagen: se deja vacío */ }
        }

        public static TextBlock Titulo(string texto) =>
            new() { Text = texto, Style = (Style)Application.Current.Resources["Titulo"] };

        public static TextBlock Ayuda(string texto) =>
            new() { Text = texto, Style = (Style)Application.Current.Resources["Ayuda"] };

        public static Brush Pincel(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}
