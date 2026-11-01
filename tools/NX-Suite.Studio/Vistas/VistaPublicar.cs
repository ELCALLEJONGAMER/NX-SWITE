using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NX_Suite.Catalogo;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Comprobación de actualizaciones (sin aplicar), validación y publicación
    /// del catálogo. Nada llega a los usuarios hasta pulsar «Publicar».
    /// </summary>
    internal sealed class VistaPublicar : StackPanel
    {
        private readonly DocumentoCatalogo _doc;
        private readonly Action _alCambiar;
        private readonly TextBox _salida = new()
        {
            IsReadOnly = true, FontFamily = new FontFamily("Consolas"), Height = 340,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
        };

        public VistaPublicar(DocumentoCatalogo doc, Action alCambiar)
        {
            _doc = doc;
            _alCambiar = alCambiar;

            Children.Add(Formularios.Titulo("Validar y publicar"));
            Children.Add(Formularios.Ayuda(
                "1) Busca versiones nuevas en GitHub (solo informa, no cambia nada). " +
                "2) Actualiza manualmente en «Módulos» lo que hayas probado en tu consola. " +
                "3) Valida y publica: se hace commit de catalogo.json y push a main, y los usuarios lo reciben al abrir la app."));

            var barra = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            barra.Children.Add(Boton("🔎 Buscar actualizaciones", async b => await BuscarAsync(b)));
            barra.Children.Add(Boton("✔ Validar", _ => Validar()));
            barra.Children.Add(Boton("🚀 Publicar", async b => await PublicarAsync(b)));
            Children.Add(barra);
            Children.Add(_salida);
        }

        private async Task BuscarAsync(Button b)
        {
            b.IsEnabled = false;
            _salida.Text = "Consultando GitHub…\n";
            try
            {
                using var gh = new GitHubReleases(ConfiguracionStudio.TokenGitHub);
                // Trabaja sobre una copia: aquí solo se informa, no se modifica el catálogo.
                var copia = _doc.Lista("Modulos").DeepClone().AsArray();
                var act = new ActualizadorCatalogo(gh, validarPaquetes: false);
                int nuevas = 0;
                foreach (var m in copia.OfType<System.Text.Json.Nodes.JsonObject>())
                {
                    var inf = await act.ProcesarAsync(m, aplicar: false);
                    if (inf.Resultado is ResultadoModulo.Actualizado or ResultadoModulo.Manual)
                    {
                        nuevas++;
                        _salida.AppendText($"• {inf.Id}: {inf.Detalle}\n");
                    }
                }
                _salida.AppendText(nuevas == 0 ? "Todo al día.\n" : $"\n{nuevas} módulo(s) con novedades. Revísalos y pruébalos antes de actualizar.\n");
            }
            catch (Exception ex)
            {
                _salida.AppendText($"Error: {ex.Message}\n");
            }
            finally { b.IsEnabled = true; }
        }

        private bool Validar()
        {
            var errores = _doc.Validar();
            _salida.Text = errores.Count == 0
                ? "✓ El catálogo es válido.\n"
                : "✗ Errores:\n• " + string.Join("\n• ", errores) + "\n";
            return errores.Count == 0;
        }

        private async Task PublicarAsync(Button b)
        {
            if (!Validar()) return;
            if (MessageBox.Show("Se publicará el catálogo para TODOS los usuarios. ¿Continuar?",
                    "NX-STUDIO", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            b.IsEnabled = false;
            _salida.AppendText("Publicando…\n");
            try
            {
                var (ok, log) = await _doc.PublicarAsync($"catalogo: actualizado desde NX-STUDIO ({DateTime.Now:yyyy-MM-dd HH:mm})");
                _salida.AppendText(log + (ok ? "\n✓ Publicado.\n" : "\n✗ La publicación falló; revisa el log.\n"));
                _alCambiar();
            }
            finally { b.IsEnabled = true; }
        }

        private static Button Boton(string texto, Func<Button, Task> accion)
        {
            var b = new Button { Content = texto };
            b.Click += async (_, _) => await accion(b);
            return b;
        }

        private static Button Boton(string texto, Action<Button> accion)
        {
            var b = new Button { Content = texto };
            b.Click += (_, _) => accion(b);
            return b;
        }
    }
}
