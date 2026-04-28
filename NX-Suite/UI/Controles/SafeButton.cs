using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace NX_Suite.UI.Controles
{
    /// <summary>
    /// Botón que requiere mantener pulsado durante <see cref="HoldTimeSeconds"/> segundos
    /// para confirmar la acción. Expone <see cref="ProgressScale"/> (0–1) para animar
    /// un relleno visual en el ControlTemplate sin necesidad de converters.
    /// Cuando <see cref="IsSafeMode"/> es false se comporta como un Button normal.
    /// </summary>
    public class SafeButton : Button
    {
        // ?? Timer ?????????????????????????????????????????????????????????????

        private readonly DispatcherTimer _timer;
        private DateTime _pressStartTime;

        // ?? Dependency Properties ??????????????????????????????????????????????

        /// <summary>Activa o desactiva el modo de confirmación por hold.</summary>
        public bool IsSafeMode
        {
            get => (bool)GetValue(IsSafeModeProperty);
            set => SetValue(IsSafeModeProperty, value);
        }
        public static readonly DependencyProperty IsSafeModeProperty =
            DependencyProperty.Register(nameof(IsSafeMode), typeof(bool), typeof(SafeButton),
                new PropertyMetadata(false, OnIsSafeModeChanged));

        /// <summary>Segundos que hay que mantener pulsado. Por defecto 2.</summary>
        public double HoldTimeSeconds
        {
            get => (double)GetValue(HoldTimeSecondsProperty);
            set => SetValue(HoldTimeSecondsProperty, value);
        }
        public static readonly DependencyProperty HoldTimeSecondsProperty =
            DependencyProperty.Register(nameof(HoldTimeSeconds), typeof(double), typeof(SafeButton),
                new PropertyMetadata(2.0));

        /// <summary>Progreso de 0 a 100.</summary>
        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            private set => SetValue(ProgressProperty, value);
        }
        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register(nameof(Progress), typeof(double), typeof(SafeButton),
                new PropertyMetadata(0.0));

        /// <summary>
        /// Escala de 0.0 a 1.0 lista para bindear directamente a ScaleTransform.ScaleX
        /// en el ControlTemplate sin necesidad de ningún converter.
        /// </summary>
        public double ProgressScale
        {
            get => (double)GetValue(ProgressScaleProperty);
            private set => SetValue(ProgressScaleProperty, value);
        }
        public static readonly DependencyProperty ProgressScaleProperty =
            DependencyProperty.Register(nameof(ProgressScale), typeof(double), typeof(SafeButton),
                new PropertyMetadata(0.0));

        // ?? Constructor ????????????????????????????????????????????????????????

        public SafeButton()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += OnTick;
        }

        // ?? Overrides ??????????????????????????????????????????????????????????

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (IsSafeMode)
            {
                _pressStartTime = DateTime.Now;
                _timer.Start();
                e.Handled = true;   // evita el Click inmediato
                return;
            }
            base.OnPreviewMouseLeftButtonDown(e);
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (IsSafeMode) { Reset(); return; }
            base.OnPreviewMouseLeftButtonUp(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            Reset();
            base.OnMouseLeave(e);
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            Reset();
            base.OnLostMouseCapture(e);
        }

        // ?? Timer ??????????????????????????????????????????????????????????????

        private void OnTick(object? sender, EventArgs e)
        {
            double elapsed = (DateTime.Now - _pressStartTime).TotalSeconds;
            double hold    = Math.Max(HoldTimeSeconds, 0.1);
            double scale   = Math.Min(elapsed / hold, 1.0);

            Progress      = scale * 100.0;
            ProgressScale = scale;

            if (elapsed >= hold)
            {
                Reset();
                OnClick();  // dispara el evento Click estándar de WPF
            }
        }

        private void Reset()
        {
            _timer.Stop();
            Progress      = 0.0;
            ProgressScale = 0.0;
        }

        // ?? Callback ???????????????????????????????????????????????????????????

        private static void OnIsSafeModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SafeButton btn && !(bool)e.NewValue)
                btn.Reset();
        }
    }
}
