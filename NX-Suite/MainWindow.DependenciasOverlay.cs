using NX_Suite.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — Overlay "mesa de crafteo" para dependencias.
    ///
    /// Diseño inspirado en Minecraft crafting table:
    ///   Izquierda  ? ingredientes (deps B, C…) con sus botones activos.
    ///   Centro     ? flecha animada que se ilumina al completarse los ingredientes.
    ///   Derecha    ? resultado (módulo A), apagado hasta que todos los ingredientes
    ///                estén instalados; al iluminarse el usuario hace clic en su
    ///                botón de tarjeta para instalarlo.
    ///
    /// El overlay se cierra automáticamente tras instalar A,
    /// o si el usuario hace clic fuera del panel (sin instalaciones activas).
    /// No hay botón de cerrar.
    /// </summary>
    public partial class MainWindow
    {
        // ?? Estado ??????????????????????????????????????????????????????????

        private ModuloConfig?         _moduloPrincipal;
        private List<ModuloConfig>?   _depsActuales;
        private string?               _letraSDCrafteo;
        private TaskCompletionSource<bool>? _depsCrafteoTcs;

        // Elementos con blur activo para restaurarlos al cerrar
        private readonly List<(UIElement Elem, Effect? Original)> _blurredElems = new();

        // ?? Punto de entrada ????????????????????????????????????????????????

        /// <summary>
        /// Muestra la mesa de crafteo, instala las deps interactivamente y,
        /// cuando el usuario instala el módulo principal (A), cierra el overlay.
        /// Devuelve <c>true</c> si A se instaló correctamente,
        /// <c>false</c> si el usuario canceló haciendo clic fuera.
        /// </summary>
        internal async Task<bool> MostrarCrafteoYInstalarAsync(
            ModuloConfig           modulo,
            List<ResultadoDependencia> depsConAccion,
            string                 letraSD)
        {
            _moduloPrincipal = modulo;
            _depsActuales    = depsConAccion.Select(d => d.Modulo).ToList();
            _letraSDCrafteo  = letraSD;
            _depsCrafteoTcs  = new TaskCompletionSource<bool>();

            // ?? Configurar UI ?????????????????????????????????????????????????
            ListaDepOverlay.ItemsSource = _depsActuales;
            ListaResultadoA.ItemsSource = new[] { modulo };

            // A comienza apagada e inactiva
            ContenedorResultadoA.Opacity          = 0.28;
            ContenedorResultadoA.IsHitTestVisible = false;

            // Resetear visuales de flecha y resultado
            PincelFlecha.Color         = Color.FromArgb(255, 30, 30, 56);
            PincelBordeResultado.Color = Color.FromArgb(255, 26, 26, 48);
            GlowFlecha.BlurRadius      = 0;
            GlowFlecha.Opacity         = 0;
            GlowResultado.BlurRadius   = 0;
            GlowResultado.Opacity      = 0;
            PincelBordeCrafteo.Color   = Color.FromArgb(255, 42, 42, 64);

            ActualizarTextoEstado();

            // ?? Suscribir monitoreo ???????????????????????????????????????????
            foreach (var dep in _depsActuales)
                if (dep is INotifyPropertyChanged npc)
                    npc.PropertyChanged += OnDepPropertyChanged;

            if (modulo is INotifyPropertyChanged mnpc)
                mnpc.PropertyChanged += OnModuloPrincipalPropertyChanged;

            // Verificar si alguna dep ya estaba instalada al abrir
            VerificarDepsCompletadas();

            // ?? Mostrar con animación de entrada ??????????????????????????????
            await AnimarEntrada();

            // ?? Esperar resolución del crafteo ????????????????????????????????
            bool resultado = await _depsCrafteoTcs.Task;

            // ?? Limpiar suscripciones ?????????????????????????????????????????
            foreach (var dep in _depsActuales)
                if (dep is INotifyPropertyChanged npc)
                    npc.PropertyChanged -= OnDepPropertyChanged;

            if (modulo is INotifyPropertyChanged mnpc2)
                mnpc2.PropertyChanged -= OnModuloPrincipalPropertyChanged;

            await AnimarSalida();

            return resultado;
        }

        // ?? Monitoreo de propiedades ?????????????????????????????????????????

        private void OnDepPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ModuloConfig.EstadoSd)
                               or nameof(ModuloConfig.EstaInstalando))
            {
                Dispatcher.BeginInvoke(VerificarDepsCompletadas);
            }
        }

        private void OnModuloPrincipalPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Cuando A termina de instalarse: esperar un momento y cerrar el overlay
            if (e.PropertyName != nameof(ModuloConfig.EstaInstalando)) return;
            if (_moduloPrincipal?.EstaInstalando != false)             return;
            if (_moduloPrincipal?.EstadoSd       != EstadoSdModulo.Instalado) return;

            Dispatcher.BeginInvoke(async () =>
            {
                TxtEstadoCrafteo.Text = $"?  {_moduloPrincipal?.Nombre} instalado correctamente";
                await Task.Delay(900);
                _depsCrafteoTcs?.TrySetResult(true);
            });
        }

        // ?? Lógica de deps ???????????????????????????????????????????????????

        private void VerificarDepsCompletadas()
        {
            if (_depsActuales == null) return;

            ActualizarTextoEstado();

            bool todasListas = _depsActuales.All(
                d => d.EstadoSd == EstadoSdModulo.Instalado);

            if (todasListas && !ContenedorResultadoA.IsHitTestVisible)
                DesbloquearResultado();
        }

        private void DesbloquearResultado()
        {
            // Habilitar interacción con la tarjeta A
            ContenedorResultadoA.IsHitTestVisible = true;

            // Animar opacity: apagado ? encendido
            ContenedorResultadoA.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0.28, 1.0,
                    new Duration(TimeSpan.FromMilliseconds(700))));

            // Flecha: dim ? verde brillante
            PincelFlecha.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(Color.FromArgb(255, 64, 192, 87),
                    new Duration(TimeSpan.FromMilliseconds(600))));

            GlowFlecha.BeginAnimation(DropShadowEffect.BlurRadiusProperty,
                new DoubleAnimation(0, 20,
                    new Duration(TimeSpan.FromMilliseconds(600))));

            GlowFlecha.BeginAnimation(DropShadowEffect.OpacityProperty,
                new DoubleAnimation(0, 0.9,
                    new Duration(TimeSpan.FromMilliseconds(600))));

            // Borde resultado: dim ? verde con glow
            PincelBordeResultado.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(Color.FromArgb(255, 64, 192, 87),
                    new Duration(TimeSpan.FromMilliseconds(600))));

            GlowResultado.BeginAnimation(DropShadowEffect.BlurRadiusProperty,
                new DoubleAnimation(0, 28,
                    new Duration(TimeSpan.FromMilliseconds(700))));

            GlowResultado.BeginAnimation(DropShadowEffect.OpacityProperty,
                new DoubleAnimation(0, 0.75,
                    new Duration(TimeSpan.FromMilliseconds(700))));

            // Borde del panel crafteo: neutro ? tenue verde
            PincelBordeCrafteo.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(Color.FromArgb(110, 64, 192, 87),
                    new Duration(TimeSpan.FromMilliseconds(700))));
        }

        private void ActualizarTextoEstado()
        {
            if (_depsActuales == null || TxtEstadoCrafteo == null) return;

            int total     = _depsActuales.Count;
            int instaladas = _depsActuales.Count(d => d.EstadoSd == EstadoSdModulo.Instalado);

            TxtEstadoCrafteo.Text = instaladas == total
                ? $"?  Dependencias listas — haz clic en INSTALAR de {_moduloPrincipal?.Nombre}"
                : $"Instala {total - instaladas} componente{(total - instaladas != 1 ? "s" : "")} " +
                  $"para desbloquear {_moduloPrincipal?.Nombre}  " +
                  $"({instaladas}/{total} listo{(instaladas != 1 ? "s" : "")})";
        }

        // ?? Handlers de click ????????????????????????????????????????????????

        /// <summary>El usuario hace clic en el botón de una tarjeta de dep (B ó C).</summary>
        private async void DepCatalogo_ClickBoton(object sender, RoutedEventArgs e)
        {
            // Mismo patrón que Catalogo_ClickBoton: OriginalSource debe ser un Button
            // con DataContext = ModuloConfig. Esto garantiza compatibilidad con SafeButton.
            if (e.OriginalSource is not Button btn || btn.DataContext is not ModuloConfig modulo)
                return;

            if (_letraSDCrafteo == null || _depsActuales == null) return;
            if (!_depsActuales.Contains(modulo)) return;
            if (modulo.EstaInstalando) return;

            await EjecutarInstalacionRapidaAsync(modulo, _letraSDCrafteo,
                resolverDependencias: false);
        }

        /// <summary>El usuario hace clic en el botón INSTALAR de la tarjeta A (resultado).</summary>
        private async void ResultadoCatalogo_ClickBoton(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn || btn.DataContext is not ModuloConfig modulo)
                return;

            if (_letraSDCrafteo == null || _moduloPrincipal == null) return;
            if (!ReferenceEquals(modulo, _moduloPrincipal)) return;
            if (_moduloPrincipal.EstaInstalando) return;

            await EjecutarInstalacionRapidaAsync(_moduloPrincipal, _letraSDCrafteo,
                resolverDependencias: false);
            // El cierre automático lo gestiona OnModuloPrincipalPropertyChanged
        }

        /// <summary>Click en el backdrop (fuera del panel): cancela si no hay instalación activa.</summary>
        private void OverlayDepsBackdrop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            bool activo = (_depsActuales?.Any(d => d.EstaInstalando) == true)
                       || (_moduloPrincipal?.EstaInstalando == true);

            if (activo) return; // ignorar clic durante una instalación

            _depsCrafteoTcs?.TrySetResult(false);
        }

        // ?? Animaciones ??????????????????????????????????????????????????????

        private async Task AnimarEntrada()
        {
            _blurredElems.Clear();

            // Blur en todos los hijos de MainGrid EXCEPTO el overlay
            foreach (UIElement child in MainGrid.Children)
            {
                if (ReferenceEquals(child, PanelDependenciasOverlay)) continue;

                var original   = child.Effect;
                var blurEffect = new BlurEffect { Radius = 0 };
                child.Effect   = blurEffect;
                blurEffect.BeginAnimation(BlurEffect.RadiusProperty,
                    new DoubleAnimation(0, 18,
                        new Duration(TimeSpan.FromMilliseconds(420))));

                _blurredElems.Add((child, original));
            }

            PanelDependenciasOverlay.Opacity    = 0;
            PanelDependenciasOverlay.Visibility = Visibility.Visible;

            var tcs    = new TaskCompletionSource<bool>();
            var fadeIn = new DoubleAnimation(0, 1,
                new Duration(TimeSpan.FromMilliseconds(320)));
            fadeIn.Completed += (_, _) => tcs.TrySetResult(true);
            PanelDependenciasOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            await tcs.Task;
        }

        private async Task AnimarSalida()
        {
            foreach (var (elem, original) in _blurredElems)
            {
                if (elem.Effect is BlurEffect blur)
                {
                    var captElem     = elem;
                    var captOriginal = original;
                    var blurOut = new DoubleAnimation(18, 0,
                        new Duration(TimeSpan.FromMilliseconds(300)));
                    blurOut.Completed += (_, _) => captElem.Effect = captOriginal;
                    blur.BeginAnimation(BlurEffect.RadiusProperty, blurOut);
                }
            }

            var tcs     = new TaskCompletionSource<bool>();
            var fadeOut = new DoubleAnimation(1, 0,
                new Duration(TimeSpan.FromMilliseconds(260)));
            fadeOut.Completed += (_, _) => tcs.TrySetResult(true);
            PanelDependenciasOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);

            await tcs.Task;

            PanelDependenciasOverlay.Visibility = Visibility.Collapsed;
            _blurredElems.Clear();
        }
    }
}
