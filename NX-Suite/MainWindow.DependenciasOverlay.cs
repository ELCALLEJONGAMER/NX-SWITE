using NX_Swite.Models;
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

namespace NX_Swite
{
    /// <summary>
    /// MainWindow � Overlay "mesa de crafteo" para dependencias.
    ///
    /// Dise�o inspirado en Minecraft crafting table:
    ///   Izquierda  ? ingredientes (deps B, C�) con sus botones activos.
    ///   Centro     ? flecha animada que se ilumina al completarse los ingredientes.
    ///   Derecha    ? resultado (m�dulo A), apagado hasta que todos los ingredientes
    ///                est�n instalados; al iluminarse el usuario hace clic en su
    ///                bot�n de tarjeta para instalarlo.
    ///
    /// El overlay se cierra autom�ticamente tras instalar A,
    /// o si el usuario hace clic fuera del panel (sin instalaciones activas).
    /// No hay bot�n de cerrar.
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
        /// cuando el usuario instala el m�dulo principal (A), cierra el overlay.
        /// Devuelve <c>true</c> si A se instal� correctamente,
        /// <c>false</c> si el usuario cancel� haciendo clic fuera.
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

            // Refrescar estado en SD de los m�dulos involucrados antes de mostrar
            // el overlay: evita que datos en cach� causen visuales incorrectos
            // (ej.: m�dulo borrado de la SD que a�n se muestra como instalado).
            await RefrescarModulosOverlayAsync(letraSD, modulo, _depsActuales);

            // Verificar si alguna dep ya estaba instalada al abrir
            VerificarDepsCompletadas();

            // ?? Mostrar con animaci�n de entrada ??????????????????????????????
            await AnimarEntrada();

            // ?? Esperar resoluci�n del crafteo ????????????????????????????????
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

        // ?? L�gica de deps ???????????????????????????????????????????????????

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
            // Habilitar interacci�n con la tarjeta A
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
                ? $"?  Dependencias listas � haz clic en INSTALAR de {_moduloPrincipal?.Nombre}"
                : $"Instala {total - instaladas} componente{(total - instaladas != 1 ? "s" : "")} " +
                  $"para desbloquear {_moduloPrincipal?.Nombre}  " +
                  $"({instaladas}/{total} listo{(instaladas != 1 ? "s" : "")})";
        }

        // ?? Handlers de click ????????????????????????????????????????????????

        /// <summary>El usuario hace clic en el bot�n de una tarjeta de dep (B � C).</summary>
        private async void DepCatalogo_ClickBoton(object sender, RoutedEventArgs e)
        {
            // Mismo patr�n que Catalogo_ClickBoton: OriginalSource debe ser un Button
            // con DataContext = ModuloConfig. Esto garantiza compatibilidad con SafeButton.
            if (e.OriginalSource is not Button btn || btn.DataContext is not ModuloConfig modulo)
                return;

            if (_letraSDCrafteo == null || _depsActuales == null) return;
            if (!_depsActuales.Contains(modulo)) return;
            if (modulo.EstaInstalando) return;

            await EjecutarInstalacionRapidaAsync(modulo, _letraSDCrafteo,
                resolverDependencias: false);
        }

        /// <summary>El usuario hace clic en el bot�n INSTALAR de la tarjeta A (resultado).</summary>
        private async void ResultadoCatalogo_ClickBoton(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn || btn.DataContext is not ModuloConfig modulo)
                return;

            if (_letraSDCrafteo == null || _moduloPrincipal == null) return;
            if (!ReferenceEquals(modulo, _moduloPrincipal)) return;
            if (_moduloPrincipal.EstaInstalando) return;

            await EjecutarInstalacionRapidaAsync(_moduloPrincipal, _letraSDCrafteo,
                resolverDependencias: false);
            // El cierre autom�tico lo gestiona OnModuloPrincipalPropertyChanged
        }

        /// <summary>Click en el backdrop (fuera del panel): cancela si no hay instalaci�n activa.</summary>
        private void OverlayDepsBackdrop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            bool activo = (_depsActuales?.Any(d => d.EstaInstalando) == true)
                       || (_moduloPrincipal?.EstaInstalando == true);

            if (activo) return; // ignorar clic durante una instalaci�n

            _depsCrafteoTcs?.TrySetResult(false);
        }

        /// <summary>
        /// Instala todas las dependencias pendientes en orden,
        /// disparando cada una secuencialmente sin que el usuario pulse tarjeta por tarjeta.
        /// </summary>
        private async void BtnInstalarTodoDeps_Click(object sender, RoutedEventArgs e)
        {
            if (_depsActuales == null || _letraSDCrafteo == null) return;

            BtnInstalarTodoDeps.IsEnabled = false;

            var pendientes = _depsActuales
                .Where(d => d.EstadoSd != EstadoSdModulo.Instalado && !d.EstaInstalando)
                .ToList();

            foreach (var dep in pendientes)
                await EjecutarInstalacionRapidaAsync(dep, _letraSDCrafteo, resolverDependencias: false);

            BtnInstalarTodoDeps.IsEnabled = true;
        }

        /// <summary>
        /// Hold-to-confirm: instala el m�dulo principal directamente sin esperar a las deps.
        /// </summary>
        private async void BtnInstalarSinDeps_Click(object sender, RoutedEventArgs e)
        {
            if (_moduloPrincipal == null || _letraSDCrafteo == null) return;
            if (_moduloPrincipal.EstaInstalando) return;

            // Refrescar solo el m�dulo principal antes de instalar para asegurarnos
            // de que el estado en memoria refleja la realidad de la SD (el m�dulo
            // puede haber sido borrado manualmente entre la apertura del overlay
            // y este clic).
            await RefrescarModulosOverlayAsync(_letraSDCrafteo, _moduloPrincipal, null);

            await EjecutarInstalacionRapidaAsync(_moduloPrincipal, _letraSDCrafteo,
                resolverDependencias: false);
            // El cierre autom�tico lo gestiona OnModuloPrincipalPropertyChanged
        }

        // ?? Helpers de estado SD ??????????????????????????????????????????????????????

        /// <summary>
        /// Refresca el estado de la SD exclusivamente para los m�dulos involucrados
        /// en el overlay (principal + deps). Operaci�n ligera: sin red, sin
        /// escaneo recursivo completo del cat�logo.
        /// </summary>
        private async Task RefrescarModulosOverlayAsync(
            string letraSD,
            ModuloConfig principal,
            IEnumerable<ModuloConfig>? deps)
        {
            try
            {
                var modulos = deps != null
                    ? new[] { principal }.Concat(deps)
                    : new[] { principal };

                await _cerebro.RefrescarEstadosSinRedAsync(modulos, letraSD);
            }
            catch
            {
                // Silenciar: es un refresco de estado; si falla no bloqueamos el flujo
            }
        }

        // ?? Animaciones ??????????????????????????????????????????????????????

        private async Task AnimarEntrada()
        {
            _blurredElems.Clear();

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
