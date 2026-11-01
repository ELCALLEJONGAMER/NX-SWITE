using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace NX_Suite.Studio.Vistas
{
    /// <summary>
    /// Editor genérico de una lista de objetos (noticias, recomendados…):
    /// lista a la izquierda con subir/bajar/añadir/quitar y formulario a la derecha.
    /// </summary>
    internal sealed class EditorLista : Grid
    {
        private readonly JsonArray _lista;
        private readonly Campo[] _campos;
        private readonly Func<JsonObject, string> _rotulo;
        private readonly Func<JsonObject> _nuevo;
        private readonly Action _alCambiar;
        private readonly ListBox _listBox = new() { Margin = new Thickness(0, 0, 0, 6) };
        private readonly ContentControl _detalle = new();

        public EditorLista(string titulo, string ayuda, JsonArray lista, Campo[] campos,
                           Func<JsonObject, string> rotulo, Func<JsonObject> nuevo, Action alCambiar)
        {
            _lista = lista; _campos = campos; _rotulo = rotulo; _nuevo = nuevo; _alCambiar = alCambiar;

            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var izquierda = new DockPanel { Margin = new Thickness(0, 0, 16, 0) };
            var cabecera = new StackPanel();
            cabecera.Children.Add(Formularios.Titulo(titulo));
            cabecera.Children.Add(Formularios.Ayuda(ayuda));
            DockPanel.SetDock(cabecera, Dock.Top);
            izquierda.Children.Add(cabecera);

            var botones = new UniformGrid4(
                Boton("↑ Subir", () => Mover(-1)), Boton("↓ Bajar", () => Mover(1)),
                Boton("+ Añadir", Anadir), Boton("× Quitar", Quitar));
            DockPanel.SetDock(botones, Dock.Bottom);
            izquierda.Children.Add(botones);
            _listBox.Margin = new Thickness(0, 10, 0, 6);
            izquierda.Children.Add(_listBox);
            Children.Add(izquierda);

            var scroll = new ScrollViewer { Content = _detalle, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            SetColumn(scroll, 1);
            Children.Add(scroll);

            _listBox.SelectionChanged += (_, _) => MostrarDetalle();
            Refrescar(0);
        }

        private void Refrescar(int seleccion)
        {
            _listBox.ItemsSource = _lista.OfType<JsonObject>().Select(_rotulo).ToList();
            _listBox.SelectedIndex = Math.Min(seleccion, _lista.Count - 1);
            MostrarDetalle();
        }

        private void MostrarDetalle()
        {
            int i = _listBox.SelectedIndex;
            if (i < 0 || _lista[i] is not JsonObject o) { _detalle.Content = null; return; }
            _detalle.Content = Formularios.Crear(o, _campos, () =>
            {
                _alCambiar();
                if (_listBox.Items[i] is string anterior && anterior != _rotulo(o))
                {
                    var items = (List<string>)_listBox.ItemsSource;
                    items[i] = _rotulo(o);
                    _listBox.Items.Refresh();
                }
            });
        }

        private void Mover(int delta)
        {
            int i = _listBox.SelectedIndex, j = i + delta;
            if (i < 0 || j < 0 || j >= _lista.Count) return;
            var nodo = _lista[i]!;
            _lista.RemoveAt(i);
            _lista.Insert(j, nodo);
            _alCambiar();
            Refrescar(j);
        }

        private void Anadir()
        {
            _lista.Add(_nuevo());
            _alCambiar();
            Refrescar(_lista.Count - 1);
        }

        private void Quitar()
        {
            int i = _listBox.SelectedIndex;
            if (i < 0) return;
            if (MessageBox.Show($"¿Quitar «{_listBox.SelectedItem}»?", "NX-STUDIO",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _lista.RemoveAt(i);
            _alCambiar();
            Refrescar(Math.Max(0, i - 1));
        }

        private static Button Boton(string texto, Action accion)
        {
            var b = new Button { Content = texto };
            b.Click += (_, _) => accion();
            return b;
        }
    }

    /// <summary>Rejilla 2×2 para los botones de lista.</summary>
    internal sealed class UniformGrid4 : System.Windows.Controls.Primitives.UniformGrid
    {
        public UniformGrid4(params UIElement[] hijos)
        {
            Columns = 2;
            foreach (var h in hijos) Children.Add(h);
        }
    }
}
