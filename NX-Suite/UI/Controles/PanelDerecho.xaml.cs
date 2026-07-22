using System;
using System.Windows;
using System.Windows.Controls;

namespace NX_Swite.UI.Controles
{
    public partial class PanelDerecho : UserControl
    {
        /// <summary>
        /// Se dispara cuando el usuario pulsa el botón "Expulsar SD".
        /// MainWindow se suscribe para ejecutar la lógica de expulsión.
        /// </summary>
        public event EventHandler? ExpulsarSolicitado;

        public PanelDerecho()
        {
            InitializeComponent();
        }

        private void BtnExpulsarSD_Click(object sender, RoutedEventArgs e)
        {
            ExpulsarSolicitado?.Invoke(this, EventArgs.Empty);
        }
    }
}