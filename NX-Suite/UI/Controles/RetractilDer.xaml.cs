using System;
using System.Windows;
using System.Windows.Controls;

namespace NX_Suite.UI.Controles
{
    public partial class RetractilDer : UserControl
    {
        /// <summary>
        /// Se dispara al pulsar "FORMAT FAT32". MainWindow se suscribe y abre
        /// el overlay correspondiente. Mantenemos el control desacoplado:
        /// no conoce a MainWindow ni a la lógica de particionado.
        /// </summary>
        public event EventHandler? FormatFAT32Solicitado;

        public RetractilDer()
        {
            InitializeComponent();
        }

        private void BtnFormatFAT32_Click(object sender, RoutedEventArgs e)
            => FormatFAT32Solicitado?.Invoke(this, EventArgs.Empty);
    }
}
