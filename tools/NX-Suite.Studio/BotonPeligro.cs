using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NX_Suite.Studio
{
    /// <summary>
    /// Botón rojo para acciones destructivas: hay que mantenerlo pulsado
    /// (<see cref="Duracion"/>) mientras se rellena; soltar antes lo cancela.
    /// Evita borrados por un clic accidental.
    /// </summary>
    internal sealed class BotonPeligro : Button
    {
        private static readonly Color Rojo = Color.FromRgb(0xC0, 0x30, 0x30);
        private static readonly Color Fondo = Color.FromRgb(0x3A, 0x18, 0x1C);
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        private readonly LinearGradientBrush _relleno;
        private readonly Action _accion;
        private DateTime _inicio;

        public TimeSpan Duracion { get; init; } = TimeSpan.FromMilliseconds(900);

        /// <summary>Plantilla plana: la estándar repinta el fondo al pasar el ratón y ocultaría el relleno.</summary>
        private static readonly ControlTemplate Plantilla = CrearPlantilla();

        private static ControlTemplate CrearPlantilla()
        {
            var borde = new FrameworkElementFactory(typeof(Border));
            borde.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            borde.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            borde.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(BorderBrush)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            borde.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(BorderThickness)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            borde.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding(nameof(Padding)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            var contenido = new FrameworkElementFactory(typeof(ContentPresenter));
            contenido.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contenido.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            borde.AppendChild(contenido);
            var plantilla = new ControlTemplate(typeof(Button)) { VisualTree = borde };
            var deshabilitado = new Trigger { Property = IsEnabledProperty, Value = false };
            deshabilitado.Setters.Add(new Setter(OpacityProperty, 0.45));
            plantilla.Triggers.Add(deshabilitado);
            return plantilla;
        }

        public BotonPeligro(string texto, Action accion)
        {
            _accion = accion;
            Content = texto;
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0xB0));
            BorderBrush = new SolidColorBrush(Rojo);
            _relleno = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            _relleno.GradientStops.Add(new GradientStop(Rojo, 0));
            _relleno.GradientStops.Add(new GradientStop(Rojo, 0));
            _relleno.GradientStops.Add(new GradientStop(Fondo, 0));
            _relleno.GradientStops.Add(new GradientStop(Fondo, 1));
            Background = _relleno;
            ToolTip = "Mantén pulsado para confirmar";
            Template = Plantilla;
            BorderThickness = new Thickness(1);

            _timer.Tick += (_, _) =>
            {
                double avance = Math.Min(1, (DateTime.Now - _inicio).TotalMilliseconds / Duracion.TotalMilliseconds);
                Pintar(avance);
                if (avance < 1) return;
                Cancelar();
                _accion();
            };
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (!IsEnabled) return;
            CaptureMouse();
            _inicio = DateTime.Now;
            _timer.Start();
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            e.Handled = true;
            Cancelar();
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            Cancelar();
        }

        // Teclado (Enter/Espacio) desactivado: solo se confirma manteniendo con el ratón.
        protected override void OnClick() { }

        private void Cancelar()
        {
            _timer.Stop();
            if (IsMouseCaptured) ReleaseMouseCapture();
            Pintar(0);
        }

        private void Pintar(double avance)
        {
            _relleno.GradientStops[1].Offset = avance;
            _relleno.GradientStops[2].Offset = avance;
        }
    }
}
