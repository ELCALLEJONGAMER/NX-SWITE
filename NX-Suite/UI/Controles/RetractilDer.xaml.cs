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

        /// <summary>
        /// Se dispara al pulsar "PARTICIONAR Y FORMATEAR". MainWindow abre
        /// <see cref="NX_Suite.UI.VentanaAsistidoCompleto"/> y ejecuta solo
        /// el particionado (sin instalación de módulos), útil para pruebas.
        /// </summary>
        public event EventHandler? ParticionadoSolicitado;

        public RetractilDer()
        {
            InitializeComponent();
        }

        private void BtnFormatFAT32_Click(object sender, RoutedEventArgs e)
            => FormatFAT32Solicitado?.Invoke(this, EventArgs.Empty);

        private void BtnParticionarFormatear_Click(object sender, RoutedEventArgs e)
            => ParticionadoSolicitado?.Invoke(this, EventArgs.Empty);
    }
}
