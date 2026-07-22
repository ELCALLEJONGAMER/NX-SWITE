using NX_Swite.Core;
using NX_Swite.Core.Configuracion;
using NX_Swite.Hardware;
using NX_Swite.Models;
using NX_Swite.UI.Controles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Animation;

namespace NX_Swite
{
    /// <summary>
    /// MainWindow � Handlers del overlay <c>PanelAsistidoCompletoOverlay</c>.
    /// Sustituye a la antigua <c>VentanaAsistidoCompleto</c>: ahora vive como
    /// overlay dentro del MainWindow, lee la SD del panel derecho, mantiene
    /// el slider emuMMC + tarjetas de m�dulos recomendados/dependencias y
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
        private bool _modoSoloInstalar = false;

        private static readonly int[] _gbTicksAsistido = { 4, 8, 12, 16, 24, 32, 48, 64 };

        // ?? Apertura / cierre ????????????????????????????????????????????????

        public void AbrirOverlayAsistidoCompleto()
        {
            _asistidoEnProceso = false;
            _modoSoloInstalar = false;
            _sdSelAsistido = InfoSD.ComboDrives.SelectedItem as SDInfo;
            TxtEtiquetaAsistido.Text = ConfiguracionLocal.EtiquetaSwitchSd;

            CargarRecomendadosAsistido();
            ActualizarInfoSDAsistido();
            ActualizarSliderAsistido((int)SliderGbAsistido.Value);
            AplicarModoAsistido();

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
                TxtLetraSDAsistido.Text  = "�";
                TxtNombreSDAsistido.Text = "Sin SD seleccionada";
                TxtInfoSDAsistido.Text   = "Selecciona una SD en el panel derecho";
                AvisoSinSDAsistido.Visibility = Visibility.Visible;
                BtnIniciarAsistido.IsEnabled = false;
                TxtEstadoAsistido.Text = "Conecta o selecciona una microSD para continuar";
                return;
            }

            string cap = string.IsNullOrEmpty(_sdSelAsistido.CapacidadTotal) || _sdSelAsistido.CapacidadTotal == "0"
                ? "Tama�o desconocido"
                : $"{_sdSelAsistido.CapacidadTotal} GB";

            TxtLetraSDAsistido.Text  = _sdSelAsistido.Letra.TrimEnd('\\', ':');
            TxtNombreSDAsistido.Text = string.IsNullOrWhiteSpace(_sdSelAsistido.Etiqueta)
                ? "Sin etiqueta"
                : _sdSelAsistido.Etiqueta;
            TxtInfoSDAsistido.Text   = $"{cap}  �  Disco #{_sdSelAsistido.DiscoFisico}  �  {(string.IsNullOrEmpty(_sdSelAsistido.Formato) ? "RAW" : _sdSelAsistido.Formato)}";

            AvisoSinSDAsistido.Visibility = Visibility.Collapsed;
            BtnIniciarAsistido.IsEnabled = _recomendadosAsistido.Count > 0;
            TxtEstadoAsistido.Text = _modoSoloInstalar
                ? "Mant�n pulsado INSTALAR M�DULOS para confirmar"
                : "Mant�n pulsado INICIAR PROCESO COMPLETO para confirmar";

