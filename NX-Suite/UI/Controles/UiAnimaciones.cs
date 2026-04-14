using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace NX_Suite.UI.Controles
{
    public static class UiAnimaciones
    {
        public static void AbrirPanelIzquierdo(FrameworkElement riel, FrameworkElement contenedor, FrameworkElement overlay)
        {
            MostrarOverlay(overlay);
            riel.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(280, TimeSpan.FromSeconds(0.4)) { EasingFunction = new QuinticEase() });
            contenedor.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(0.4)));
        }

        public static void AbrirPanelDerecho(FrameworkElement riel, FrameworkElement contenedor, FrameworkElement overlay)
        {
            MostrarOverlay(overlay);
            riel.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(220, TimeSpan.FromSeconds(0.4)) { EasingFunction = new QuinticEase() });
            contenedor.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(0.4)));
        }

        public static void CerrarPaneles(FrameworkElement rielIzq, FrameworkElement contIzq, FrameworkElement rielDer, FrameworkElement contDer, FrameworkElement overlay)
        {
            OcultarOverlay(overlay);

            // Cierra el izquierdo si tiene ancho
            if (rielIzq.Width > 10 || double.IsNaN(rielIzq.Width))
            {
                rielIzq.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(10, TimeSpan.FromSeconds(0.3)));
                contIzq.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(0.2)));
            }

            // Cierra el derecho si tiene ancho
            if (rielDer.Width > 10 || double.IsNaN(rielDer.Width))
            {
                rielDer.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(10, TimeSpan.FromSeconds(0.3)));
                contDer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(0.2)));
            }
        }

        private static void MostrarOverlay(FrameworkElement overlay)
        {
            overlay.Visibility = Visibility.Visible;
            overlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.5, TimeSpan.FromSeconds(0.3)));
        }

        private static void OcultarOverlay(FrameworkElement overlay)
        {
            DoubleAnimation anim = new DoubleAnimation(0, TimeSpan.FromSeconds(0.2));
            anim.Completed += (s, e) => overlay.Visibility = Visibility.Collapsed;
            overlay.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }
}