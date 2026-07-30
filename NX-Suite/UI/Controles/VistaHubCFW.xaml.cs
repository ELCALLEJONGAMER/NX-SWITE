using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NX_Swite.UI.Controles
{
    /// <summary>
    /// Hub visual de administración del Custom Firmware (CFW).
    ///
    /// Fase 1 de la migración (solo visual): expone 4 tarjetas —
    /// Alertas y Recomendaciones, Instalación, Catálogo y Personalización —
    /// que emiten eventos de navegación. La conexión real a las vistas
    /// existentes (VistaAsistida, CatalogoModulos, VentanaPersonalizacion,
    /// diagnóstico) se hace en <see cref="MainWindow"/> durante la Fase 2.
    ///
    /// Ver progreso y decisiones en CODEBASE_INDEX.md ? sección
    /// "Migración de mundos a CFW".
    /// </summary>
    public partial class VistaHubCFW : UserControl
    {
        /// <summary>Se dispara al pulsar la tarjeta de Alertas y Recomendaciones.</summary>
        public event EventHandler? AlertasSolicitado;

        /// <summary>Se dispara al pulsar la tarjeta de Instalación (Asistido).</summary>
        public event EventHandler? InstalacionSolicitada;

        /// <summary>Se dispara al pulsar la tarjeta de Catálogo.</summary>
        public event EventHandler? CatalogoSolicitado;

        /// <summary>Se dispara al pulsar la tarjeta de Personalización.</summary>
        public event EventHandler? PersonalizacionSolicitada;

        /// <summary>Se dispara al pulsar la tarjeta de Actualizar.</summary>
        public event EventHandler? ActualizarSolicitado;

        public VistaHubCFW()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Actualiza el texto de resumen de la tarjeta de Alertas.
        /// Fase visual: sólo texto libre; en fases posteriores se sustituye
        /// por binding a los contadores reales de MainWindow.Diagnostico.cs.
        /// </summary>
        public void ActualizarResumenAlertas(string texto)
        {
            TxtResumenAlertasHub.Text = texto;
        }

        /// <summary>
        /// Actualiza el texto de resumen de la tarjeta de Catálogo
        /// (ej. "3 módulos necesitan actualización").
        /// </summary>
        public void ActualizarResumenCatalogo(string texto)
        {
            TxtResumenCatalogoHub.Text = texto;
        }

        /// <summary>
        /// Actualiza el texto de resumen de la tarjeta de Actualizar
        /// (ej. "2 módulos tienen una version mas reciente").
        /// </summary>
        public void ActualizarResumenActualizar(string texto)
        {
            TxtResumenActualizarHub.Text = texto;
        }

        private void TarjetaAlertas_Click(object sender, MouseButtonEventArgs e)
            => AlertasSolicitado?.Invoke(this, EventArgs.Empty);

        private void TarjetaInstalacion_Click(object sender, MouseButtonEventArgs e)
            => InstalacionSolicitada?.Invoke(this, EventArgs.Empty);

        private void TarjetaCatalogo_Click(object sender, MouseButtonEventArgs e)
            => CatalogoSolicitado?.Invoke(this, EventArgs.Empty);

        private void TarjetaPersonalizacion_Click(object sender, MouseButtonEventArgs e)
            => PersonalizacionSolicitada?.Invoke(this, EventArgs.Empty);

        private void TarjetaActualizar_Click(object sender, MouseButtonEventArgs e)
            => ActualizarSolicitado?.Invoke(this, EventArgs.Empty);
    }
}
