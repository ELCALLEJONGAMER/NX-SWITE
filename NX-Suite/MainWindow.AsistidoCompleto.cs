using NX_Swite.Core;
using NX_Swite.Core.Configuracion;
using NX_Swite.Hardware;
using NX_Swite.Models;
using NX_Swite.UI;
using NX_Swite.UI.Controles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        private bool _selectorModoAsistidoVisible = true;

        /// <summary>
        /// True cuando el overlay opera en modo "Descargar paquete en el PC":
        /// el destino no es una microSD sino una carpeta local elegida por el usuario.
        /// </summary>
        private bool _modoDescargaLocal = false;
        private string? _rutaDescargaLocal;

        /// <summary>Mapea cada tamaño de preset emuMMC a su RadioButton correspondiente.</summary>
        private RadioButton? ObtenerPresetButton(int gb) => gb switch
        {
            12 => BtnPreset12,
            24 => BtnPreset24,
            32 => BtnPreset32,
            64 => BtnPreset64,
            _  => null
        };

        // ?? Apertura / cierre ????????????????????????????????????????????????

        /// <summary>
        /// Abre el overlay Asistido Completo.
        /// </summary>
        /// <param name="soloInstalar">Modo fijo: true = Solo Instalar (Actualizacion), false = Instalacion completa. Ya no es intercambiable dentro del overlay: cada punto de entrada del hub CFW abre un modo permanente.</param>
        /// <param name="mostrarSelectorModo">Reservado por compatibilidad; ya no controla ningun selector visual (eliminado). Solo afecta la visibilidad del boton VOLVER.</param>
        public void AbrirOverlayAsistidoCompleto(bool soloInstalar = false, bool mostrarSelectorModo = true)
        {
            _sdSelAsistido = InfoSD.ComboDrives.SelectedItem as SDInfo;
            if (_sdSelAsistido == null || _sdSelAsistido.DiscoFisico < 0)
            {
                Dialogos.Advertencia(
                    "No se detecto ninguna microSD conectada. Conecta una microSD e intentalo de nuevo.",
                    "Sin microSD");
                return;
            }

            _asistidoEnProceso = false;
            _modoSoloInstalar = soloInstalar;
            _modoDescargaLocal = false;
            _rutaDescargaLocal = null;
            _selectorModoAsistidoVisible = mostrarSelectorModo;
            TxtEtiquetaAsistido.Text = ConfiguracionLocal.EtiquetaSwitchSd;

            CargarRecomendadosAsistido();
            ActualizarInfoSDAsistido();
            ActualizarPresetEmuMMC(_gbEmuMMCAsistido);
            AplicarModoAsistido();

            BtnVolverAsistidoCompleto.Visibility = _selectorModoAsistidoVisible
                ? Visibility.Collapsed
                : Visibility.Visible;

            TxtTituloAsistidoCompleto.Text = soloInstalar ? "ACTUALIZACION" : "INSTALACION";

            MostrarOverlayConAnimacion(PanelAsistidoCompletoOverlay);
        }

        /// <summary>
        /// Abre el overlay reutilizando la ventana de ACTUALIZACION, pero en modo
        /// "Descargar paquete en el PC": el destino es una carpeta local elegida
        /// por el usuario en vez de una microSD. No formatea ni particiona nada.
        /// </summary>
        public void AbrirOverlayDescargaLocalPc()
        {
            _asistidoEnProceso = false;
            _modoSoloInstalar = true;
            _modoDescargaLocal = true;
            _rutaDescargaLocal = null;
            _selectorModoAsistidoVisible = false;

            CargarRecomendadosAsistido();
            ActualizarInfoSDAsistido();
            AplicarModoAsistido();

            BtnVolverAsistidoCompleto.Visibility = Visibility.Visible;
            TxtTituloAsistidoCompleto.Text = "DESCARGAR PAQUETE EN EL PC";

            MostrarOverlayConAnimacion(PanelAsistidoCompletoOverlay);
        }

        private void BtnVolverAsistidoCompleto_Click(object sender, RoutedEventArgs e)
        {
            if (_asistidoEnProceso) return;
            CerrarOverlayAsistidoCompleto();
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
            if (_modoDescargaLocal)
            {
                ActualizarInfoRutaDescargaLocal();
                return;
            }

            if (_sdSelAsistido == null || _sdSelAsistido.DiscoFisico < 0)
            {
                TxtLetraSDAsistido.Text  = "�";
                TxtNombreSDAsistido.Text = "Sin SD seleccionada";
                TxtInfoSDAsistido.Text   = "Selecciona una SD en el panel derecho";
                TxtAvisoSinSDAsistido.Text = "Selecciona una microSD en el panel derecho para continuar.";
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
                if (_gbEmuMMCAsistido != rec)
                    ActualizarPresetEmuMMC(rec);
            }
        }

        // ?? Modo Descargar paquete en el PC ??????????????????????????????????

        /// <summary>
        /// Pinta la tarjeta de "destino" y el aviso cuando el overlay opera en
        /// modo Descargar paquete en el PC (sin microSD).
        /// </summary>
        private void ActualizarInfoRutaDescargaLocal()
        {
            if (string.IsNullOrEmpty(_rutaDescargaLocal))
            {
                TxtLetraSDAsistido.Text  = "\uD83D\uDCC1";
                TxtNombreSDAsistido.Text = "Ruta de descarga";
                TxtInfoSDAsistido.Text   = "Selecciona una carpeta de destino en tu PC";
                TxtAvisoSinSDAsistido.Text = "Selecciona una carpeta de destino para continuar.";
                AvisoSinSDAsistido.Visibility = Visibility.Visible;
                BtnIniciarAsistido.IsEnabled = false;
                TxtEstadoAsistido.Text = "Elige una carpeta para continuar";
                return;
            }

            TxtLetraSDAsistido.Text  = "\uD83D\uDCC1";
            TxtNombreSDAsistido.Text = "Ruta de descarga";
            TxtInfoSDAsistido.Text   = _rutaDescargaLocal;

            AvisoSinSDAsistido.Visibility = Visibility.Collapsed;
            BtnIniciarAsistido.IsEnabled = _recomendadosAsistido.Count > 0;
            TxtEstadoAsistido.Text = "Mant�n pulsado DESCARGAR PAQUETE para confirmar";
        }

        /// <summary>
        /// Nombre de version compatible actual (publicado en el Gist), usado
        /// como nombre de la subcarpeta de destino para no mezclar el paquete
        /// descargado con el resto de archivos de la carpeta elegida por el
        /// usuario. Si el Gist aun no se ha sincronizado, cae a "22.5.0".
        /// </summary>
        private static string ObtenerVersionCarpetaDescarga()
        {
            string? version = ConfiguracionRemota.Ui?.VersionCompatible;
            return string.IsNullOrWhiteSpace(version) ? "22.5.0" : version;
        }

        /// <summary>
        /// Construye la ruta final de destino anidando una subcarpeta nombrada
        /// segun la version compatible (ej. "NX-Suite_22.5.0") dentro de la
        /// carpeta base elegida por el usuario. Evita doble anidado si la
        /// carpeta seleccionada ya es la propia subcarpeta versionada.
        /// </summary>
        private static string ConstruirRutaDescargaVersionada(string carpetaBase)
        {
            string nombreVersion = $"ATMOS-{ObtenerVersionCarpetaDescarga()}";
            string nombreCarpetaBase = Path.GetFileName(
                carpetaBase.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (string.Equals(nombreCarpetaBase, nombreVersion, StringComparison.OrdinalIgnoreCase))
                return carpetaBase;

            return Path.Combine(carpetaBase, nombreVersion);
        }

        /// <summary>
        /// Determina la carpeta base por defecto a proponer al elegir el destino
        /// de descarga (normalmente la carpeta de Descargas del usuario).
        /// </summary>
        private string ObtenerCarpetaDescargaPorDefecto()
        {
            string carpetaDescargas = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            return Directory.Exists(carpetaDescargas)
                ? carpetaDescargas
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        private void TarjetaSDAsistido_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_modoDescargaLocal || _asistidoEnProceso) return;

            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title           = "Selecciona la carpeta de destino",
                InitialDirectory = _rutaDescargaLocal ?? ObtenerCarpetaDescargaPorDefecto()
            };

            if (dlg.ShowDialog() == true)
            {
                // Se anida automaticamente en una subcarpeta nombrada segun la
                // version compatible (ej. "NX-Suite_22.5.0") para que el paquete
                // descargado no se mezcle con el resto de archivos de la carpeta
                // elegida por el usuario.
                _rutaDescargaLocal = ConstruirRutaDescargaVersionada(dlg.FolderName);
                ActualizarInfoRutaDescargaLocal();
            }
        }

        // ?? Selector de modo ?????????????????????????????????????????????????
        // El toggle interactivo Completo/Solo-Instalar fue eliminado: cada
        // punto de entrada del hub CFW (Instalacion / Actualizacion) abre el
        // overlay ya en el modo correspondiente y permanente. AplicarModoAsistido
        // sigue existiendo para aplicar la visibilidad/estilos derivados de
        // _modoSoloInstalar en el resto del overlay (slider, warning, etc.).

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
            ScrollModulosAsistido.Height = _modoSoloInstalar ? 380 : 190;

            // Texto del bot�n
            TxtBtnIniciarAsistido.Text = _modoDescargaLocal
                ? "DESCARGAR PAQUETE"
                : _modoSoloInstalar
                    ? "INSTALAR M�DULOS"
                    : "INICIAR PROCESO COMPLETO";
        }

        // ?? Presets emuMMC ???????????????????????????????????????????????????

        private void PresetEmuMMC_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tagStr && int.TryParse(tagStr, out int gb))
                _gbEmuMMCAsistido = gb;

            TxtGbValorAsistido.Text = $"{_gbEmuMMCAsistido} GB";
        }

        /// <summary>Marca el RadioButton correspondiente al tamaño emuMMC indicado.</summary>
        private void ActualizarPresetEmuMMC(int gb)
        {
            _gbEmuMMCAsistido = gb;
            TxtGbValorAsistido.Text = $"{gb} GB";

            var btn = ObtenerPresetButton(gb);
            if (btn != null) btn.IsChecked = true;
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
            if (_modoDescargaLocal)
            {
                BtnIniciarAsistido_ClickDescargaLocal();
                return;
            }

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

        /// <summary>
        /// Variante del flujo de instalaci�n para el modo "Descargar paquete en el PC":
        /// el destino es la carpeta local elegida por el usuario en vez de una letra
        /// de microSD. Reutiliza <see cref="ProcesarCompletoArgs"/> pasando la ruta de
        /// carpeta como si fuera la ra�z del destino (v�lido porque el pipeline solo
        /// hace Path.Combine con rutas relativas).
        /// </summary>
        private void BtnIniciarAsistido_ClickDescargaLocal()
        {
            if (string.IsNullOrEmpty(_rutaDescargaLocal))
            {
                ActualizarInfoSDAsistido();
                return;
            }

            if (_recomendadosAsistido.Count == 0)
            {
                TxtEstadoAsistido.Text = "No hay m�dulos recomendados para descargar";
                return;
            }

            try
            {
                if (!Directory.Exists(_rutaDescargaLocal))
                    Directory.CreateDirectory(_rutaDescargaLocal);
            }
            catch (Exception ex)
            {
                UI.Dialogos.Error($"No se pudo crear la carpeta de destino: {ex.Message}");
                return;
            }

            _asistidoEnProceso = true;

            var modulosPrincipales = _recomendadosAsistido.Select(v => v.Modulo).ToList();
            var modulos = _depsAsistido.Concat(modulosPrincipales).ToList();
            var idsDeps = _depsAsistido.Select(m => m.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var args = new ProcesarCompletoArgs
            {
                GbEmuMMC        = _gbEmuMMCAsistido,
                LetraSD         = _rutaDescargaLocal,
                Etiqueta        = string.Empty,
                NumeroDisco     = -1,
                Modulos         = modulos,
                IdsDependencias = idsDeps,
                SoloInstalar    = true,
                EsDescargaLocal = true,
                Logger          = null
            };

            AplicarBlurFondo(false);
            PanelAsistidoCompletoOverlay.Visibility = Visibility.Collapsed;

            VistaAsistida_ProcesarCompletoSolicitado(this, args);

            _asistidoEnProceso = false;
        }
    }
}
