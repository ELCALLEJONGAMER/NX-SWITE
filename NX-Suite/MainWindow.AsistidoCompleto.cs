using NX_Suite.Core;
using NX_Suite.Core.Configuracion;
using NX_Suite.Hardware;
using NX_Suite.Models;
using NX_Suite.UI.Controles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Animation;

namespace NX_Suite
{
    /// <summary>
    /// MainWindow — Handlers del overlay <c>PanelAsistidoCompletoOverlay</c>.
    /// Sustituye a la antigua <c>VentanaAsistidoCompleto</c>: ahora vive como
    /// overlay dentro del MainWindow, lee la SD del panel derecho, mantiene
    /// el slider emuMMC + tarjetas de módulos recomendados/dependencias y
    /// usa un <c>SafeButton</c> (hold 2s) en el footer para confirmar.
    /// </summary>
    public partial class MainWindow
    {
        // ?? Estado ???????????????????????????????????????????????????????????

        private SDInfo? _sdSelAsistido;
        private int     _gbEmuMMCAsistido = 12;
        private List<ModuloConfig> _depsAsistido = new();
        private List<RecomendadoVM> _recomendadosAsistido = new();
        private bool _asistidoEnProceso;

        private static readonly int[] _gbTicksAsistido = { 4, 8, 12, 16, 24, 32, 48, 64 };

        // ?? Apertura / cierre ????????????????????????????????????????????????

        public void AbrirOverlayAsistidoCompleto()
        {
            _asistidoEnProceso = false;
            _sdSelAsistido = InfoSD.ComboDrives.SelectedItem as SDInfo;
            TxtEtiquetaAsistido.Text = ConfiguracionLocal.EtiquetaSwitchSd;

            CargarRecomendadosAsistido();
            ActualizarInfoSDAsistido();
            ActualizarSliderAsistido((int)SliderGbAsistido.Value);

            MostrarOverlayConAnimacion(PanelAsistidoCompletoOverlay);
        }

