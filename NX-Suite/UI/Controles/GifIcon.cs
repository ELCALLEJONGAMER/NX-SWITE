using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace NX_Suite.UI.Controles
{
    /// <summary>
    /// Control Image que anima GIFs descargados desde una URL.
    /// Soporta animación continua, solo en hover o solo en click.
    /// Con URLs estáticas (PNG/JPG) funciona igual que un Image normal.
    /// </summary>
    public class GifIcon : Image
    {
        // ?? Dependency Properties ??????????????????????????????????????????

        public static readonly DependencyProperty UrlProperty =
            DependencyProperty.Register(nameof(Url), typeof(string), typeof(GifIcon),
                new PropertyMetadata(null, OnUrlChanged));

        /// <summary>URL del GIF o imagen estática a mostrar.</summary>
        public string? Url
        {
            get => (string?)GetValue(UrlProperty);
            set => SetValue(UrlProperty, value);
        }

        public static readonly DependencyProperty AnimateOnHoverProperty =
            DependencyProperty.Register(nameof(AnimateOnHover), typeof(bool), typeof(GifIcon),
                new PropertyMetadata(false));

        /// <summary>
        /// Si es true, la animación solo corre mientras el mouse esté encima
        /// del control o de su Button padre. Al salir vuelve al frame 0.
        /// </summary>
        public bool AnimateOnHover
        {
            get => (bool)GetValue(AnimateOnHoverProperty);
            set => SetValue(AnimateOnHoverProperty, value);
        }

        public static readonly DependencyProperty AnimateOnClickProperty =
            DependencyProperty.Register(nameof(AnimateOnClick), typeof(bool), typeof(GifIcon),
                new PropertyMetadata(false));

        /// <summary>
        /// Si es true, la animación corre una sola vez completa al hacer click
        /// y luego vuelve al frame 0.
        /// </summary>
        public bool AnimateOnClick
        {
            get => (bool)GetValue(AnimateOnClickProperty);
            set => SetValue(AnimateOnClickProperty, value);
        }

        // ?? Estado interno ?????????????????????????????????????????????????

        private static readonly HttpClient _http = new();

        private BitmapDecoder?  _decoder;
        private DispatcherTimer? _timer;
        private int              _frameIndex;
        private bool             _reproduciendo;
        private bool             _cicloUnico;     // true = detener al terminar ciclo

        // ?? Ciclo de vida ?????????????????????????????????????????????????

        public GifIcon()
        {
            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Conectar eventos del Button padre si existe
            var padre = Parent as UIElement ?? TemplatedParent as UIElement;
            if (padre is not null)
            {
                padre.MouseEnter         += Padre_MouseEnter;
                padre.MouseLeave         += Padre_MouseLeave;
                padre.MouseLeftButtonDown += Padre_Click;
            }
            else
            {
                // Fallback: escuchar los propios eventos
                MouseEnter         += Padre_MouseEnter;
                MouseLeave         += Padre_MouseLeave;
                MouseLeftButtonDown += Padre_Click;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetenerAnimacion();
            var padre = Parent as UIElement ?? TemplatedParent as UIElement;
            if (padre is not null)
            {
                padre.MouseEnter         -= Padre_MouseEnter;
                padre.MouseLeave         -= Padre_MouseLeave;
                padre.MouseLeftButtonDown -= Padre_Click;
            }
        }

        // ?? Handlers de interacción ???????????????????????????????????????

        private void Padre_MouseEnter(object sender, MouseEventArgs e)
        {
            if (AnimateOnHover && _decoder?.Frames.Count > 1)
            {
                _cicloUnico = false;
                IniciarAnimacion();
            }
        }

        private void Padre_MouseLeave(object sender, MouseEventArgs e)
        {
            if (AnimateOnHover)
                RestablecerPrimerFrame();
        }

        private void Padre_Click(object sender, MouseButtonEventArgs e)
        {
            if (AnimateOnClick && _decoder?.Frames.Count > 1)
            {
                _cicloUnico = true;
                _frameIndex = 0;
                IniciarAnimacion();
            }
        }

        // ?? Carga desde URL ???????????????????????????????????????????????

        private static void OnUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((GifIcon)d).CargarDesdeUrl(e.NewValue as string);

        private async void CargarDesdeUrl(string? url)
        {
            DetenerAnimacion();
            Source = null;

            if (string.IsNullOrWhiteSpace(url)) return;

            try
            {
                byte[] datos = await _http.GetByteArrayAsync(url);
                using var ms = new MemoryStream(datos);

                _decoder = BitmapDecoder.Create(ms,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

                // Mostrar siempre el primer frame
                Source = _decoder.Frames[0];

                // Animación continua solo si no hay modo hover/click
                if (_decoder.Frames.Count > 1 && !AnimateOnHover && !AnimateOnClick)
                {
                    _cicloUnico = false;
                    IniciarAnimacion();
                }
            }
            catch
            {
                Source = null;
            }
        }

        // ?? Motor de animación ????????????????????????????????????????????

        private void IniciarAnimacion()
        {
            if (_decoder is null || _reproduciendo) return;

            _reproduciendo = true;
            _frameIndex    = 0;
            Source         = _decoder.Frames[0];

            int delay = ObtenerDelay(_decoder.Frames[0].Metadata as BitmapMetadata);
            _timer         = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
            _timer.Tick   += AvanzarFrame;
            _timer.Start();
        }

        private void AvanzarFrame(object? sender, EventArgs e)
        {
            if (_decoder is null) return;

            _frameIndex = (_frameIndex + 1) % _decoder.Frames.Count;
            Source      = _decoder.Frames[_frameIndex];

            int delay = ObtenerDelay(_decoder.Frames[_frameIndex].Metadata as BitmapMetadata);
            if (_timer is not null)
                _timer.Interval = TimeSpan.FromMilliseconds(delay);

            // Si es ciclo único, detener al llegar al último frame
            if (_cicloUnico && _frameIndex == _decoder.Frames.Count - 1)
                RestablecerPrimerFrame();
        }

        private void RestablecerPrimerFrame()
        {
            DetenerAnimacion();
            if (_decoder?.Frames.Count > 0)
                Source = _decoder.Frames[0];
        }

        private void DetenerAnimacion()
        {
            _timer?.Stop();
            _timer         = null;
            _reproduciendo = false;
        }

        private static int ObtenerDelay(BitmapMetadata? meta)
        {
            try
            {
                if (meta?.GetQuery("/grctlext/Delay") is ushort raw && raw > 0)
                    return raw * 10; // centésimas ? milisegundos
            }
            catch { }
            return 100; // fallback 10 fps
        }
    }
}