            // Recomendar tama�o seg�n capacidad (>=512 GB ? 24 GB; resto ? 12 GB)
            if (int.TryParse(_sdSelAsistido.CapacidadTotal, out int sdGb))
            {
                int rec = sdGb >= 512 ? 24 : 12;
                int idx = Array.IndexOf(_gbTicksAsistido, rec);
                if (idx >= 0 && (int)SliderGbAsistido.Value != idx)
                    SliderGbAsistido.Value = idx;
            }
        }

        // ?? Selector de modo ?????????????????????????????????????????????????

        private void BtnModoCompleto_Click(object sender, RoutedEventArgs e)
        {
            if (_modoSoloInstalar)
            {
                _modoSoloInstalar = false;
                AplicarModoAsistido();
                ActualizarInfoSDAsistido();
            }
        }

        private void BtnModoSoloInstalar_Click(object sender, RoutedEventArgs e)
        {
            if (!_modoSoloInstalar)
            {
                _modoSoloInstalar = true;
                AplicarModoAsistido();
                ActualizarInfoSDAsistido();
            }
        }

        private void AplicarModoAsistido()
        {
            // Panel de opciones de formato (slider + etiqueta)
            PanelOpcionesFormato.Visibility = _modoSoloInstalar
                ? Visibility.Collapsed
                : Visibility.Visible;

            // Warning destructivo
            WarningDestructivoAsistido.Visibility = _modoSoloInstalar
                ? Visibility.Collapsed
                : Visibility.Visible;

            // ScrollViewer de m�dulos: m�s espacio en modo Solo Instalar
            ScrollModulosAsistido.Height = _modoSoloInstalar ? 320 : 145;

            // Texto del bot�n
            TxtBtnIniciarAsistido.Text = _modoSoloInstalar
                ? "INSTALAR M�DULOS"
                : "INICIAR PROCESO COMPLETO";

            // Estilo visual del selector: solo Background y color del texto
            // El BorderBrush neon lo gestiona el Style/Trigger del XAML en hover.
            BtnModoCompleto.Background = _modoSoloInstalar
                ? System.Windows.Media.Brushes.Transparent
                : new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1400D2FF"));
            BtnModoCompleto.BorderBrush = _modoSoloInstalar
                ? new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#252535"))
                : new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00D2FF"));

            var txtCompleto = (System.Windows.Controls.TextBlock)FindName("TxtModoCompleto");
            txtCompleto.Foreground = _modoSoloInstalar
                ? new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#506070"))
                : new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00D2FF"));

            BtnModoSoloInstalar.Background = _modoSoloInstalar
                ? new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1400D2FF"))
                : System.Windows.Media.Brushes.Transparent;
            BtnModoSoloInstalar.BorderBrush = _modoSoloInstalar
                ? new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00D2FF"))
                : new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#252535"));

            var txtSolo = (System.Windows.Controls.TextBlock)FindName("TxtModoSoloInstalar");
            txtSolo.Foreground = _modoSoloInstalar
                ? new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00D2FF"))
                : new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#506070"));
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

        // ?? Carga de recomendados + resoluci�n de dependencias ???????????????

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

            // En modo completo la SD se formatea, as� que EstadoSd es irrelevante:
            // resolvemos deps por ID declarado.
            var idsRecomendados = _recomendadosAsistido
                .Select(v => v.Modulo.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Recorrer las dependencias rastreando qu� m�dulo las declara.
            // Si CUALQUIER alternativa OR ya est� en recomendados, la entrada queda cubierta.
            var depRequierenPor = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);
            var yaAgregados    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _depsAsistido      = new System.Collections.Generic.List<ModuloConfig>();

            foreach (var rec in _recomendadosAsistido)
            {
                foreach (var entrada in rec.Modulo.Dependencias ?? new System.Collections.Generic.List<string>())
                {
                    var alternativas = AnalizadorDependencias.ParsearAlternativas(entrada);

                    // Si alguna alternativa ya est� cubierta por los recomendados, saltar
                    if (alternativas.Any(alt => idsRecomendados.Contains(alt)))
                        continue;

                    var (modulo, _) = AnalizadorDependencias.ResolverEntrada(entrada, todos);
                    if (modulo == null || idsRecomendados.Contains(modulo.Id)) continue;

                    if (!depRequierenPor.TryGetValue(modulo.Id, out var reqs))
                        depRequierenPor[modulo.Id] = reqs = new System.Collections.Generic.List<string>();
                    if (!reqs.Contains(rec.Modulo.Nombre))
                        reqs.Add(rec.Modulo.Nombre);

                    if (yaAgregados.Add(modulo.Id))
                        _depsAsistido.Add(modulo);
                }
            }

            // Texto "requerido por" por cada dep
            if (_depsAsistido.Count > 0 && depRequierenPor.Count > 0)
            {
                TxtDepsRequierenPor.Text = string.Join("\n", _depsAsistido
                    .Where(m => depRequierenPor.ContainsKey(m.Id))
                    .Select(m => $"{m.Nombre}: requerido por {string.Join(", ", depRequierenPor[m.Id])}"));
                TxtDepsRequierenPor.Visibility = Visibility.Visible;
            }
            else
            {
                TxtDepsRequierenPor.Visibility = Visibility.Collapsed;
            }

            PanelDepsAsistido.Visibility = _depsAsistido.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            ListaDepsAsistido.ItemsSource = _depsAsistido;
        }

        // ?? Acci�n principal: lanzar el flujo asistido completo ??????????????

        private void BtnIniciarAsistido_Click(object sender, RoutedEventArgs e)
        {
            // Releer la SD por si el usuario cambi� la selecci�n en el panel derecho
            _sdSelAsistido = InfoSD.ComboDrives.SelectedItem as SDInfo;
            if (_sdSelAsistido == null || _sdSelAsistido.DiscoFisico < 0)
            {
                ActualizarInfoSDAsistido();
                return;
            }

            if (_recomendadosAsistido.Count == 0)
            {
                TxtEstadoAsistido.Text = "No hay m�dulos recomendados para instalar";
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
                SoloInstalar    = _modoSoloInstalar,
                Logger          = null
            };

            // Cerrar el overlay � el progreso se ve en la pantalla de carga global
            AplicarBlurFondo(false);
            PanelAsistidoCompletoOverlay.Visibility = Visibility.Collapsed;

            // Reutiliza el handler ya existente (declarado en MainWindow.Asistido.cs).
            // Fire-and-forget: el m�todo es async void y gestiona todos los errores.
            VistaAsistida_ProcesarCompletoSolicitado(this, args);

            _asistidoEnProceso = false;
        }
    }
}