        private void PanelAsistidoCompleto_BackdropClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_asistidoEnProceso) return;
            CerrarOverlayAsistidoCompleto();
        }

        internal void CerrarOverlayAsistidoCompleto()
        {
            if (_asistidoEnProceso) return;

            var animacionSalida = new DoubleAnimation(PanelAsistidoCompletoOverlay.Opacity, 0,
                new Duration(TimeSpan.FromMilliseconds(220)));
            animacionSalida.Completed += (_, _) =>
            {
                AplicarBlurFondo(false);
                PanelAsistidoCompletoOverlay.Visibility = Visibility.Collapsed;
                PanelAsistidoCompletoOverlay.Opacity = 1;
            };

            PanelAsistidoCompletoOverlay.BeginAnimation(UIElement.OpacityProperty, animacionSalida);
        }

        // ?? Pintado de la tarjeta SD desde el panel derecho ??????????????????

        private void ActualizarInfoSDAsistido()
        {
            if (_sdSelAsistido == null || _sdSelAsistido.DiscoFisico < 0)
            {
                TxtLetraSDAsistido.Text  = "—";
                TxtNombreSDAsistido.Text = "Sin SD seleccionada";
                TxtInfoSDAsistido.Text   = "Selecciona una SD en el panel derecho";
                AvisoSinSDAsistido.Visibility = Visibility.Visible;
                BtnIniciarAsistido.IsEnabled = false;
                TxtEstadoAsistido.Text = "Conecta o selecciona una microSD para continuar";
                return;
            }

            string cap = string.IsNullOrEmpty(_sdSelAsistido.CapacidadTotal) || _sdSelAsistido.CapacidadTotal == "0"
                ? "Tamaño desconocido"
                : $"{_sdSelAsistido.CapacidadTotal} GB";

            TxtLetraSDAsistido.Text  = _sdSelAsistido.Letra.TrimEnd('\\', ':');
            TxtNombreSDAsistido.Text = string.IsNullOrWhiteSpace(_sdSelAsistido.Etiqueta)
                ? "Sin etiqueta"
                : _sdSelAsistido.Etiqueta;
            TxtInfoSDAsistido.Text   = $"{cap}  •  Disco #{_sdSelAsistido.DiscoFisico}  •  {(string.IsNullOrEmpty(_sdSelAsistido.Formato) ? "RAW" : _sdSelAsistido.Formato)}";

            AvisoSinSDAsistido.Visibility = Visibility.Collapsed;
            BtnIniciarAsistido.IsEnabled = _recomendadosAsistido.Count > 0;
            TxtEstadoAsistido.Text = "Mantén pulsado INICIAR PROCESO COMPLETO para confirmar";

            // Recomendar tamaño según capacidad (>=512 GB ? 24 GB; resto ? 12 GB)
            if (int.TryParse(_sdSelAsistido.CapacidadTotal, out int sdGb))
            {
                int rec = sdGb >= 512 ? 24 : 12;
                int idx = Array.IndexOf(_gbTicksAsistido, rec);
                if (idx >= 0 && (int)SliderGbAsistido.Value != idx)
                    SliderGbAsistido.Value = idx;
            }
        }

        // ?? Slider emuMMC ????????????????????????????????????????????????????

        private void SliderGbAsistido_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
            => ActualizarSliderAsistido((int)e.NewValue);

        private void ActualizarSliderAsistido(int indice)
        {
            indice = Math.Clamp(indice, 0, _gbTicksAsistido.Length - 1);
            _gbEmuMMCAsistido = _gbTicksAsistido[indice];

            TxtGbValorAsistido.Text     = $"{_gbEmuMMCAsistido} GB";
            BadgeRecAsistido.Visibility = (_gbEmuMMCAsistido == 12 || _gbEmuMMCAsistido == 24)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ?? Carga de recomendados + resolución de dependencias ???????????????

        private void CargarRecomendadosAsistido()
        {
            var todos = _catalogoModulos != null
                ? _catalogoModulos.ToList()
                : new List<ModuloConfig>();

            _recomendadosAsistido = ConfiguracionRemota.Recomendados
                .Select(r =>
                {
                    var m = todos.FirstOrDefault(x =>
                        string.Equals(x.Id, r.Id, StringComparison.OrdinalIgnoreCase));
                    return m == null ? null : new RecomendadoVM { Modulo = m, Config = r };
                })
                .Where(v => v != null)
                .Select(v => v!)
                .ToList();

            ListaModulosAsistido.ItemsSource = _recomendadosAsistido;

            // En modo completo la SD se formatea, así que EstadoSd es irrelevante:
            // resolvemos deps por ID declarado.
            var idsRecomendados = _recomendadosAsistido
                .Select(v => v.Modulo.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _depsAsistido = _recomendadosAsistido
                .SelectMany(v => v.Modulo.Dependencias ?? new List<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(entrada => AnalizadorDependencias.ResolverEntrada(entrada, todos))
                .Where(r => r.Modulo != null && !idsRecomendados.Contains(r.Modulo.Id))
                .Select(r => r.Modulo!)
                .GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            PanelDepsAsistido.Visibility = _depsAsistido.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            ListaDepsAsistido.ItemsSource = _depsAsistido;
        }

        // ?? Acción principal: lanzar el flujo asistido completo ??????????????

        private void BtnIniciarAsistido_Click(object sender, RoutedEventArgs e)
        {
            // Releer la SD por si el usuario cambió la selección en el panel derecho
            _sdSelAsistido = InfoSD.ComboDrives.SelectedItem as SDInfo;
            if (_sdSelAsistido == null || _sdSelAsistido.DiscoFisico < 0)
            {
                ActualizarInfoSDAsistido();
                return;
            }

            if (_recomendadosAsistido.Count == 0)
            {
                TxtEstadoAsistido.Text = "No hay módulos recomendados para instalar";
                return;
            }

            _asistidoEnProceso = true;
            string etiqueta = NormalizarEtiquetaVolumen(TxtEtiquetaAsistido.Text);
            TxtEtiquetaAsistido.Text = etiqueta;

            // Componer args y delegar al pipeline existente
            var modulosPrincipales = _recomendadosAsistido.Select(v => v.Modulo).ToList();
            var modulos = _depsAsistido.Concat(modulosPrincipales).ToList();
            var idsDeps = _depsAsistido.Select(m => m.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var args = new ProcesarCompletoArgs
            {
                GbEmuMMC        = _gbEmuMMCAsistido,
                LetraSD         = _sdSelAsistido.Letra,
                Etiqueta        = etiqueta,
                NumeroDisco     = _sdSelAsistido.DiscoFisico,
                Modulos         = modulos,
                IdsDependencias = idsDeps,
                Logger          = null
            };

            // Cerrar el overlay — el progreso se ve en la pantalla de carga global
            AplicarBlurFondo(false);
            PanelAsistidoCompletoOverlay.Visibility = Visibility.Collapsed;

            // Reutiliza el handler ya existente (declarado en MainWindow.Asistido.cs).
            // Fire-and-forget: el método es async void y gestiona todos los errores.
            VistaAsistida_ProcesarCompletoSolicitado(this, args);

            _asistidoEnProceso = false;
        }
    }
}
