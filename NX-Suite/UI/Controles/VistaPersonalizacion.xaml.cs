using NX_Suite.Models;
using NX_Suite.Models;
using NX_Suite.UI;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NX_Suite.UI.Controles
{
    public partial class VistaPersonalizacion : UserControl
    {
        private readonly List<TemaConfig> _temas = new();
        private VentanaPersonalizacion? _ventana;

        public event System.Action<TemaConfig>? TemaAplicado;

        public VistaPersonalizacion()
        {
            InitializeComponent();
        }

        public void CargarTemas(IEnumerable<TemaConfig> temas)
        {
            _temas.Clear();
            _temas.AddRange(temas);
            RefrescarLista();
        }

        private void RefrescarLista()
        {
            ListaTemas.ItemsSource = null;
            ListaTemas.ItemsSource = _temas;
        }

        private void TarjetaTema_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.DataContext is TemaConfig tema)
            {
                foreach (var t in _temas) t.Aplicado = false;
                tema.Aplicado = true;
                RefrescarLista();
                TemaAplicado?.Invoke(tema);
            }
        }

        private void BtnAbrirPersonalizacion_Click(object sender, RoutedEventArgs e)
        {
            if (_ventana is { IsVisible: true })
            {
                _ventana.Activate();
                return;
            }
            _ventana = new VentanaPersonalizacion();
            _ventana.Show();
        }
    }
}
