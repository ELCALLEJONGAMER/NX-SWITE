using System;
using System.Windows;
using System.Windows.Controls;

namespace NX_Suite.UI.Controles
{
    public partial class RetractilIzq : UserControl
    {
        public event EventHandler? CerrarSolicitado;

        public RetractilIzq()
        {
            InitializeComponent();
        }

        private void BtnCerrarMando_Click(object sender, RoutedEventArgs e)
        {
            CerrarSolicitado?.Invoke(this, EventArgs.Empty);
        }
    }
}