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

        /// <summary>Se dispara al pulsar «Recuperar Micro SD».</summary>
        public event EventHandler? RecuperarSolicitado;

        public PanelDerecho()
        {
            InitializeComponent();
        }

        private void BtnRecuperarSD_Click(object sender, RoutedEventArgs e)
            => RecuperarSolicitado?.Invoke(this, EventArgs.Empty);

        private void BtnExpulsarSD_Click(object sender, RoutedEventArgs e)
        {
            ExpulsarSolicitado?.Invoke(this, EventArgs.Empty);
        }
    }
}
