using NX_Suite.UI.Controles;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — Paneles laterales retráctiles (Centro de Mando a la
    /// izquierda y Arsenal a la derecha): rieles, animaciones y estado abierto.
    /// </summary>
    public partial class MainWindow
    {
        private void CambiarColorRiel(Border riel, bool aplicar, string colorHex)
        {
            if (aplicar)
                riel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        }

        private void BtnCerrarPaneles_Click(object sender, RoutedEventArgs e)
        {
            UiAnimaciones.CerrarPanelDerecho(ArsenalRetractil.RielGris, ArsenalRetractil.ContenedorArsenal, FondoOscuro);

            _panelDerechoAbierto = false;

            if (ArsenalRetractil.Pestanita != null) ArsenalRetractil.Pestanita.Visibility = Visibility.Visible;

            ArsenalRetractil.ContenedorArsenal.IsHitTestVisible = false;
        }

        private void RielGris_Click(object sender, MouseButtonEventArgs e)
        {
            if (!_panelDerechoAbierto)
            {
                BtnCerrarPaneles_Click(null, null);
                UiAnimaciones.AbrirPanelDerecho(ArsenalRetractil.RielGris, ArsenalRetractil.ContenedorArsenal, FondoOscuro);
                _panelDerechoAbierto = true;
                if (ArsenalRetractil.Pestanita != null) ArsenalRetractil.Pestanita.Visibility = Visibility.Collapsed;
                ArsenalRetractil.ContenedorArsenal.IsHitTestVisible = true;
            }
            else
            {
                BtnCerrarPaneles_Click(null, null);
            }
        }
    }
}
