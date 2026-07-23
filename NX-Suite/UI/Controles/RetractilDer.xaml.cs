using System;
using System.Windows;
using System.Windows.Controls;

namespace NX_Swite.UI.Controles
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
        /// <see cref="NX_Swite.UI.VentanaAsistidoCompleto"/> y ejecuta solo
        /// el particionado (sin instalación de módulos), útil para pruebas.
        /// </summary>
        public event EventHandler? ParticionadoSolicitado;

        /// <summary>
        /// Se dispara al completar el hold de "LIMPIAR SD".
        /// MainWindow se suscribe para abrir el overlay de limpieza.
        /// </summary>
        public event EventHandler? LimpiezaMicroSDSolicitada;

        /// <summary>
        /// Se dispara al pulsar "RESPALDAR LLAVES".
        /// MainWindow abre el overlay de respaldo de llaves.
        /// </summary>
        public event EventHandler? RespaldoLlavesSolicitado;

        public RetractilDer()
        {
            InitializeComponent();
        }

        private void BtnFormatFAT32_Click(object sender, RoutedEventArgs e)
            => FormatFAT32Solicitado?.Invoke(this, EventArgs.Empty);

        private void BtnParticionarFormatear_Click(object sender, RoutedEventArgs e)
            => ParticionadoSolicitado?.Invoke(this, EventArgs.Empty);

        private void BtnLimpiarSD_Click(object sender, RoutedEventArgs e)
            => LimpiezaMicroSDSolicitada?.Invoke(this, EventArgs.Empty);

        private void BtnRespaldarLlaves_Click(object sender, RoutedEventArgs e)
            => RespaldoLlavesSolicitado?.Invoke(this, EventArgs.Empty);
    }
}
