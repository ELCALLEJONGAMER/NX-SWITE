using NX_Suite.Core;
using NX_Suite.Core.Configuracion;
using NX_Suite.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NX_Suite.UI.Controles
{
    // ??????????????????????????????????????????????????????????????????????
    //  Tipos auxiliares (VMs, EventArgs, TemplateSelectors, Placeholders) ?
    //  extraídos a la subcarpeta UI/Controles/VistaAsistida/.
    //  Este archivo contiene únicamente la code-behind del UserControl.
    // ??????????????????????????????????????????????????????????????????????

    public partial class VistaAsistida : UserControl
    {
        // ?? Pilares ??????????????????????????????????????????????

        private record PilarConfig(
            int    Indice,
            string Titulo,
            string Descripcion,
            string ColorNeon,
            bool   EsObligatorio,
            IReadOnlyList<string> Etiquetas);

        private static readonly IReadOnlyList<PilarConfig> _pilares = new PilarConfig[]
        {
            new(0, "Bootloader", "Controla el arranque de tu consola. Necesario para ejecutar el Custom Firmware.",                "#A855F7", true,  new[]{"bootloader","hekate"}),
            new(1, "CFW",        "Custom Firmware. El nucleo del hackeo. Sin esto, nada de lo demas funciona.",                    "#00D2FF", true,  new[]{"cfw","atmosphere"}),
            new(2, "Firmware",   "Sistema operativo oficial de la consola. Puedes saltarte este paso si no quieres actualizarlo.", "#FFD700", false, new[]{"firmware"}),
        };

        private static readonly Dictionary<string, string> _nombresComplementos = new(StringComparer.OrdinalIgnoreCase)
        {
            { "payload",        "Payloads"          },
            { "payloads",       "Payloads"          },
            { "sigpatches",     "Sigpatches"        },
            { "homebrew",       "Homebrew Apps"     },
            { "theme",          "Temas"             },
            { "themes",         "Temas"             },
            { "hekatetheme",    "Temas Hekate"      },
            { "hekatethemes",   "Temas Hekate"      },
            { "emulador",       "Emuladores"        },
            { "emuladores",     "Emuladores"        },
            { "emulator",       "Emuladores"        },
            { "app",            "Aplicaciones"      },
            { "apps",           "Aplicaciones"      },
            { "cheats",         "Trucos"            },
            { "cheat",          "Trucos"            },
            { "config",         "Configuración"     },
            { "visual",         "Personalización"   },
            { "overlay",        "Overlays"          },
            { "overlays",       "Overlays"          },
            { "plugin",         "Plugins"           },
            { "plugins",        "Plugins"           },
            { "sysmodule",      "Módulos de Sistema" },
            { "sysmodules",     "Módulos de Sistema" },
            { "language",       "Idiomas"           },
            { "languages",      "Idiomas"           },
            { "firmware",       "Firmware"          },
            { "bootloader",     "Bootloader"        },
            { "cfw",            "Custom Firmware"   },
            { "atmosphere",     "Atmosphere"        },
            { "hekate",         "Hekate"            },
            { "tessuite",       "Tesla Suite"       },
            { "emuiibo",        "EmuIIbo"           },
            { "nxdumptool",     "NX Dump Tool"      },
            { "tinfoil",        "Tinfoil"           },
            { "goldleaf",       "Goldleaf"          },
        };

        private static string NombreAmigable(string etiqueta)
        {
            if (_nombresComplementos.TryGetValue(etiqueta, out var n)) return n;
            // Fallback: capitalize words, replace separators
            return System.Globalization.CultureInfo.InvariantCulture.TextInfo
                .ToTitleCase(etiqueta.Replace("_", " ").Replace("-", " "));
        }

        // ?? Estado ???????????????????????????????????????????????

        // Paso logico: 0=Pilar0, 1=Comp0, 2=Pilar1, 3=Comp1, 4=Pilar2, 5=Resumen
        private int _pasoLogico = 0;

        private readonly Dictionary<int, ModuloConfig?> _seleccionesPilar         = new();
        private readonly Dictionary<int, List<SubcategoriaVM>> _complementosPorPilar = new();

        private SubcategoriaVM?    _subcategoriaEnEdicion;
        private int                _pilarEnEdicionIdx    = -1;
        private List<ModuloConfig> _todosModulos             = new();
        private List<ModuloConfig> _modulosFiltradosSelector = new();

        private readonly ObservableCollection<ItemCheckoutVM> _itemsCheckout             = new();
        private readonly HashSet<ModuloConfig>                _recomendadosSeleccionados = new();
        private readonly Dictionary<string, string>          _imagenesPendientes         = new();

        // Secciones agrupadas del panel Hekate
        private readonly ObservableCollection<HekateSeccionVM> _seccionesHekate = new();
        private HekateSeccionVM?           _seccionMultiEnEdicion;
        private SubcategoriaVM?            _subcategoriaMultiEnEdicion;
        private List<ItemMultiSeleccionVM> _itemsMultiSelector          = new();
        private List<ItemMultiSeleccionVM> _itemsMultiSelectorFiltrados = new();

        // Fix 4: 3 tarjetas por pagina, sin tarjetas cortadas
        private const int ElementosPorPagina         = 3;
        private const int ElementosPorPaginaSelector = 6;
        private int _paginaActual = 0;
        private List<ModuloConfig> _modulosPasoCompleto = new();
        private int _totalPaginas = 1;

        // Paginacion del selector de complementos
        private int _paginaSelector = 0;
        private int _totalPaginasSelector = 1;
        private List<ModuloConfig> _modulosSelectorBase = new();


        // Stepper (4 circulos: Boot, CFW, FW, Resumen)
        private Border[]?    _circulosPaso;
        private TextBlock[]? _textosPaso;
        private Rectangle[]? _lineasPaso;

        // ?? Modo Asistido Completo ????????????????????????????????????????
        // La lógica de chips/GB vive en VentanaAsistidoCompleto

        public event EventHandler<SesionAsistida>? InstalacionSolicitada;
        public event EventHandler<ModuloConfig>?   DetalleModuloSolicitado;
        public event EventHandler<ProcesarCompletoArgs>? ProcesarCompletoSolicitado;

        public VistaAsistida()
        {
            InitializeComponent();

            _circulosPaso = new[] { CirPaso0, CirPaso1, CirPaso2, CirPaso3 };
            _textosPaso   = new[] { TxtPaso0, TxtPaso1, TxtPaso2, TxtPaso3 };
            _lineasPaso   = new[] { LineaPaso0, LineaPaso1, LineaPaso2 };

            CheckoutLista.ItemsSource = _itemsCheckout;

            SuscribirHoverSonido(ListaModulosPaso);
            SuscribirHoverSonido(ListaModulosSelector);
            SuscribirHoverSonido(ListaMultiSelector);
        }

        // ?? Modo Asistido Completo: handlers ?????????????????????????????

        private void ModoCompleto_Click(object sender, MouseButtonEventArgs e)
        {
            // Abre la ventana independiente del Asistido Completo
            var ventana = new NX_Suite.UI.VentanaAsistidoCompleto(_todosModulos)
            {
                Owner = Window.GetWindow(this)
            };
            ventana.ProcesarSolicitado += (s, args) => ProcesarCompletoSolicitado?.Invoke(this, args);
            ventana.ShowDialog();
        }

        private void ModoParcial_Click(object sender, MouseButtonEventArgs e)
        {
            // Oculta el overlay y muestra el flujo paso a paso existente
            if (FindName("OverlaySelectorModo") is UIElement overlay)
                overlay.Visibility = Visibility.Collapsed;
            // Muestra el boton de volver al selector
            if (FindName("BtnVolverAlSelector") is UIElement btnVolver)
                btnVolver.Visibility = Visibility.Visible;
        }

        private void BtnVolverAlSelector_Click(object sender, RoutedEventArgs e)
        {
            // Vuelve a mostrar el overlay de seleccion de modo
            if (FindName("OverlaySelectorModo") is UIElement overlay)
                overlay.Visibility = Visibility.Visible;
            if (FindName("BtnVolverAlSelector") is UIElement btnVolver)
                btnVolver.Visibility = Visibility.Collapsed;
        }

        // ????????????????????????????????????????????????????????
        //  API publica
        // ????????????????????????????????????????????????????????

        public void Cargar(List<NodoDiagramaConfig> nodos, List<ModuloConfig> modulos, string modoAsistente)
        {
            _todosModulos = modulos ?? new List<ModuloConfig>();
            _seleccionesPilar.Clear();
            _complementosPorPilar.Clear();
            _itemsCheckout.Clear();
            _recomendadosSeleccionados.Clear();
            _imagenesPendientes.Clear();
            foreach (var s in _slotsImagenHekate) s.Preview = null;

            for (int i = 0; i < _pilares.Count; i++)
            {
                var pilar     = _pilares[i];
                var instalado = _todosModulos
                    .Where(m => pilar.Etiquetas.Any(e => CoincideEtiqueta(m, e)))
                    .FirstOrDefault(m => m.EstadoSd == EstadoSdModulo.Instalado ||
                                        m.EstadoSd == EstadoSdModulo.ParcialmenteInstalado);
                if (instalado != null)
                    SeleccionarPilar(i, instalado, silencioso: true);
            }

            IrAPasoLogico(0);
        }

        // ????????????????????????????????????????????????????????
        //  Navegacion central
        // ????????????????????????????????????????????????????????

        private int  IndicePilarDesdePasoLogico(int p) => p / 2;
        private bool EsPasoComplemento(int p)          => p % 2 == 1;
        private int  PasoLogicoResumen                 => _pilares.Count * 2 - 1; // 5

        private int IndiceStepperDesdePasoLogico(int pasoLogico)
        {
            if (pasoLogico >= PasoLogicoResumen) return _pilares.Count;
            return IndicePilarDesdePasoLogico(pasoLogico);
        }

        private void IrAPasoLogico(int pasoLogico)
        {
            _pasoLogico = pasoLogico;
            Servicios.Sonidos.Reproducir(EventoSonido.Navegacion);

            if (pasoLogico >= PasoLogicoResumen)
            {
                MostrarResumen();
            }
            else if (EsPasoComplemento(pasoLogico))
            {
                int iPilar   = IndicePilarDesdePasoLogico(pasoLogico);
                var pilarSel = _seleccionesPilar.GetValueOrDefault(iPilar);
                if (pilarSel == null) { IrAPasoLogico(pasoLogico + 1); return; }

                if (EsHekate(pilarSel))
                    MostrarPersonalizacionHekate(iPilar, pilarSel);
                else if (pilarSel.Complementos.Count == 0)
                    IrAPasoLogico(pasoLogico + 1);
                else
                    MostrarComplementos(iPilar, pilarSel);
            }
            else
            {
                MostrarPilar(_pilares[IndicePilarDesdePasoLogico(pasoLogico)]);
            }

            ActualizarIndicadorPasos();
        }

        private void AvanzarDesdePasoActual()    => IrAPasoLogico(_pasoLogico + 1);

        private void RetrocederDesdePasoActual()
        {
            if (_pasoLogico <= 0) return;
            int anterior = _pasoLogico - 1;
            if (EsPasoComplemento(anterior))
            {
                int iPilar = IndicePilarDesdePasoLogico(anterior);
                var pilar  = _seleccionesPilar.GetValueOrDefault(iPilar);
                if (pilar == null || pilar.Complementos.Count == 0) anterior--;
            }
            IrAPasoLogico(Math.Max(0, anterior));
        }

        private void MostrarVista(UIElement vistaActiva)
        {
            GridPaso.Visibility                  = GridPaso                  == vistaActiva ? Visibility.Visible : Visibility.Collapsed;
            GridComplementos.Visibility          = GridComplementos          == vistaActiva ? Visibility.Visible : Visibility.Collapsed;
            GridPersonalizacionHekate.Visibility = GridPersonalizacionHekate == vistaActiva ? Visibility.Visible : Visibility.Collapsed;
            GridSelector.Visibility              = GridSelector              == vistaActiva ? Visibility.Visible : Visibility.Collapsed;
            GridEditorImagen.Visibility          = GridEditorImagen          == vistaActiva ? Visibility.Visible : Visibility.Collapsed;
            GridResumen.Visibility               = GridResumen               == vistaActiva ? Visibility.Visible : Visibility.Collapsed;
            // El overlay multi-selector se cierra siempre al cambiar de vista principal
            GridSelectorMultiHekate.Visibility   = Visibility.Collapsed;
        }

        // ????????????????????????????????????????????????????????
        //  Fix 1: Stepper visual — checkmark con fuente compatible
        // ????????????????????????????????????????????????????????

        private void ActualizarIndicadorPasos()
        {
            if (_circulosPaso == null) return;

            int stepperActual = IndiceStepperDesdePasoLogico(_pasoLogico);
            int total = _pilares.Count + 1; // 4 circulos

            for (int i = 0; i < total; i++)
            {
                var circulo  = _circulosPaso[i];
                var texto    = _textosPaso![i];

                string hex   = i < _pilares.Count ? _pilares[i].ColorNeon : "#40C057";
                var    color = (Color)ColorConverter.ConvertFromString(hex);
                bool actual  = i == stepperActual;
                bool pasado  = i < stepperActual;
                bool subpaso = actual && EsPasoComplemento(_pasoLogico);
                var  num     = circulo.Child as TextBlock;

                // Fuente con glifo de marca de verificacion garantizado
                var fuenteCheck = new FontFamily("Segoe UI Symbol, Segoe UI");

                if (pasado)
                {
                    circulo.Background  = new SolidColorBrush(color);
                    circulo.BorderBrush = new SolidColorBrush(color);
                    if (num != null)
                    {
                        num.Text       = "\u2713";
                        num.Foreground = Brushes.Black;
                        num.FontFamily = fuenteCheck;
                    }
                    texto.Foreground = Brushes.White;
                    texto.Opacity    = 1;
                }
                else if (actual)
                {
                    circulo.Background  = new SolidColorBrush(
                        Color.FromArgb((byte)(subpaso ? 0x44 : 0x22), color.R, color.G, color.B));
                    circulo.BorderBrush = new SolidColorBrush(color);
                    if (num != null)
                    {
                        num.Text       = i == _pilares.Count ? "\u2713" : (i + 1).ToString();
                        num.Foreground = new SolidColorBrush(color);
                        num.FontFamily = fuenteCheck;
                    }
                    texto.Foreground = new SolidColorBrush(color);
                    texto.Opacity    = 1;
                }
                else
                {
                    circulo.Background  = Brushes.Transparent;
                    circulo.BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x70, 0x70, 0x80));
                    if (num != null)
                    {
                        num.Text       = i == _pilares.Count ? "\u2713" : (i + 1).ToString();
                        num.Foreground = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
                        num.FontFamily = fuenteCheck;
                    }
                    texto.Foreground = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
                    texto.Opacity    = 1;
                }

                if (i < _lineasPaso!.Length)
                    _lineasPaso[i].Fill = pasado
                        ? new SolidColorBrush(color)
                        : new SolidColorBrush(Color.FromArgb(0x20, 0x70, 0x70, 0x80));
            }
        }

        // ????????????????????????????????????????????????????????
        //  VISTA PILAR
        // ????????????????????????????????????????????????????????

        private void MostrarPilar(PilarConfig pilar)
        {
            TxtTituloPaso.Text       = pilar.Titulo;
            TxtTituloPaso.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(pilar.ColorNeon)!;
            TxtDescripcionPaso.Text  = pilar.Descripcion;

            BadgeObligatorio.Visibility = pilar.EsObligatorio ? Visibility.Visible  : Visibility.Collapsed;
            BadgeOpcional.Visibility    = pilar.EsObligatorio ? Visibility.Collapsed : Visibility.Visible;

            _modulosPasoCompleto = FiltrarPorEtiquetas(pilar.Etiquetas);
            _paginaActual = 0;
            _totalPaginas = Math.Max(1, (int)Math.Ceiling(_modulosPasoCompleto.Count / (double)ElementosPorPagina));
            MostrarPaginaActual();

            BtnVolverPaso.Visibility        = _pasoLogico > 0 ? Visibility.Visible   : Visibility.Collapsed;
            BtnVolverAlSelector.Visibility  = _pasoLogico == 0 ? Visibility.Visible  : Visibility.Collapsed;
            ActualizarBotonSiguientePilar();

            MostrarVista(GridPaso);
        }

        private void MostrarPaginaActual()
        {
            var pagina = _modulosPasoCompleto
                .Skip(_paginaActual * ElementosPorPagina)
                .Take(ElementosPorPagina)
                .ToList();

            ListaModulosPaso.ItemsSource = pagina;

            BtnPaginaAnterior.Visibility  = _paginaActual > 0              ? Visibility.Visible : Visibility.Collapsed;
            BtnPaginaSiguiente.Visibility = _paginaActual < _totalPaginas-1 ? Visibility.Visible : Visibility.Collapsed;

            ActualizarIndicadorPaginas();
            ActualizarSeleccionVisual();
        }

        private void ActualizarIndicadorPaginas()
        {
            PanelIndicadorPaginas.Children.Clear();

            if (_totalPaginas <= 1) { PanelIndicadorPaginas.Visibility = Visibility.Collapsed; return; }
            PanelIndicadorPaginas.Visibility = Visibility.Visible;

            int    iPilar   = IndicePilarDesdePasoLogico(_pasoLogico);
            string colorHex = iPilar < _pilares.Count ? _pilares[iPilar].ColorNeon : "#40C057";

            for (int i = 0; i < _totalPaginas; i++)
            {
                bool esActual = i == _paginaActual;
                PanelIndicadorPaginas.Children.Add(new Ellipse
                {
                    Width             = esActual ? 10 : 7,
                    Height            = esActual ? 10 : 7,
                    Fill              = esActual
                        ? (SolidColorBrush)new BrushConverter().ConvertFrom(colorHex)!
                        : new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                    Margin            = new Thickness(4, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }
        }

        private void BtnPaginaAnterior_Click(object sender, RoutedEventArgs e)
        {
            if (_paginaActual > 0)
            { _paginaActual--; Servicios.Sonidos.Reproducir(EventoSonido.Navegacion); MostrarPaginaActual(); }
        }

        private void BtnPaginaSiguiente_Click(object sender, RoutedEventArgs e)
        {
            if (_paginaActual < _totalPaginas - 1)
            { _paginaActual++; Servicios.Sonidos.Reproducir(EventoSonido.Navegacion); MostrarPaginaActual(); }
        }

        private void ActualizarBotonSiguientePilar()
        {
            int  iPilar         = IndicePilarDesdePasoLogico(_pasoLogico);
            bool tieneSeleccion = _seleccionesPilar.TryGetValue(iPilar, out var sel) && sel != null;
            bool obligatorio    = iPilar < _pilares.Count && _pilares[iPilar].EsObligatorio;
            // Para pasos opcionales sin selección, Siguiente también avanza (equivale a saltar)
            BtnSiguientePaso.Visibility = (obligatorio ? tieneSeleccion : true) ? Visibility.Visible : Visibility.Collapsed;
        }

        // Tarjetas: seleccionada con halo neon verde, resto semi-transparentes
        private void ActualizarSeleccionVisual()
        {
            int iPilar       = IndicePilarDesdePasoLogico(_pasoLogico);
            var seleccionado = _seleccionesPilar.GetValueOrDefault(iPilar);

            // Actualizar propiedad en todos los módulos del paso (incluyendo páginas no visibles)
            foreach (var item in _modulosPasoCompleto)
                item.EstaSeleccionadoAsistido = ReferenceEquals(item, seleccionado);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var item in ListaModulosPaso.Items)
                {
                    var cp = ListaModulosPaso.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                    if (cp == null) continue;
                    bool esElegido = ReferenceEquals(item, seleccionado);
                    // Sin selección: todas visibles; con selección: no-elegidas al 55%
                    cp.Opacity = (seleccionado == null || esElegido) ? 1.0 : 0.55;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void PasoModulo_Click(object sender, MouseButtonEventArgs e)
        {
            if (EsClickEnBotonInfo(e.OriginalSource as DependencyObject))
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is ModuloConfig moduloInfo)
                {
                    e.Handled = true;
                    Servicios.Sonidos.Reproducir(EventoSonido.Click);
                    DetalleModuloSolicitado?.Invoke(this, moduloInfo);
                    return;
                }
            }

            if ((e.OriginalSource as FrameworkElement)?.DataContext is not ModuloConfig modulo) return;
            e.Handled = true;

            int iPilar = IndicePilarDesdePasoLogico(_pasoLogico);
            var actual = _seleccionesPilar.GetValueOrDefault(iPilar);

            // Deseleccionar si se pulsa la tarjeta ya elegida
            if (ReferenceEquals(actual, modulo))
            {
                modulo.EstaSeleccionadoAsistido = false;
                _seleccionesPilar[iPilar] = null;
                var itemChk = _itemsCheckout.FirstOrDefault(x => x.Modulo == modulo && !x.EsComplemento);
                if (itemChk != null) _itemsCheckout.Remove(itemChk);
                ActualizarCheckout();
                ActualizarSeleccionVisual();
                ActualizarBotonSiguientePilar();
                Servicios.Sonidos.Reproducir(EventoSonido.Click);
                return;
            }

            SeleccionarPilar(iPilar, modulo);
            ActualizarBotonSiguientePilar();
        }

        private void SeleccionarPilar(int indicePilar, ModuloConfig modulo, bool silencioso = false)
        {
            if (!silencioso) Servicios.Sonidos.Reproducir(EventoSonido.Click);

            var anterior = _seleccionesPilar.GetValueOrDefault(indicePilar);
            _seleccionesPilar[indicePilar] = modulo;

            // Limpiar complementos del pilar anterior si cambio de opcion
            if (anterior != null && anterior != modulo)
            {
                if (_complementosPorPilar.TryGetValue(indicePilar, out var subsAnt))
                    foreach (var sub in subsAnt)
                        foreach (var selMod in sub.Seleccionados.ToList())
                        {
                            var it = _itemsCheckout.FirstOrDefault(x => x.Modulo == selMod);
                            if (it != null) _itemsCheckout.Remove(it);
                        }
                _complementosPorPilar.Remove(indicePilar);

                // Para Hekate: limpiar recomendados e imágenes pendientes
                if (EsHekate(anterior))
                {
                    foreach (var m in _recomendadosSeleccionados.ToList())
                    {
                        var it = _itemsCheckout.FirstOrDefault(x => x.Modulo == m && x.EsComplemento);
                        if (it != null) _itemsCheckout.Remove(it);
                    }
                    _recomendadosSeleccionados.Clear();
                    _imagenesPendientes.Clear();
                    foreach (var s in _slotsImagenHekate) s.Preview = null;
                }
            }

            // Preparar subcategorias de complementos (solo pilares no-Hekate)
            if (!_complementosPorPilar.ContainsKey(indicePilar) && modulo.Complementos.Count > 0 && !EsHekate(modulo))
                _complementosPorPilar[indicePilar] = modulo.Complementos
                    .Select(etiq => new SubcategoriaVM(
                        etiq,
                        NombreAmigable(etiq),
                        permiteMultiseleccion: true))
                    .Where(s => FiltrarPorEtiqueta(s.Etiqueta).Any())
                    .ToList();

            // Reemplazar pilar en checkout
            if (anterior != null)
            {
                var itemAnt = _itemsCheckout.FirstOrDefault(x => x.Modulo == anterior && !x.EsComplemento);
                if (itemAnt != null) _itemsCheckout.Remove(itemAnt);
            }

            int insertAt = _itemsCheckout.Count(x =>
                _pilares.Any(p => p.Titulo == x.PasoTitulo && p.Indice < indicePilar) && !x.EsComplemento);

            var nuevo = new ItemCheckoutVM
            {
                Modulo        = modulo,
                PasoTitulo    = _pilares[indicePilar].Titulo,
                ColorNeon     = _pilares[indicePilar].ColorNeon,
                EsComplemento = false,
            };

            if (insertAt >= _itemsCheckout.Count) _itemsCheckout.Add(nuevo);
            else _itemsCheckout.Insert(insertAt, nuevo);

            ActualizarCheckout();
            ActualizarSeleccionVisual();
        }

        private void BtnVolverPaso_Click(object sender, RoutedEventArgs e)    => RetrocederDesdePasoActual();
        private void BtnSiguientePaso_Click(object sender, RoutedEventArgs e) => AvanzarDesdePasoActual();

        // ????????????????????????????????????????????????????????
        //  VISTA COMPLEMENTOS
        // ????????????????????????????????????????????????????????

        private void MostrarComplementos(int indicePilar, ModuloConfig pilar)
        {
            _pilarEnEdicionIdx = indicePilar;

            TxtTituloComplementos.Text       = $"Complementos de {pilar.Nombre}";
            TxtTituloComplementos.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(_pilares[indicePilar].ColorNeon)!;
            TxtDescComplementos.Text         = $"Personaliza tu {_pilares[indicePilar].Titulo} con extras opcionales.";

            try
            {
                string? ruta = Core.Servicios.Iconos.ObtenerRutaLocal(pilar.IconoUrl);
                ImgPadreComplemento.Source = new BitmapImage(new Uri(ruta ?? pilar.IconoUrl));
            }
            catch { ImgPadreComplemento.Source = null; }

            ListaSubcategorias.ItemsSource = _complementosPorPilar.GetValueOrDefault(indicePilar) ?? new();
            MostrarVista(GridComplementos);
        }

        private void ComplementoSlotClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button { Tag: SlotVacioPlaceholder placeholder }) return;
            e.Handled = true;
            Servicios.Sonidos.Reproducir(EventoSonido.Click);
            AbrirSelectorComplemento(placeholder.Subcategoria);
        }

        // ????????????????????????????????????????????????????????
        //  EDITOR DE IMAGEN (conversor_imagen)
        // ????????????????????????????????????????????????????????

        private record EspecsImagen(string Titulo, int Ancho, int Alto, string NombreArchivo, string RutaSD, int Bits = 24);

        private static readonly Dictionary<string, EspecsImagen> _especsPorEtiqueta =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "bootlogo",     new("Boot Logo",          720,  1280, "bootlogo.bmp",    "/bootloader/bootlogo.bmp",         24) },
                { "background",   new("Fondo de pantalla",  1280, 720,  "background.bmp",  "/bootloader/res/background.bmp",   32) },
                { "icon_emummc",  new("Icono EmuMMC",       256,  256,  "emummc.bmp",      "/bootloader/res/emummc.bmp",       24) },
                { "icon_stock",   new("Icono Stock",        256,  256,  "stock.bmp",       "/bootloader/res/stock.bmp",        24) },
                { "icon_sysnand", new("Icono SysNand",      256,  256,  "sysnand.bmp",     "/bootloader/res/sysnand.bmp",      24) },
            };

        private readonly List<ImagenSlotVM> _slotsImagenHekate = new()
        {
            new() { Etiqueta = "bootlogo",     Titulo = "Boot Logo",          Resolucion = "720 × 1280 px · vertical" },
            new() { Etiqueta = "background",   Titulo = "Fondo de pantalla",  Resolucion = "1280 × 720 px"            },
            new() { Etiqueta = "icon_emummc",  Titulo = "Icono EmuMMC",       Resolucion = "256 × 256 px"             },
            new() { Etiqueta = "icon_stock",   Titulo = "Icono Stock",        Resolucion = "256 × 256 px"             },
            new() { Etiqueta = "icon_sysnand", Titulo = "Icono SysNand",      Resolucion = "256 × 256 px"             },
        };

        private string?       _etiquetaEditorActual;
        private EspecsImagen? _especsEditorActual;
        private string?       _rutaImagenSeleccionada;

        private void AbrirEditorImagen(ImagenSlotVM slot)
        {
            _etiquetaEditorActual      = slot.Etiqueta;
            _rutaImagenSeleccionada    = null;
            ImgPreviewEditor.Source    = null;
            BtnAplicarImagen.IsEnabled = false;

            _especsPorEtiqueta.TryGetValue(slot.Etiqueta, out _especsEditorActual);

            TxtTituloEditorImagen.Text = $"Personalizar {slot.Titulo}";
            TxtDescEditorImagen.Text   = "La imagen será convertida automáticamente al formato requerido por Hekate.";
            TxtEspecsImagen.Text       = _especsEditorActual != null
                ? $"{_especsEditorActual.Ancho} × {_especsEditorActual.Alto} px · BMP 24-bit · {_especsEditorActual.NombreArchivo}"
                : string.Empty;

            // Si ya hay imagen pendiente para este slot, precargarla
            if (_imagenesPendientes.TryGetValue(slot.Etiqueta, out var rutaAnterior))
                CargarImagenEnEditor(rutaAnterior);

            MostrarVista(GridEditorImagen);
        }

        private void CargarImagenEnEditor(string rutaArchivo)
        {
            try
            {
                _rutaImagenSeleccionada = rutaArchivo;
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.UriSource     = new Uri(rutaArchivo);
                bmp.CacheOption   = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.EndInit();
                ImgPreviewEditor.Source    = bmp;
                BtnAplicarImagen.IsEnabled = true;
                ZonaDropImagen.BorderBrush = (System.Windows.Media.Brush)FindResource("AcentoVerde");
            }
            catch
            {
                ImgPreviewEditor.Source    = null;
                BtnAplicarImagen.IsEnabled = false;
                _rutaImagenSeleccionada    = null;
            }
        }

        private void ZonaDropImagen_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            ZonaDropImagen.BorderBrush = (System.Windows.Media.Brush)FindResource("AcentoCian");
            e.Handled = true;
        }

        private void ZonaDropImagen_DragLeave(object sender, DragEventArgs e)
        {
            ZonaDropImagen.BorderBrush = (System.Windows.Media.Brush)FindResource("BordeGeneral");
        }

        private void ZonaDropImagen_Drop(object sender, DragEventArgs e)
        {
            ZonaDropImagen.BorderBrush = (System.Windows.Media.Brush)FindResource("BordeGeneral");
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var archivos = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (archivos?.Length > 0) CargarImagenEnEditor(archivos[0]);
        }

        private void BtnExplorarImagen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Seleccionar imagen",
                Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|Todos|*.*"
            };
            if (dlg.ShowDialog() == true) CargarImagenEnEditor(dlg.FileName);
        }

        private void BtnAplicarImagen_Click(object sender, RoutedEventArgs e)
        {
            if (_rutaImagenSeleccionada == null || _etiquetaEditorActual == null) return;

            if (_especsEditorActual == null)
            {
                NX_Suite.UI.Dialogos.Advertencia(
                    $"La etiqueta '{_etiquetaEditorActual}' no tiene especificaciones definidas.\nVálidos: bootlogo, background, icon.",
                    "Tipo no reconocido");
                return;
            }

            // Guardar ruta de origen para conversión en el momento de instalar
            _imagenesPendientes[_etiquetaEditorActual] = _rutaImagenSeleccionada;

            // Actualizar preview del slot
            var slot = _slotsImagenHekate.FirstOrDefault(s => s.Etiqueta == _etiquetaEditorActual);
            if (slot != null)
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource   = new Uri(_rutaImagenSeleccionada, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    slot.Preview = bmp;
                }
                catch { slot.Preview = null; }
            }

            // Volver a personalización Hekate
            MostrarVista(GridPersonalizacionHekate);
        }

        private void BtnVolverEditorImagen_Click(object sender, RoutedEventArgs e)
        {
            if (_pilarEnEdicionIdx >= 0 &&
                _seleccionesPilar.TryGetValue(_pilarEnEdicionIdx, out var pilar) && pilar != null)
            {
                if (EsHekate(pilar))
                    MostrarVista(GridPersonalizacionHekate);
                else
                    MostrarComplementos(_pilarEnEdicionIdx, pilar);
            }
            else
                MostrarVista(GridComplementos);
        }

        private static void ConvertirImagenABmp(string rutaOrigen, string rutaDestino, int ancho, int alto, int bits = 24)
            => Core.ImageConverter.ConvertirParaHekate(rutaOrigen, rutaDestino, ancho, alto, bits);

        // ????????????????????????????????????????????????????????
        //  VISTA PERSONALIZACIÓN HEKATE
        // ????????????????????????????????????????????????????????

        private static bool EsHekate(ModuloConfig m)
            => m.Etiquetas?.Any(e => string.Equals(e, "hekate", StringComparison.OrdinalIgnoreCase)) == true;

        private void MostrarPersonalizacionHekate(int indicePilar, ModuloConfig pilar)
        {
            _pilarEnEdicionIdx = indicePilar;

            TxtTituloPersonalizacion.Text = $"Personalización de {pilar.Nombre}";
            TxtDescPersonalizacion.Text   = "Personaliza los complementos y el aspecto visual de Hekate.";

            try
            {
                string? ruta = Core.Servicios.Iconos.ObtenerRutaLocal(pilar.IconoUrl);
                ImgIconoHekate.Source = new BitmapImage(new Uri(ruta ?? pilar.IconoUrl));
            }
            catch { ImgIconoHekate.Source = null; }

            // Construir secciones agrupadas por etiqueta de complemento
            _seccionesHekate.Clear();
            foreach (var etiq in pilar.Complementos)
            {
                var modulos = FiltrarPorEtiqueta(etiq);
                if (!modulos.Any()) continue;

                string nombre = NombreAmigable(etiq);
                string color  = etiq.Contains("theme",   StringComparison.OrdinalIgnoreCase) ||
                                etiq.Contains("visual",  StringComparison.OrdinalIgnoreCase)  ? "#A855F7" :
                                etiq.Contains("payload", StringComparison.OrdinalIgnoreCase)  ? "#00D2FF" :
                                etiq.Contains("cheat",   StringComparison.OrdinalIgnoreCase)  ? "#FFD54A" :
                                "#22C55E";

                var seccion = new HekateSeccionVM { Etiqueta = etiq, Titulo = nombre, ColorNeon = color };

                // Restaurar selecciones previas que coincidan con esta etiqueta
                foreach (var m in _recomendadosSeleccionados.Where(m => CoincideEtiqueta(m, etiq)))
                    seccion.Seleccionados.Add(m);

                _seccionesHekate.Add(seccion);
            }
            ListaSeccionesHekate.ItemsSource = _seccionesHekate;

            // Sincronizar slots de imagen con previews pendientes
            foreach (var slot in _slotsImagenHekate)
            {
                if (!slot.TieneImagen && _imagenesPendientes.TryGetValue(slot.Etiqueta, out var rutaImg))
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource   = new Uri(rutaImg, UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        slot.Preview = bmp;
                    }
                    catch { }
                }
            }
            // ListaImagenesPersonalizadas.ItemsSource = _slotsImagenHekate; // UI removida, logica preservada

            MostrarVista(GridPersonalizacionHekate);
        }

        // ????????????????????????????????????????????????????????????
        //  Multi-selector flotante
        // ????????????????????????????????????????????????????????????

        private void BtnAgregarHekateSeccion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: HekateSeccionVM seccion })
            {
                Servicios.Sonidos.Reproducir(EventoSonido.Click);
                AbrirSelectorMultiHekate(seccion);
            }
        }

        private void BtnAgregarComplementoCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: SubcategoriaVM subVM })
            {
                Servicios.Sonidos.Reproducir(EventoSonido.Click);
                AbrirSelectorMultiComplemento(subVM);
            }
        }

        private void AbrirSelectorMultiComplemento(SubcategoriaVM subVM)
        {
            _subcategoriaMultiEnEdicion = subVM;
            _seccionMultiEnEdicion      = null;
            TxtTituloSelectorMulti.Text = $"Seleccionar {subVM.Nombre}";
            TxtBuscadorMulti.Text       = string.Empty;

            var todos = FiltrarPorEtiqueta(subVM.Etiqueta);

            // Solo mostrar módulos que no estén ya en otra subcategoría (evitar duplicados)
            var disponibles = todos.Where(m =>
                subVM.Seleccionados.Contains(m) ||
                !_itemsCheckout.Any(x => x.Modulo == m && x.EsComplemento)).ToList();

            _itemsMultiSelector = disponibles.Select(m => new ItemMultiSeleccionVM
            {
                Modulo       = m,
                Seleccionado = subVM.Seleccionados.Contains(m)
            }).ToList();
            _itemsMultiSelectorFiltrados   = _itemsMultiSelector;
            ListaMultiSelector.ItemsSource = _itemsMultiSelectorFiltrados;

            GridSelectorMultiHekate.Visibility = Visibility.Visible;
            ActualizarSeleccionVisualMulti();
        }

        private void AbrirSelectorMultiHekate(HekateSeccionVM seccion)
        {
            _seccionMultiEnEdicion      = seccion;
            TxtTituloSelectorMulti.Text = $"Seleccionar {seccion.Titulo}";
            TxtBuscadorMulti.Text       = string.Empty;

            var todos = FiltrarPorEtiqueta(seccion.Etiqueta);

            // Solo mostrar módulos que no estén ya en otra sección (evitar duplicados)
            var disponibles = todos.Where(m =>
                seccion.Seleccionados.Contains(m) ||
                !_itemsCheckout.Any(x => x.Modulo == m && x.EsComplemento)).ToList();

            _itemsMultiSelector = disponibles.Select(m => new ItemMultiSeleccionVM
            {
                Modulo       = m,
                Seleccionado = seccion.Seleccionados.Contains(m)
            }).ToList();
            _itemsMultiSelectorFiltrados   = _itemsMultiSelector;
            ListaMultiSelector.ItemsSource = _itemsMultiSelectorFiltrados;

            GridSelectorMultiHekate.Visibility = Visibility.Visible;
            ActualizarSeleccionVisualMulti();
        }


        private void ItemMultiSelector_Click(object sender, MouseButtonEventArgs e)
        {
            if (EsClickEnBotonInfo(e.OriginalSource as DependencyObject)) return;
            if ((e.OriginalSource as FrameworkElement)?.DataContext is not ItemMultiSeleccionVM item) return;
            e.Handled         = true;
            item.Seleccionado = !item.Seleccionado;
            Servicios.Sonidos.Reproducir(EventoSonido.Click);
            ActualizarSeleccionVisualMulti();
        }

        private void TxtBuscadorMulti_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = TxtBuscadorMulti.Text.Trim();
            _itemsMultiSelectorFiltrados = string.IsNullOrEmpty(filtro)
                ? _itemsMultiSelector
                : _itemsMultiSelector
                    .Where(x => x.Modulo.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            ListaMultiSelector.ItemsSource = _itemsMultiSelectorFiltrados;
            ActualizarSeleccionVisualMulti();
        }

        private void BtnAceptarSelectorMulti_Click(object sender, RoutedEventArgs e)
        {
            if (_seccionMultiEnEdicion != null)
            {
                string titulo = _pilarEnEdicionIdx >= 0 && _pilarEnEdicionIdx < _pilares.Count
                    ? _pilares[_pilarEnEdicionIdx].Titulo    : "Hekate";
                string color  = _pilarEnEdicionIdx >= 0 && _pilarEnEdicionIdx < _pilares.Count
                    ? _pilares[_pilarEnEdicionIdx].ColorNeon : "#A855F7";

                var ahora = _itemsMultiSelector.Where(x => x.Seleccionado).Select(x => x.Modulo).ToHashSet();
                var antes  = _seccionMultiEnEdicion.Seleccionados.ToList();

                foreach (var m in antes.Where(m => !ahora.Contains(m)).ToList())
                {
                    _seccionMultiEnEdicion.Seleccionados.Remove(m);
                    _recomendadosSeleccionados.Remove(m);
                    var it = _itemsCheckout.FirstOrDefault(x => x.Modulo == m && x.EsComplemento);
                    if (it != null) _itemsCheckout.Remove(it);
                }

                foreach (var m in ahora.Where(m => !antes.Contains(m)))
                {
                    _seccionMultiEnEdicion.Seleccionados.Add(m);
                    _recomendadosSeleccionados.Add(m);
                    if (!_itemsCheckout.Any(x => x.Modulo == m))
                        _itemsCheckout.Add(new ItemCheckoutVM
                        {
                            Modulo = m, PasoTitulo = titulo, ColorNeon = color, EsComplemento = true
                        });
                }
            }
            else if (_subcategoriaMultiEnEdicion != null)
            {
                int    iPilar = _pilarEnEdicionIdx >= 0 ? _pilarEnEdicionIdx : 0;
                string titulo = iPilar < _pilares.Count ? _pilares[iPilar].Titulo    : _subcategoriaMultiEnEdicion.Nombre;
                string color  = iPilar < _pilares.Count ? _pilares[iPilar].ColorNeon : "#22C55E";

                var ahora = _itemsMultiSelector.Where(x => x.Seleccionado).Select(x => x.Modulo).ToHashSet();
                var antes  = _subcategoriaMultiEnEdicion.Seleccionados.ToList();

                foreach (var m in antes.Where(m => !ahora.Contains(m)).ToList())
                {
                    _subcategoriaMultiEnEdicion.Seleccionados.Remove(m);
                    var it = _itemsCheckout.FirstOrDefault(x => x.Modulo == m && x.EsComplemento);
                    if (it != null) _itemsCheckout.Remove(it);
                }

                foreach (var m in ahora.Where(m => !antes.Contains(m)))
                {
                    _subcategoriaMultiEnEdicion.Seleccionados.Add(m);
                    if (!_itemsCheckout.Any(x => x.Modulo == m))
                        _itemsCheckout.Add(new ItemCheckoutVM
                        {
                            Modulo = m, PasoTitulo = titulo, ColorNeon = color, EsComplemento = true
                        });
                }
                _subcategoriaMultiEnEdicion = null;
            }

            ActualizarCheckout();
            GridSelectorMultiHekate.Visibility = Visibility.Collapsed;
        }

        private void BtnCancelarSelectorMulti_Click(object sender, RoutedEventArgs e)
            => GridSelectorMultiHekate.Visibility = Visibility.Collapsed;

        private void ActualizarSeleccionVisualMulti()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var item in ListaMultiSelector.Items)
                {
                    var cp = ListaMultiSelector.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                    if (cp != null && item is ItemMultiSeleccionVM vm)
                        cp.Opacity = vm.Seleccionado ? 1.0 : 0.40;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void BtnQuitarDeSeccion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ModuloConfig modulo }) return;

            foreach (var sec in _seccionesHekate)
                sec.Seleccionados.Remove(modulo);

            _recomendadosSeleccionados.Remove(modulo);
            var it = _itemsCheckout.FirstOrDefault(x => x.Modulo == modulo && x.EsComplemento);
            if (it != null) _itemsCheckout.Remove(it);

            ActualizarCheckout();
        }

        private void BtnSlotImagenEditar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: ImagenSlotVM slot }) AbrirEditorImagen(slot);
        }

        private void BtnSlotImagenQuitar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ImagenSlotVM slot }) return;
            _imagenesPendientes.Remove(slot.Etiqueta);
            slot.Preview = null;
        }

        private void BtnVolverHekate_Click(object sender, RoutedEventArgs e)    => RetrocederDesdePasoActual();
        private void BtnSiguienteHekate_Click(object sender, RoutedEventArgs e) => AvanzarDesdePasoActual();

        private void AbrirSelectorComplemento(SubcategoriaVM subVM)
        {
            _subcategoriaEnEdicion    = subVM;
            TxtTituloSelector.Text    = subVM.Nombre;
            TxtSubtituloSelector.Text = "Puedes seleccionar varios";
            TxtBuscador.Text          = string.Empty;

            _modulosSelectorBase = FiltrarPorEtiqueta(subVM.Etiqueta);
            _modulosFiltradosSelector = _modulosSelectorBase;

            _paginaSelector       = 0;
            _totalPaginasSelector = Math.Max(1, (int)Math.Ceiling(_modulosFiltradosSelector.Count / (double)ElementosPorPaginaSelector));
            MostrarPaginaSelector();
            MostrarVista(GridSelector);
        }

        private void MostrarPaginaSelector()
        {
            var pagina = _modulosFiltradosSelector
                .Skip(_paginaSelector * ElementosPorPaginaSelector)
                .Take(ElementosPorPaginaSelector)
                .ToList();

            ListaModulosSelector.ItemsSource = pagina;

            BtnPaginaAnteriorSelector.Visibility  = _paginaSelector > 0                    ? Visibility.Visible : Visibility.Collapsed;
            BtnPaginaSiguienteSelector.Visibility = _paginaSelector < _totalPaginasSelector - 1 ? Visibility.Visible : Visibility.Collapsed;

            ActualizarIndicadorPaginasSelector();
        }

        private void ActualizarIndicadorPaginasSelector()
        {
            PanelIndicadorPaginasSelector.Children.Clear();

            if (_totalPaginasSelector <= 1)
            {
                PanelIndicadorPaginasSelector.Visibility = Visibility.Collapsed;
                return;
            }
            PanelIndicadorPaginasSelector.Visibility = Visibility.Visible;

            for (int i = 0; i < _totalPaginasSelector; i++)
            {
                bool actual = i == _paginaSelector;
                PanelIndicadorPaginasSelector.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width             = actual ? 9 : 6,
                    Height            = actual ? 9 : 6,
                    Fill              = actual
                        ? (System.Windows.Media.Brush)FindResource("AcentoCian")
                        : new System.Windows.Media.SolidColorBrush(
                              System.Windows.Media.Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)),
                    Margin            = new System.Windows.Thickness(5, 0, 5, 0),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                });
            }
        }

        private void BtnPaginaAnteriorSelector_Click(object sender, RoutedEventArgs e)
        {
            if (_paginaSelector > 0)
            { _paginaSelector--; Servicios.Sonidos.Reproducir(EventoSonido.Navegacion); MostrarPaginaSelector(); }
        }

        private void BtnPaginaSiguienteSelector_Click(object sender, RoutedEventArgs e)
        {
            if (_paginaSelector < _totalPaginasSelector - 1)
            { _paginaSelector++; Servicios.Sonidos.Reproducir(EventoSonido.Navegacion); MostrarPaginaSelector(); }
        }

        private void BtnVolverComplementos_Click(object sender, RoutedEventArgs e)    => RetrocederDesdePasoActual();
        private void BtnSiguienteComplementos_Click(object sender, RoutedEventArgs e) => AvanzarDesdePasoActual();

        // ????????????????????????????????????????????????????????
        //  VISTA SELECTOR
        // ????????????????????????????????????????????????????????

        private void SelectorModulo_Click(object sender, MouseButtonEventArgs e)
        {
            if (EsClickEnBotonInfo(e.OriginalSource as DependencyObject))
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is ModuloConfig moduloInfo)
                {
                    e.Handled = true;
                    Servicios.Sonidos.Reproducir(EventoSonido.Click);
                    DetalleModuloSolicitado?.Invoke(this, moduloInfo);
                    return;
                }
            }

            e.Handled = true;
            if ((e.OriginalSource as FrameworkElement)?.DataContext is not ModuloConfig modulo) return;
            if (_subcategoriaEnEdicion == null) return;

            if (!_subcategoriaEnEdicion.Seleccionados.Contains(modulo))
            {
                _subcategoriaEnEdicion.Seleccionados.Add(modulo);

                if (!_itemsCheckout.Any(x => x.Modulo == modulo))
                {
                    int    iPilar = _pilarEnEdicionIdx >= 0 ? _pilarEnEdicionIdx : 0;
                    string titulo = iPilar < _pilares.Count ? _pilares[iPilar].Titulo    : _subcategoriaEnEdicion.Nombre;
                    string color  = iPilar < _pilares.Count ? _pilares[iPilar].ColorNeon : "#22C55E";

                    _itemsCheckout.Add(new ItemCheckoutVM
                    {
                        Modulo        = modulo,
                        PasoTitulo    = titulo,
                        ColorNeon     = color,
                        EsComplemento = true,
                    });
                }
                ActualizarCheckout();
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_pilarEnEdicionIdx >= 0 &&
                    _seleccionesPilar.TryGetValue(_pilarEnEdicionIdx, out var pilar) && pilar != null)
                    MostrarComplementos(_pilarEnEdicionIdx, pilar);
                else
                    MostrarVista(GridComplementos);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void BtnVolverDesdeSelector_Click(object sender, RoutedEventArgs e)
        {
            if (_pilarEnEdicionIdx >= 0 &&
                _seleccionesPilar.TryGetValue(_pilarEnEdicionIdx, out var pilar) && pilar != null)
                MostrarComplementos(_pilarEnEdicionIdx, pilar);
            else
                MostrarVista(GridComplementos);
        }

        private void TxtBuscador_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = TxtBuscador.Text.Trim();
            _modulosFiltradosSelector = string.IsNullOrEmpty(filtro)
                ? _modulosSelectorBase
                : _modulosSelectorBase
                    .Where(m => m.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            _paginaSelector       = 0;
            _totalPaginasSelector = Math.Max(1, (int)Math.Ceiling(_modulosFiltradosSelector.Count / (double)ElementosPorPaginaSelector));
            MostrarPaginaSelector();
        }

        // ????????????????????????????????????????????????????????
        //  VISTA RESUMEN — agrupado por pilar padre
        // ????????????????????????????????????????????????????????

        private void MostrarResumen()
        {
            var grupos = new List<GrupoResumenVM>();
            foreach (var pilar in _pilares)
            {
                var itemPilar = _itemsCheckout.FirstOrDefault(x => x.PasoTitulo == pilar.Titulo && !x.EsComplemento);
                if (itemPilar == null) continue;
                grupos.Add(new GrupoResumenVM
                {
                    Pilar        = itemPilar,
                    Complementos = _itemsCheckout.Where(x => x.PasoTitulo == pilar.Titulo && x.EsComplemento).ToList()
                });
            }
            ListaResumen.ItemsSource = grupos;
            MostrarVista(GridResumen);
        }

        private sealed class GrupoResumenVM
        {
            public ItemCheckoutVM                Pilar        { get; init; } = null!;
            public IReadOnlyList<ItemCheckoutVM> Complementos { get; init; } = Array.Empty<ItemCheckoutVM>();
        }

        private void BtnVolverResumen_Click(object sender, RoutedEventArgs e) => RetrocederDesdePasoActual();

        private void BtnInstalarAsistido_Click(object sender, RoutedEventArgs e)
        {
            var modulos = _itemsCheckout.Select(x => x.Modulo).Distinct().ToList();

            // Convertir imágenes pendientes y añadirlas al pipeline de instalación
            foreach (var (etiqueta, rutaOrigen) in _imagenesPendientes)
            {
                if (!_especsPorEtiqueta.TryGetValue(etiqueta, out var specs)) continue;

                string rutaCache = System.IO.Path.Combine(
                    ConfiguracionLocal.RutaCacheZips, specs.NombreArchivo);

                try { ConvertirImagenABmp(rutaOrigen, rutaCache, specs.Ancho, specs.Alto, specs.Bits); }
                catch { continue; }

                var pipeline = new List<Models.PasoPipeline>
                {
                    new()
                    {
                        Paso       = 1,
                        TipoAccion = "COPIARSD",
                        MensajeUI  = $"Copiando {specs.Titulo}",
                        Parametros = System.Text.Json.JsonSerializer.SerializeToElement(
                            new { OrigenTemp = specs.NombreArchivo,
                                  DestinoSD  = System.IO.Path.GetDirectoryName(specs.RutaSD)!.Replace('\\', '/') })
                    }
                };

                modulos.Add(new Models.ModuloConfig
                {
                    Id     = $"img_{specs.NombreArchivo}",
                    Nombre = specs.Titulo,
                    Versiones = new List<Models.ModuloVersion>
                    {
                        new() { Version = "custom", PipelineInstalacion = pipeline }
                    }
                });
            }

            if (modulos.Count == 0) return;
            InstalacionSolicitada?.Invoke(this, new SesionAsistida { Modulos = modulos });
        }

        private void BtnVerResumenCheckout_Click(object sender, RoutedEventArgs e)
            => IrAPasoLogico(PasoLogicoResumen);

        // ????????????????????????????????????????????????????????
        //  Fix 3: CHECKOUT BAR — contador inteligente
        // ????????????????????????????????????????????????????????

        private void ActualizarCheckout()
        {
            int n = _itemsCheckout.Count;
            BarraCheckout.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;

            int pilares      = _itemsCheckout.Count(x => !x.EsComplemento);
            int complementos = _itemsCheckout.Count(x =>  x.EsComplemento);

            TxtContadorCheckout.Text = complementos > 0
                ? $"{pilares} pilar{(pilares != 1 ? "es" : "")} \u00B7 {complementos} complemento{(complementos != 1 ? "s" : "")}"
                : $"{pilares} pilar{(pilares != 1 ? "es" : "")} seleccionado{(pilares != 1 ? "s" : "")}";
        }

        private void BtnEliminarCheckout_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ItemCheckoutVM item }) return;
            _itemsCheckout.Remove(item);

            if (!item.EsComplemento)
                foreach (var kv in _seleccionesPilar.Where(kv => kv.Value == item.Modulo).ToList())
                    _seleccionesPilar[kv.Key] = null;
            else
            {
                _recomendadosSeleccionados.Remove(item.Modulo);
                foreach (var subs in _complementosPorPilar.Values)
                    foreach (var sub in subs)
                        sub.Seleccionados.Remove(item.Modulo);
            }

            ActualizarCheckout();

            if (!EsPasoComplemento(_pasoLogico) && _pasoLogico < PasoLogicoResumen)
                ActualizarBotonSiguientePilar();
        }

        private void BtnEliminarComplemento_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ModuloConfig modulo }) return;

            foreach (var subs in _complementosPorPilar.Values)
                foreach (var sub in subs)
                    sub.Seleccionados.Remove(modulo);

            var item = _itemsCheckout.FirstOrDefault(x => x.Modulo == modulo && x.EsComplemento);
            if (item != null) _itemsCheckout.Remove(item);

            ActualizarCheckout();
        }

        // ????????????????????????????????????????????????????????
        //  Helpers de filtrado
        // ????????????????????????????????????????????????????????

        private List<ModuloConfig> FiltrarPorEtiquetas(IReadOnlyList<string> etiquetas)
            => _todosModulos.Where(m => etiquetas.Any(e => CoincideEtiqueta(m, e))).ToList();

        private List<ModuloConfig> FiltrarPorEtiqueta(string etiqueta)
            => _todosModulos.Where(m => CoincideEtiqueta(m, etiqueta)).ToList();

        private static bool CoincideEtiqueta(ModuloConfig modulo, string etiqueta)
            => modulo.Etiquetas?.Any(t => string.Equals(t, etiqueta, StringComparison.OrdinalIgnoreCase)) == true;

        // ????????????????????????????????????????????????????????
        //  Hover sound
        // ????????????????????????????????????????????????????????

        private void BtnInfoComplemento_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.DataContext is ModuloConfig modulo)
            {
                e.Handled = true;
                Servicios.Sonidos.Reproducir(EventoSonido.Click);
                DetalleModuloSolicitado?.Invoke(this, modulo);
            }
        }

        private void TarjetaModo_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Hover);
        }

        private void Slot_HoverSonido(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Servicios.Sonidos.Reproducir(EventoSonido.Hover);
        }

        private void ComplementoInnerItems_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ItemsControl ic)
                SuscribirHoverSonido(ic);
        }

        private void SuscribirHoverSonido(ItemsControl itemsControl)
        {
            itemsControl.ItemContainerGenerator.StatusChanged += (_, _) =>
            {
                if (itemsControl.ItemContainerGenerator.Status !=
                    System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated) return;

                foreach (var item in itemsControl.Items)
                {
                    var cp = itemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                    if (cp != null && cp.Tag is not "hover_wired")
                    {
                        cp.MouseEnter += (_, _) => Servicios.Sonidos.Reproducir(EventoSonido.Hover);
                        cp.Tag = "hover_wired";
                    }
                }
            };
        }

        // ????????????????????????????????????????????????????????
        //  Info button detection
        // ????????????????????????????????????????????????????????

        private static bool EsClickEnBotonInfo(DependencyObject? source)
        {
            var current = source;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.Name == "BtnInfoAsist") return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }
    }
}
