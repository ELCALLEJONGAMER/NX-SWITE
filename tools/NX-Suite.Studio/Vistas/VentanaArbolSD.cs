using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NX_Suite.Catalogo;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Árbol de la microSD tras instalar la versión nueva, con los cambios
    /// respecto a la versión publicada coloreados (+ nuevo, − eliminado).
    /// </summary>
    internal sealed class VentanaArbolSD : Window
    {
        private const string Verde = "#3DDC84", Rojo = "#FF6B6B", Normal = "#D8D8E0";

        public VentanaArbolSD(string nombre, PropuestaActualizacion p)
        {
            Title = $"{nombre} {p.VersionNueva} · microSD";
            Width = 720; Height = 760;
            Background = Formularios.Pincel("#121217");
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = Application.Current.MainWindow;

            var dock = new DockPanel { Margin = new Thickness(16) };

            var cab = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            cab.Children.Add(Formularios.Titulo($"{nombre}   {p.VersionActual} → {p.VersionNueva}"));
            int nuevos = p.SdNueva.Count(r => !p.SdVieja.Contains(r));
            int quitados = p.SdVieja.Count(r => !p.SdNueva.Contains(r));
            var leyenda = new TextBlock { Foreground = Formularios.Pincel("#9A9AA8") };
            leyenda.Inlines.Add($"{p.SdNueva.Count} archivos en la SD   ");
            leyenda.Inlines.Add(new System.Windows.Documents.Run($"+{nuevos} nuevos   ") { Foreground = Formularios.Pincel(Verde) });
            leyenda.Inlines.Add(new System.Windows.Documents.Run($"−{quitados} eliminados") { Foreground = Formularios.Pincel(Rojo) });
            cab.Children.Add(leyenda);
            if (p.EnlaceRoto)
                cab.Children.Add(Aviso("El enlace anterior ya no existe: no hay con qué comparar, revisa el árbol completo."));
            if (p.ErrorEstructura != null) cab.Children.Add(Aviso(p.ErrorEstructura));
            foreach (var a in p.Avisos) cab.Children.Add(Aviso(a));

            var soloCambios = new CheckBox { Content = "Mostrar solo cambios", Margin = new Thickness(0, 8, 0, 0) };
            cab.Children.Add(soloCambios);
            DockPanel.SetDock(cab, Dock.Top);
            dock.Children.Add(cab);

            var arbol = new TreeView
            {
                Background = Formularios.Pincel("#1A1A21"), BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"), FontSize = 13, Padding = new Thickness(6),
            };
            dock.Children.Add(arbol);
            Content = dock;

            void Construir()
            {
                arbol.Items.Clear();
                var todas = p.SdNueva.Select(r => (Ruta: r, Estado: p.SdVieja.Contains(r) || p.EnlaceRoto ? 0 : 1))
                    .Concat(p.EnlaceRoto ? Enumerable.Empty<(string Ruta, int Estado)>() : p.SdVieja.Where(r => !p.SdNueva.Contains(r)).Select(r => (Ruta: r, Estado: -1)))
                    .Where(x => soloCambios.IsChecked != true || x.Estado != 0)
                    .OrderBy(x => x.Ruta, StringComparer.OrdinalIgnoreCase);
                var raiz = new TreeViewItem { Header = Cabecera("/", Normal, true), IsExpanded = true };
                foreach (var (ruta, estado) in todas) Insertar(raiz, ruta, estado);
                arbol.Items.Add(raiz);
            }
            soloCambios.Checked += (_, _) => Construir();
            soloCambios.Unchecked += (_, _) => Construir();
            Construir();
        }

        private static void Insertar(TreeViewItem raiz, string ruta, int estado)
        {
            var partes = ruta.Trim('/').Split('/');
            var actual = raiz;
            for (int i = 0; i < partes.Length; i++)
            {
                bool hoja = i == partes.Length - 1;
                string color = estado switch { 1 => Verde, -1 => Rojo, _ => Normal };
                string clave = (hoja ? "f:" : "d:") + partes[i];
                var hijo = actual.Items.OfType<TreeViewItem>().FirstOrDefault(t => (string)t.Tag == clave);
                if (hijo == null)
                {
                    string prefijo = hoja ? estado switch { 1 => "+ ", -1 => "− ", _ => "  " } : "";
                    hijo = new TreeViewItem
                    {
                        Tag = clave, IsExpanded = i < 1 || estado != 0,
                        Header = Cabecera(prefijo + (hoja ? partes[i] : partes[i] + "/"), hoja ? color : Normal, !hoja),
                    };
                    actual.Items.Add(hijo);
                }
                else if (estado != 0) hijo.IsExpanded = true;
                actual = hijo;
            }
        }

        private static TextBlock Cabecera(string texto, string color, bool carpeta) => new()
        {
            Text = (carpeta ? "📁 " : "") + texto, Foreground = Formularios.Pincel(color),
            FontWeight = carpeta ? FontWeights.SemiBold : FontWeights.Normal,
        };

        private static TextBlock Aviso(string texto) => new()
        {
            Text = "⚠ " + texto, Foreground = Formularios.Pincel("#FFA640"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        };
    }
}
