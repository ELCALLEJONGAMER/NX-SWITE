using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace NX_Suite.UI.Controles
{
    public class SafeButton : Button
    {
        private DispatcherTimer _timer;
        private DateTime _pressStartTime;
        private const double DEFAULT_HOLD_TIME = 3.0;

        // Propiedad para activar/desactivar el modo seguro desde XAML
        public bool IsSafeMode
        {
            get { return (bool)GetValue(IsSafeModeProperty); }
            set { SetValue(IsSafeModeProperty, value); }
        }
        public static readonly DependencyProperty IsSafeModeProperty =
            DependencyProperty.Register("IsSafeMode", typeof(bool), typeof(SafeButton), new PropertyMetadata(false));

        // Propiedad para ver el progreso (0 a 100)
        public double Progress
        {
            get { return (double)GetValue(ProgressProperty); }
            private set { SetValue(ProgressProperty, value); }
        }
        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register("Progress", typeof(double), typeof(SafeButton), new PropertyMetadata(0.0));

        public SafeButton()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _timer.Tick += Timer_Tick;
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (IsSafeMode)
            {
                _pressStartTime = DateTime.Now;
                _timer.Start();
                e.Handled = true; // Evita el click instantáneo
            }
            base.OnPreviewMouseLeftButtonDown(e);
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            ResetTimer();
            base.OnPreviewMouseLeftButtonUp(e);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            double elapsed = (DateTime.Now - _pressStartTime).TotalSeconds;
            Progress = Math.Min(elapsed / DEFAULT_HOLD_TIME * 100, 100);

            if (elapsed >= DEFAULT_HOLD_TIME)
            {
                ResetTimer();
                OnClick(); // Dispara el evento Click normal de WPF
            }
        }

        private void ResetTimer()
        {
            _timer.Stop();
            Progress = 0;
        }
    }
}