using System;
using NX_Swite.Core;
using NX_Swite.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace NX_Swite.UI.Controles
{
    /// <summary>
    /// Hub visual de administración del Custom Firmware (CFW).
    ///
    /// Expone 6 tarjetas — Alertas y Estado, Instalación, Actualización,
    /// Catálogo, Personalización y Herramientas (placeholder) — que emiten
    /// eventos de navegación. La conexión real a las vistas existentes
    /// (VistaAsistida, CatalogoModulos, VentanaPersonalizacion, diagnóstico)
    /// se hace en <see cref="MainWindow"/>.
    ///
    /// Todas las tarjetas comparten un efecto hover "premium": halo neon
    /// rotatorio (<c>EstiloHaloHub</c>) + escala sutil + sonido
    /// <see cref="EventoSonido.Hover"/> (ver región "Hover premium" abajo).
    /// Instalación, Actualización, Catálogo, Personalización y Herramientas
    /// admiten una imagen de fondo opcional descargada y cacheada desde el
    /// Gist (sección raíz "tarjetasHubCfw", ver <see cref="TarjetaHubCfwConfig"/>
    /// y <see cref="AplicarImagenesRemotas"/>).
    ///
    /// Ver progreso y decisiones en CODEBASE_INDEX.md ? sección
    /// "Migración de mundos a CFW" / "Hub CFW — tarjetas e imágenes remotas".
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

        /// <summary>Se dispara al pulsar la tarjeta de Herramientas (placeholder).</summary>
        public event EventHandler? HerramientasSolicitado;

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

        /// <summary>
        /// Aplica las imágenes de fondo remotas declaradas en la sección
        /// "tarjetasHubCfw" del Gist. Se comportan igual que el resto de
        /// iconos remotos: se cargan desde caché local si ya existen
        /// (<see cref="GestorIconos.ObtenerRutaLocal"/>) o se descargan y
        /// cachean en background (<see cref="GestorIconos.DescargarSiNoExisteAsync"/>).
        /// IDs reconocidos: "instalacion", "actualizacion", "catalogo",
        /// "personalizacion", "herramientas". "alertas" no admite imagen.
        /// </summary>
        public async void AplicarImagenesRemotas(IDictionary<string, string>? tarjetas)
        {
            if (tarjetas is null || tarjetas.Count == 0) return;

            var porId = new Dictionary<string, string>(tarjetas, StringComparer.OrdinalIgnoreCase);

            var mapa = new (string Id, Image Destino)[]
            {
                ("instalacion",     ImgInstalacion),
                ("actualizacion",   ImgActualizar),
                ("catalogo",        ImgCatalogo),
                ("personalizacion", ImgPersonalizacion),
                ("herramientas",    ImgHerramientas),
            };

            foreach (var (id, destino) in mapa)
            {
                if (!porId.TryGetValue(id, out var url) || string.IsNullOrWhiteSpace(url)) continue;

                await Servicios.Iconos.DescargarSiNoExisteAsync(url);
                string? rutaLocal = Servicios.Iconos.ObtenerRutaLocal(url);

                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource   = new Uri(rutaLocal ?? url);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();

                    destino.Source     = bmp;
                    destino.Visibility = Visibility.Visible;
                }
                catch { /* Silencioso: la tarjeta se queda con su fondo solido */ }
            }
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

        private void TarjetaHerramientas_Click(object sender, MouseButtonEventArgs e)
            => HerramientasSolicitado?.Invoke(this, EventArgs.Empty);

        // ????????????????????????????????????????????????????????
        //  Hover premium — halo neon + escala + sonido
        // ????????????????????????????????????????????????????????

        private void Tarjeta_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not Border tarjeta) return;

            Servicios.Sonidos.Reproducir(EventoSonido.Hover);

            var halo = BuscarHalo(tarjeta);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur  = TimeSpan.FromSeconds(0.18);

            if (halo != null)
            {
                halo.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.9, dur) { EasingFunction = ease });
                var scaleHalo = (ScaleTransform)halo.RenderTransform;
                scaleHalo.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.015, dur) { EasingFunction = ease });
                scaleHalo.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.015, dur) { EasingFunction = ease });
            }

            var scaleTarjeta = (ScaleTransform)tarjeta.RenderTransform;
            scaleTarjeta.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.02, dur) { EasingFunction = ease });
            scaleTarjeta.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.02, dur) { EasingFunction = ease });
        }

        private void Tarjeta_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not Border tarjeta) return;

            var halo = BuscarHalo(tarjeta);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur  = TimeSpan.FromSeconds(0.18);

            if (halo != null)
            {
                halo.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, dur) { EasingFunction = ease });
                var scaleHalo = (ScaleTransform)halo.RenderTransform;
                scaleHalo.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
                scaleHalo.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
            }

            var scaleTarjeta = (ScaleTransform)tarjeta.RenderTransform;
            scaleTarjeta.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
            scaleTarjeta.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
        }

        private Border? BuscarHalo(Border tarjeta)
        {
            if (tarjeta.Tag is not string nombreHalo) return null;
            return FindName(nombreHalo) as Border;
        }
    }
}
