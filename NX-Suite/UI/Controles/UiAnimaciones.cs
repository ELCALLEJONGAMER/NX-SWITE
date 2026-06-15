using System;
using System.Windows;
using System.Windows.Media;
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

        // ── Transición entre mundos ──────────────────────────────────

        /// <summary>
        /// Transición gamer al cambiar de mundo:
        /// OUT → slide izquierda + fade + micro-escala (QuinticEase In) →
        /// flash neon cian → IN → slide derecha + fade + escala restaurada (ExponentialEase Out).
        /// </summary>
        public static void TransicionMundo(
            FrameworkElement contenido,
            ScaleTransform   scale,
            TranslateTransform translate,
            UIElement        flash,
            Action           onCargar)
        {
            const double DurOut    = 0.17;   // segundos fase salida
            const double DurIn     = 0.24;   // segundos fase entrada
            const double Slide     = 38.0;   // píxeles de desplazamiento
            const double EscalaMin = 0.975;  // escala mínima (efecto profundidad)

            var easeIn  = new QuinticEase     { EasingMode = EasingMode.EaseIn  };
            var easeOut = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5 };

            // ── FASE OUT ────────────────────────────────────────────────
            var fadeOut  = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(DurOut)) { EasingFunction = easeIn };
            var slideOut = new DoubleAnimation(0, -Slide, TimeSpan.FromSeconds(DurOut)) { EasingFunction = easeIn };
            var scaleOut = new DoubleAnimation(1, EscalaMin, TimeSpan.FromSeconds(DurOut)) { EasingFunction = easeIn };

            fadeOut.Completed += (_, _) =>
            {
                // Posicionar para la entrada (viene desde la derecha, invisible)
                translate.X  = Slide;
                scale.ScaleX = EscalaMin;
                scale.ScaleY = EscalaMin;

                // Cargar nuevo contenido mientras todo es invisible
                onCargar?.Invoke();

                // ── FASE IN ──────────────────────────────────────────────
                contenido.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromSeconds(DurIn)) { EasingFunction = easeOut });

                translate.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(Slide, 0, TimeSpan.FromSeconds(DurIn)) { EasingFunction = easeOut });

                scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(EscalaMin, 1, TimeSpan.FromSeconds(DurIn)) { EasingFunction = easeOut });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(EscalaMin, 1, TimeSpan.FromSeconds(DurIn)) { EasingFunction = easeOut });
            };

            // ── FLASH neon cian: sube al punto medio de OUT, baja durante IN ──
            var flashIn = new DoubleAnimation(0, 0.28, TimeSpan.FromSeconds(DurOut * 0.55))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
            };
            flashIn.Completed += (_, _) =>
            {
                flash.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0.28, 0, TimeSpan.FromSeconds(DurIn * 0.7))
                    {
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn }
                    });
            };

            contenido.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            translate.BeginAnimation(TranslateTransform.XProperty, slideOut);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
            flash.BeginAnimation(UIElement.OpacityProperty, flashIn);
        }

        // ── Catálogo ─────────────────────────────────────────────────

        /// <summary>
        /// Aparición escalonada de tarjetas al cargar el catálogo.
        /// Llama una vez por cada UIElement del ItemsControl.
        /// </summary>
        public static void AnimarEntradaTarjeta(UIElement elemento, int indice)
        {
            elemento.Opacity = 0;

            var delay = TimeSpan.FromMilliseconds(indice * 45);
            var ease  = new CubicEase { EasingMode = EasingMode.EaseOut };

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.35))
            {
                BeginTime      = delay,
                EasingFunction = ease
            };
            elemento.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            if (elemento is FrameworkElement fe)
            {
                var tt = new TranslateTransform(0, 18);
                fe.RenderTransform = tt;
                var slideIn = new DoubleAnimation(18, 0, TimeSpan.FromSeconds(0.35))
                {
                    BeginTime      = delay,
                    EasingFunction = ease
                };
                tt.BeginAnimation(TranslateTransform.YProperty, slideIn);
            }
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