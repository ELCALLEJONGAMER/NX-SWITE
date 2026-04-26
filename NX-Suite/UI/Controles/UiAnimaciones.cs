using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace NX_Suite.UI.Controles
{
    public static class UiAnimaciones
    {
        // ── Paneles laterales ────────────────────────────────────────

        public static void AbrirPanelIzquierdo(FrameworkElement riel, FrameworkElement contenedor, FrameworkElement overlay)
        {
            MostrarOverlay(overlay);
            riel.BeginAnimation(FrameworkElement.WidthProperty,
                new DoubleAnimation(280, TimeSpan.FromSeconds(0.4)) { EasingFunction = new QuinticEase() });
            contenedor.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, TimeSpan.FromSeconds(0.4)));
        }

        public static void AbrirPanelDerecho(FrameworkElement riel, FrameworkElement contenedor, FrameworkElement overlay)
        {
            MostrarOverlay(overlay);
            riel.BeginAnimation(FrameworkElement.WidthProperty,
                new DoubleAnimation(220, TimeSpan.FromSeconds(0.4)) { EasingFunction = new QuinticEase() });
            contenedor.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, TimeSpan.FromSeconds(0.4)));
        }

        public static void CerrarPaneles(FrameworkElement rielIzq, FrameworkElement contIzq,
                                         FrameworkElement rielDer, FrameworkElement contDer,
                                         FrameworkElement overlay)
        {
            OcultarOverlay(overlay);
            if (rielIzq.Width > 10 || double.IsNaN(rielIzq.Width))
            {
                rielIzq.BeginAnimation(FrameworkElement.WidthProperty,
                    new DoubleAnimation(10, TimeSpan.FromSeconds(0.3)));
                contIzq.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, TimeSpan.FromSeconds(0.2)));
            }
            if (rielDer.Width > 10 || double.IsNaN(rielDer.Width))
            {
                rielDer.BeginAnimation(FrameworkElement.WidthProperty,
                    new DoubleAnimation(10, TimeSpan.FromSeconds(0.3)));
                contDer.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, TimeSpan.FromSeconds(0.2)));
            }
        }

        public static void CerrarPanelIzquierdo(FrameworkElement riel, FrameworkElement contenedor)
        {
            riel.BeginAnimation(FrameworkElement.WidthProperty,
                new DoubleAnimation(15, TimeSpan.FromSeconds(0.3)));
            contenedor.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, TimeSpan.FromSeconds(0.2)));
        }

        public static void CerrarPanelDerecho(FrameworkElement riel, FrameworkElement contenedor, FrameworkElement overlay)
        {
            OcultarOverlay(overlay);
            riel.BeginAnimation(FrameworkElement.WidthProperty,
                new DoubleAnimation(10, TimeSpan.FromSeconds(0.3)));
            contenedor.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, TimeSpan.FromSeconds(0.2)));
        }

        // ── Catálogo ─────────────────────────────────────────────────

        /// <summary>
        /// Aparición escalonada de tarjetas al cargar el catálogo.
        /// Llama una vez por cada UIElement del ItemsControl.
        /// </summary>
        public static void AnimarEntradaTarjeta(UIElement elemento, int indice)
        {
            elemento.Opacity = 0;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.35))
            {
                BeginTime     = TimeSpan.FromMilliseconds(indice * 45),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var slideIn = new ThicknessAnimation(
                new Thickness(0, 18, 0, 0),
                new Thickness(0),
                TimeSpan.FromSeconds(0.35))
            {
                BeginTime      = TimeSpan.FromMilliseconds(indice * 45),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            elemento.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            if (elemento is FrameworkElement fe)
                fe.BeginAnimation(FrameworkElement.MarginProperty, slideIn);
        }

        /// <summary>
        /// Fade rápido de todo el panel de catálogo al cambiar de mundo/filtro.
        /// </summary>
        public static void FadeOutCatalogo(FrameworkElement catalogo, Action onCompleted)
        {
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.15));
            anim.Completed += (_, _) => onCompleted?.Invoke();
            catalogo.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        public static void FadeInCatalogo(FrameworkElement catalogo)
        {
            catalogo.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.25))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        }

        // ── Vista de detalle ─────────────────────────────────────────

        /// <summary>
        /// Transición al abrir la vista de detalle: fade + slide desde la derecha.
        /// </summary>
        public static void MostrarDetalle(FrameworkElement vistaDetalle)
        {
            vistaDetalle.Visibility = Visibility.Visible;
            vistaDetalle.Opacity    = 0;

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.25))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var slide = new ThicknessAnimation(
                new Thickness(30, 0, 0, 0),
                new Thickness(0),
                TimeSpan.FromSeconds(0.25))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            vistaDetalle.BeginAnimation(UIElement.OpacityProperty, fade);
            vistaDetalle.BeginAnimation(FrameworkElement.MarginProperty, slide);
        }

        /// <summary>
        /// Transición al volver al catálogo: fade out del detalle.
        /// </summary>
        public static void OcultarDetalle(FrameworkElement vistaDetalle, Action onCompleted)
        {
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.18));
            fade.Completed += (_, _) =>
            {
                vistaDetalle.Visibility = Visibility.Collapsed;
                onCompleted?.Invoke();
            };
            vistaDetalle.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        // ── Overlay ──────────────────────────────────────────────────

        private static void MostrarOverlay(FrameworkElement overlay)
        {
            overlay.Visibility = Visibility.Visible;
            overlay.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0.5, TimeSpan.FromSeconds(0.3)));
        }

        private static void OcultarOverlay(FrameworkElement overlay)
        {
            var anim = new DoubleAnimation(0, TimeSpan.FromSeconds(0.2));
            anim.Completed += (_, _) => overlay.Visibility = Visibility.Collapsed;
            overlay.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }
}